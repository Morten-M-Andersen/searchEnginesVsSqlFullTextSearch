using SolrNet;
using SolrNet.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SolrNet.Attributes;
using System.Text.Json.Serialization;

namespace ApacheSolrAPI
{
    public class SolrSearchService
    {
        private readonly ISolrOperations<SparePartDocument> _solr;

        public SolrSearchService(ISolrOperations<SparePartDocument> solr)
        {
            _solr = solr;
        }

        public async Task<SearchResult> SearchAsync(string query, int size = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Search query cannot be empty", nameof(query));
            }

            var options = new QueryOptions
            {
                Rows = size,
                Start = 0,
                ExtraParams = new Dictionary<string, string>
                {
                    {"defType", "edismax"},
                    {"qf", "name description sparePartNo manufacturerName categoryName supplierName locationName unitName"}
                }
            };

            // Use simple query with edismax
            var searchQuery = new SolrQuery(query);

            var results = await Task.Run(() => _solr.Query(searchQuery, options));

            return new SearchResult
            {
                Total = (int)results.NumFound,
                Took = results.Header?.QTime ?? 0,
                Ids = results.Select(doc => doc.Id).ToList()
            };
        }

        public async Task<SearchResult> AdvancedSearchAsync(SearchRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                throw new ArgumentException("Search request and query cannot be empty");
            }

            var queries = new List<ISolrQuery>();

            // Main search query
            var mainQuery = new SolrQuery(request.Query);
            queries.Add(mainQuery);

            // Add UnitNo filter if specified
            if (!string.IsNullOrEmpty(request.UnitNo))
            {
                queries.Add(new SolrQueryByField("unitNo", request.UnitNo));
            }

            // Combine queries
            ISolrQuery finalQuery = queries.Count == 1
                ? queries[0]
                : new SolrMultipleCriteriaQuery(queries, SolrMultipleCriteriaQuery.Operator.AND);

            var options = new QueryOptions
            {
                Rows = request.Size,
                Start = request.From,
                ExtraParams = new Dictionary<string, string>
                {
                    {"defType", "edismax"},
                    {"qf", "name description sparePartNo manufacturerName categoryName supplierName locationName unitName"}
                }
            };

            var results = await Task.Run(() => _solr.Query(finalQuery, options));

            return new SearchResult
            {
                Total = (int)results.NumFound,
                Took = results.Header?.QTime ?? 0,
                Ids = results.Select(doc => doc.Id).ToList()
            };
        }
    }

    public class SearchResult
    {
        public int Total { get; set; }
        public int Took { get; set; }
        public List<string> Ids { get; set; } = new List<string>();
        //public List<SearchDocument> Documents { get; set; } = new List<SearchDocument>();
    }

    public class SearchRequest
    {
        public string Query { get; set; }
        public int Size { get; set; } = 50;
        public int From { get; set; } = 0;
        public string? UnitNo { get; set; }
    }

    public class SparePartDocument
    {
        [SolrUniqueKey("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [SolrField("sparePartNo")]
        [JsonPropertyName("sparePartNo")]
        public ICollection<string> SparePartNo { get; set; }

        [SolrField("sparePartSerialCode")]
        [JsonPropertyName("sparePartSerialCode")]
        public ICollection<string> SparePartSerialCode { get; set; }

        [SolrField("name")]
        [JsonPropertyName("name")]
        public ICollection<string> Name { get; set; }

        [SolrField("description")]
        [JsonPropertyName("description")]
        public ICollection<string> Description { get; set; }

        [SolrField("typeNo")]
        [JsonPropertyName("typeNo")]
        public ICollection<string> TypeNo { get; set; }

        [SolrField("notes")]
        [JsonPropertyName("notes")]
        public ICollection<string> Notes { get; set; }

        [SolrField("currency")]
        [JsonPropertyName("currency")]
        public ICollection<string> Currency { get; set; }

        [SolrField("manufacturerNo")]
        [JsonPropertyName("manufacturerNo")]
        public ICollection<string> ManufacturerNo { get; set; }

        [SolrField("manufacturerName")]
        [JsonPropertyName("manufacturerName")]
        public ICollection<string> ManufacturerName { get; set; }

        [SolrField("manufacturerNotes")]
        [JsonPropertyName("manufacturerNotes")]
        public ICollection<string> ManufacturerNotes { get; set; }

        [SolrField("categoryNo")]
        [JsonPropertyName("categoryNo")]
        public ICollection<string> CategoryNo { get; set; }

        [SolrField("categoryName")]
        [JsonPropertyName("categoryName")]
        public ICollection<string> CategoryName { get; set; }

        [SolrField("categoryDescription")]
        [JsonPropertyName("categoryDescription")]
        public ICollection<string> CategoryDescription { get; set; }

        [SolrField("supplierNo")]
        [JsonPropertyName("supplierNo")]
        public ICollection<string> SupplierNo { get; set; }

        [SolrField("supplierName")]
        [JsonPropertyName("supplierName")]
        public ICollection<string> SupplierName { get; set; }

        [SolrField("supplierContactInfo")]
        [JsonPropertyName("supplierContactInfo")]
        public ICollection<string> SupplierContactInfo { get; set; }

        [SolrField("supplierNotes")]
        [JsonPropertyName("supplierNotes")]
        public ICollection<string> SupplierNotes { get; set; }

        [SolrField("locationNo")]
        [JsonPropertyName("locationNo")]
        public ICollection<string> LocationNo { get; set; }

        [SolrField("locationName")]
        [JsonPropertyName("locationName")]
        public ICollection<string> LocationName { get; set; }

        [SolrField("locationArea")]
        [JsonPropertyName("locationArea")]
        public ICollection<string> LocationArea { get; set; }

        [SolrField("locationBuilding")]
        [JsonPropertyName("locationBuilding")]
        public ICollection<string> LocationBuilding { get; set; }

        [SolrField("locationNotes")]
        [JsonPropertyName("locationNotes")]
        public ICollection<string> LocationNotes { get; set; }

        [SolrField("unitNo")]
        [JsonPropertyName("unitNo")]
        public ICollection<string> UnitNo { get; set; }

        [SolrField("unitName")]
        [JsonPropertyName("unitName")]
        public ICollection<string> UnitName { get; set; }
    }
}