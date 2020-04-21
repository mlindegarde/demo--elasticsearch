using System;

namespace Demo.Elasticsearch.SearchModel
{
    public class EpFile
    {
        #region Properties
        public string Id { get; set; }

        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; }

        public string SectionId { get; set; }
        public string SectionName { get; set; }
        public string SectionNumber { get; set; }

        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string Title { get; set; }
        public string FileText { get; set; }
        public DateTime CreatedOn { get; set; }
        #endregion
    }
}
