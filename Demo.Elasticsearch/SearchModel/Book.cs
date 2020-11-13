using System;
using Nest;

namespace Demo.Elasticsearch.SearchModel
{
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
