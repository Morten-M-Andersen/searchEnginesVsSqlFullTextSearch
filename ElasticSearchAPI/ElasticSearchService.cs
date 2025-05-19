using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ElasticSearchAPI
{
    public interface IElasticSearchService
    {
        Task<SearchResult> SearchAsync(string query, int size = 50);
        Task<SearchResult> AdvancedSearchAsync(SearchRequest request);
    }

    public class ElasticSearchService : IElasticSearchService
    {
        private readonly ElasticsearchClient _client;
        private readonly string _indexName;

        public ElasticSearchService(string elasticUrl = "http://localhost:9200", string indexName = "spareparts")
        {
            _indexName = indexName;

            var settings = new ElasticsearchClientSettings(new Uri(elasticUrl))
                .DefaultIndex(indexName)
                .ServerCertificateValidationCallback((sender, certificate, chain, errors) => true);

            _client = new ElasticsearchClient(settings);
        }

        public async Task<SearchResult> SearchAsync(string query, int size = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Search query cannot be empty", nameof(query));
            }

            // Opret en simpel MultiMatchQuery, der søger i alle tekstfelter
            var multiMatchQuery = new MultiMatchQuery
            {
                Query = query,
                Fields = new[]
                {
                    "name",
                    "sparePartNo",
                    "sparePartSerialCode",
                    "description",
                    "typeNo",
                    "manufacturerName",
                    "manufacturerNo",
                    "categoryName",
                    "categoryNo",
                    "supplierName",
                    "locationName",
                    "unitName",
                    "notes",
                    "manufacturerNotes",
                    "categoryDescription",
                    "supplierContactInfo",
                    "supplierNotes",
                    "locationArea",
                    "locationBuilding",
                    "locationNotes"
                },
                Type = TextQueryType.BestFields,
                Fuzziness = new Fuzziness("AUTO")
            };

            var searchResponse = await _client.SearchAsync<SparePartDocument>(s => s
                .Index(_indexName)
                .Size(size)
                .Query(multiMatchQuery)
            );

            if (!searchResponse.IsValidResponse)
            {
                throw new Exception($"Error in search: {searchResponse.DebugInformation}");
            }

            return new SearchResult
            {
                Total = searchResponse.Total,
                Took = searchResponse.Took,
                Ids = searchResponse.Documents.Select(doc => doc.Id).ToList()
            };
        }

        public async Task<SearchResult> AdvancedSearchAsync(SearchRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                throw new ArgumentException("Search request and query cannot be empty");
            }

            var searchResponse = await _client.SearchAsync<SparePartDocument>(s => s
                .Index(_indexName)
                .Size(request.Size)
                .From(request.From)
                .Query(BuildQuery(request))
            );

            if (!searchResponse.IsValidResponse)
            {
                throw new Exception($"Error in search: {searchResponse.DebugInformation}");
            }

            return new SearchResult
            {
                Total = searchResponse.Total,
                Took = searchResponse.Took,
                Ids = searchResponse.Documents.Select(doc => doc.Id).ToList()
            };
        }

        private Query BuildQuery(SearchRequest request)
        {
            // Opret en liste af queries som vi vil kombinere
            var queries = new List<Query>();

            // Tilføj multi-match søgning på tværs af felter
            var multiMatchQuery = new MultiMatchQuery
            {
                Query = request.Query,
                Fields = new[]
                {
                    "name",
                    "sparePartNo",
                    "sparePartSerialCode",
                    "description",
                    "typeNo",
                    "manufacturerName",
                    "manufacturerNo",
                    "categoryName",
                    "categoryNo",
                    "supplierName",
                    "locationName",
                    "unitName",
                    "unitNo",
                    "notes",
                    "manufacturerNotes",
                    "categoryDescription",
                    "supplierContactInfo",
                    "supplierNotes",
                    "locationArea",
                    "locationBuilding",
                    "locationNotes"
                },
                Type = TextQueryType.BestFields,
                Fuzziness = new Fuzziness("AUTO")
            };

            queries.Add(multiMatchQuery);

            // Tilføj UnitNo filter hvis angivet
            if (!string.IsNullOrEmpty(request.UnitNo))
            {
                queries.Add(new TermQuery { Field = "unitNo.keyword", Value = request.UnitNo });
            }

            // Kombiner alle queries med bool/must
            return new BoolQuery { Must = queries };
        }
    }

    public class SearchResult
    {
        public long Total { get; set; }
        public long Took { get; set; }
        public List<string> Ids { get; set; } = new List<string>();
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
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("sparePartNo")]
        public string SparePartNo { get; set; }

        [JsonPropertyName("sparePartSerialCode")]
        public string SparePartSerialCode { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("typeNo")]
        public string TypeNo { get; set; }

        [JsonPropertyName("notes")]
        public string Notes { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("manufacturerNo")]
        public string ManufacturerNo { get; set; }

        [JsonPropertyName("manufacturerName")]
        public string ManufacturerName { get; set; }

        [JsonPropertyName("manufacturerNotes")]
        public string ManufacturerNotes { get; set; }

        [JsonPropertyName("categoryNo")]
        public string CategoryNo { get; set; }

        [JsonPropertyName("categoryName")]
        public string CategoryName { get; set; }

        [JsonPropertyName("categoryDescription")]
        public string CategoryDescription { get; set; }

        [JsonPropertyName("supplierNo")]
        public string SupplierNo { get; set; }

        [JsonPropertyName("supplierName")]
        public string SupplierName { get; set; }

        [JsonPropertyName("supplierContactInfo")]
        public string SupplierContactInfo { get; set; }

        [JsonPropertyName("supplierNotes")]
        public string SupplierNotes { get; set; }

        [JsonPropertyName("locationNo")]
        public string LocationNo { get; set; }

        [JsonPropertyName("locationName")]
        public string LocationName { get; set; }

        [JsonPropertyName("locationArea")]
        public string LocationArea { get; set; }

        [JsonPropertyName("locationBuilding")]
        public string LocationBuilding { get; set; }

        [JsonPropertyName("locationNotes")]
        public string LocationNotes { get; set; }

        [JsonPropertyName("unitNo")]
        public string UnitNo { get; set; }

        [JsonPropertyName("unitName")]
        public string UnitName { get; set; }

    }
}