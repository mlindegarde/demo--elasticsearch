﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AutoFixture;
using Demo.Elasticsearch.SearchModel;
using Microsoft.Extensions.Configuration;
using Nest;
using Serilog;

namespace Demo.Elasticsearch
{
    class Program
    {
        #region Member Variables
        private AppConfig _appConfig;

        private ElasticClient _esClient;
        private List<EpFile> _epFiles;
        private List<Book> _books;

        private int _titleIndex = 0;
        private int _descIndex = 0;
        #endregion

        private async Task InitClientAsync()
        {
            // Setup Serilog to log to the console
            Log.Logger =
                new LoggerConfiguration()
                    .WriteTo.Console()
                    .CreateLogger();

            // You load this in your startup.cs (either in the constructor or before you configure your
            // services in ConfigureServices
            _appConfig =
                new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build()
                    .Get<AppConfig>();

            // Once loaded inject the ElasticsearchConfig into the IoC container
            //services.AddSingleton(appConfig.Elasticsearch)

            // Now that you've added the ElasticsearchConfig to your IoC container it will be populated
            // via the constructor (e.g. your repository constructor)

            ConnectionSettings settings =
                new ConnectionSettings(_appConfig.Elasticsearch.Uri)
                    .DefaultMappingFor<EpFile>(m => m.IndexName(_appConfig.Elasticsearch.EpFileIndex));

            _esClient = new ElasticClient(settings);

            await _esClient.Indices.DeleteAsync(_appConfig.Elasticsearch.BookIndex);

            CreateIndexResponse response = await _esClient.Indices.CreateAsync(
                _appConfig.Elasticsearch.BookIndex,
                c => c.Map<Book>(m => m.AutoMap()));

            Console.WriteLine(response.Index);
        }

        private void GenerateData()
        {
            _books = 
                new Fixture()
                    .Build<Book>()
                    .With(b => b.Title, () => $"Title - {_titleIndex++}")
                    .With(b => b.Description, () => $"Description - {_descIndex++}")
                    .CreateMany(10)
                    .ToList();
        }

        private async Task InsertDocumentsAsync()
        {
            foreach (Book book in _books)
            {
                IndexResponse response = await _esClient.IndexAsync(book, i => i.Index(_appConfig.Elasticsearch.BookIndex));

                Log.Information(
                    "Inserted {Title}, has Id {Id}",
                    book.Title,
                    response.Id);
            }
        }

        #region Book Update Methods
        private async Task<bool> UpdateByQueryAsync()
        {
            Guid bookId = _books.First().Id;

            UpdateByQueryResponse response = 
                await _esClient.UpdateByQueryAsync<Book>(
                    desc =>
                        desc.Index(_appConfig.Elasticsearch.BookIndex)
                            .Query(
                                query =>
                                    query.Term(b => b.Id, bookId.ToString()))
                            .Script(
                                script =>
                                    script
                                        .Source(
                                            "ctx._source.title = params.title;" +
                                            "ctx._source.description = params.description;")
                                        .Params(
                                            new Dictionary<string, object>()
                                            {
                                                {"title", "new title"},
                                                {"description", "new desc"}
                                            })));

            return response.Updated > 0;
        }

        private async Task<bool> UpdateAsync()
        {
            Book book = _books.Last();

            book.Title = "Updated from update";
            book.Description = "also updated from update";

            UpdateResponse<Book> response = await _esClient.UpdateAsync<Book>(
                book.Id,
                    desc =>
                        desc
                            .Index(_appConfig.Elasticsearch.BookIndex)
                            .Doc(book));

            return response.Result == Result.Updated;
        }
        #endregion

        #region EpSearch Methods
        private async Task<bool> SearchForExactTitleMatchAsync()
        {
            string title = _epFiles.First().Title;

            // MatchPhrase will attempt to find an exact string match.  If you just
            // just Match it will try to match using the field analyzer that was used
            // when the field was created.  You can read more about searching here:
            //    https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/search.html

            ISearchResponse<EpFile> searchResponse =
                await _esClient.SearchAsync<EpFile>(
                    search =>
                        search
                            .Query(
                                query =>
                                    query.MatchPhrase(
                                        match =>
                                            match
                                                .Field(file => file.Title)
                                                .Query(title))));

            List<EpFile> matches = searchResponse.Documents.ToList();

            Log.Information(
                "{Method} searched for {Title}, found {Count} match(es)",
                Caller(),
                title,
                matches.Count);

            return matches.Any();
        }

        private async Task<bool> SearchForAnyMatchAsync()
        {
            string fileText = "runs and jumps";
            Log.Information(
                "{Method} search for {FileText}",
                Caller(),
                fileText);

            ISearchResponse<EpFile> searchResponse =
                await _esClient.SearchAsync<EpFile>(
                    search =>
                        search
                            .From(0)
                            .Size(10)
                            .Query(
                                query =>
                                    query.Match(
                                        match =>
                                            match
                                                .Field(file => file.FileText)
                                                .Query(fileText))));
                                        

            // This list will contain just the objects that matched the above query
            List<EpFile> matches = searchResponse.Documents.ToList();

            // This list will give us some additional details about the matches.  You can
            // get to the object via the hit.Source property.
            List<IHit<EpFile>> hits = searchResponse.Hits.OrderBy(h => h.Score).ToList();

            Log.Information(
                "\r\n\tFound {Count} match(es)\r\n\tHigh Score = {HighScore}\r\n\tLow Score = {LowScore}", 
                matches.Count,
                hits.First().Score,
                hits.Last().Score);

            return matches.Any();
        }

        private async Task<bool> BoolOrSearchAsync()
        {
            string text = _epFiles.First().FileName;

            ISearchResponse<EpFile> searchResponse =
                await _esClient.SearchAsync<EpFile>(
                    search =>
                        search
                            .From(0)
                            .Size(10)
                            .Query(
                                query =>
                                    query.Match(
                                        match =>
                                            match
                                                .Field(file => file.FileText)
                                                .Query(text)) ||
                                    query.MatchPhrase(
                                        match =>
                                            match
                                                .Field(file => file.Title)
                                                .Query(text)) ||
                                    query.MatchPhrase(
                                        match =>
                                            match
                                                .Field(file => file.FileName)
                                                .Query(text))));


            List<EpFile> matches = searchResponse.Documents.ToList();

            Log.Information(
                "{Method} found {Count} match(es)",
                Caller(),
                matches.Count);

            return matches.Any();
        }
        #endregion

        private void End()
        {
            Console.WriteLine("Press ENTER to exit: ");
            Console.ReadLine();
        }

        #region Entry Point
        static async Task Main()
        {
            Program program = new Program();

            await program.InitClientAsync();
            program.GenerateData();
            
            await program.InsertDocumentsAsync();

            /*
            // Elasticearch by default uses eventual consistency.  This means that the write
            // operation you just did may not have completed.  For the sake of this demo I
            // am running the method until I get teh matches I expect.  You should not do this
            // in real life.  It's bad.  You can also force ES to be real-time.  I don't recommend
            // taking that approach.
            await RunUntilSuccess(program.SearchForExactTitleMatchAsync);
            await RunUntilSuccess(program.SearchForAnyMatchAsync);
            await RunUntilSuccess(program.BoolOrSearchAsync);
            */

            await RunUntilSuccess(program.UpdateByQueryAsync);
            await RunUntilSuccess(program.UpdateAsync);

            program.End();
        }
        #endregion

        #region Utility Methods
        private static string Caller([CallerMemberName] string caller = null) => caller;

        private static async Task RunUntilSuccess(Func<Task<bool>> search)
        {
            bool success = false;

            while (!success)
            {
                success = await search();

                if (!success)
                    await Task.Delay(100);
            }
        }
        #endregion
    }
}
