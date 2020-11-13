using System;
using Nest;

namespace Demo.Elasticsearch.SearchModel
{
    // If you want to use something other than Id is the _id of the document, add
    // the following attribute
    //[ElasticsearchType(IdProperty = nameof(Name))]
    public class Book
    {
        #region Properties
        [Keyword]
        public Guid Id { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        #endregion
    }
}
