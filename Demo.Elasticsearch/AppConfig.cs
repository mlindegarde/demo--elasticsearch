using System;

namespace Demo.Elasticsearch
{
    public class AppConfig
    {
        public class ElasticsearchConfig
        {
            public Uri Uri { get; set; }
            public string EpFileIndex { get; set; }
            public string BookIndex { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }

        #region Properties
        public ElasticsearchConfig Elasticsearch { get; set; }
        #endregion
    }
}
