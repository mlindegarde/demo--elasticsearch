using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AutoFixture;
using Demo.Elasticsearch.SearchModel;
using Nest;
using Serilog;

namespace Demo.Elasticsearch
{
    class Program
    {
        #region Member Variables
        private ElasticClient _esClient;
        private List<EpFile> _epFiles;
        #endregion

        private void InitClient()
        {
            // Setup Serilog to log to the console
            Log.Logger =
                new LoggerConfiguration()
                    .WriteTo.Console()
                    .CreateLogger();

            // Setup the connection providing a mapping for the EpFile Object
            ConnectionSettings settings =
                new ConnectionSettings(new Uri("http://localhost:9200"))
                    .DefaultMappingFor<EpFile>(m => m.IndexName("epfile"));

            _esClient = new ElasticClient(settings);

            // You can explicitly create an index using the CreateAsync method.
            // For more control over the index in ES you will want to do this.
            // You can read more about it here:
            //    https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/mapping.html

            //_esClient.Indices.CreateAsync()
        }

        private void LoadDataFromSql()
        {
            /*
             * I'm just faking data for this demo.  You would want to load all relevant data
             * from SQL and de-normalize it to build the search document model.  You operational
             * database (SQL Server) should be normalized to some degree.  Your search database
             * should be denormalized as appropriate.
             */
            _epFiles = 
                new Fixture()
                    .Build<EpFile>()
                    .With(f => f.CreatedOn, DateTime.Now)
                    .With(f => f.FileText, $"The cat runs and jumps all morning long {DateTime.Now}")
                    .CreateMany(10)
                    .ToList();
        }

        private async Task InsertDocumentsAsync()
        {
            foreach (EpFile file in _epFiles)
            {
                IndexResponse response = await _esClient.IndexDocumentAsync(file);

                Log.Information(
                    "Inserted {Title}, has Id {Id}",
                    file.Title,
                    response.Id);
            }
        }

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

        private void End()
        {
            Console.WriteLine("Press ENTER to exit: ");
            Console.ReadLine();
        }

        #region Entry Point
        static async Task Main()
        {
            Program program = new Program();

            program.InitClient();
            program.LoadDataFromSql();
            
            await program.InsertDocumentsAsync();

            // Elasticearch by default uses eventual consistency.  This means that the write
            // operation you just did may not have completed.  For the sake of this demo I
            // am running the method until I get teh matches I expect.  You should not do this
            // in real life.  It's bad.  You can also force ES to be real-time.  I don't recommend
            // taking that approach.
            await RunUntilSuccess(program.SearchForExactTitleMatchAsync);
            await RunUntilSuccess(program.SearchForAnyMatchAsync);
            await RunUntilSuccess(program.BoolOrSearchAsync);

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
