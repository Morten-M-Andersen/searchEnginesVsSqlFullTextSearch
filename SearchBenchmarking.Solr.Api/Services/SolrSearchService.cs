using SearchBenchmarking.Library.DTOs;
using SearchBenchmarking.Library.Interfaces;
using SearchBenchmarking.Solr.Api.Documents;
using SolrNet;
using SolrNet.Commands.Parameters;

namespace SearchBenchmarking.Solr.Api.Services
{
    
    public class SolrSearchService : ISearchService
    {
        private readonly ISolrOperations<SparePartSolrDocument> _solr;
        private readonly ILogger<SolrSearchService> _logger;
        private readonly string[]? _searchableFields;

        public SolrSearchService(ISolrOperations<SparePartSolrDocument> solr, IConfiguration configuration, ILogger<SolrSearchService> logger)
        {
            _solr = solr;
            _logger = logger;

            // Læs QueryFields fra konfiguration
            _searchableFields = configuration.GetSection("Solr:QueryFields").Get<string[]>();

            if (_searchableFields == null || _searchableFields.Length == 0)
            {
                _logger.LogWarning("No searchable fields configured in Solr:QueryFields. Using default fields.");
                _searchableFields = ["_text_"]; // Default - se Solr indekser for detaljer om hvilke felter der er kopiret til _text_
            }
        }

        public async Task<SearchResult> SearchAsync(SearchRequest request)
        {
            // Tjek for null eller ugyldige værdier i request
            if (request == null)
            {
                _logger.LogWarning("SearchRequest objektet var null.");
                // Overvej at kaste ArgumentNullException eller returnere et tomt/fejl resultat
                return new SearchResult { Hits = new List<DocumentHit>(), TotalHits = 0, QueryTimeMs = -1 };
            }

            if (string.IsNullOrWhiteSpace(request.Query))
            {
                _logger.LogInformation("Tom søgestreng modtaget. Returnerer ingen resultater.");
                return new SearchResult { Hits = new List<DocumentHit>(), TotalHits = 0, QueryTimeMs = 0 };
            }

            // Sammensæt qf-strengen fra de konfigurerede søgbare felter
            string qfValue = string.Empty;
            if (_searchableFields != null && _searchableFields.Any())
            {
                qfValue = string.Join(" ", _searchableFields);
            }
            else
            {
                _logger.LogWarning("_searchableFields er ikke konfigureret eller er tom. Edismax søgning vil måske ikke fungere som forventet eller vil bruge Solr defaults for qf.");
            }

            var extraParametersForSolr = new Dictionary<string, string> { { "defType", "edismax" } };
            if (!string.IsNullOrWhiteSpace(qfValue))
            {
                extraParametersForSolr.Add("qf", qfValue);
            }

            var queryOptions = new QueryOptions
            {
                Rows = request.PageSize,
                StartOrCursor = new StartOrCursor.Start(request.StartFrom),
                // "id" er ofte Solrs unique key og returneres vistnok selvom den ikke er eksplicit i fl,
                Fields = new[] { "id", "score" },
                ExtraParams = extraParametersForSolr
            };

            try
            {
                _logger.LogDebug("Udfører Solr query: '{Query}' med ExtraParams: '{ExtraParams}', Fields: 'id,score', Rows: {Rows}, Start: {Start}",
                    request.Query, string.Join(", ", extraParametersForSolr.Select(kv => kv.Key + "=" + kv.Value)), request.PageSize, request.StartFrom);

                var solrQuery = new SolrQuery(request.Query);
                var solrResults = await _solr.QueryAsync(solrQuery, queryOptions);

                return new SearchResult
                {
                    TotalHits = solrResults.NumFound,
                    QueryTimeMs = solrResults.Header?.QTime ?? 0, // QTime er Solr's interne query tid
                    Hits = solrResults.Select(doc => new DocumentHit { Id = doc.Id, Score = doc.Score }).ToList(),

                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl under udførelse af Solr query for: {Query}", request.Query);
                // Returner et tomt resultat eller kast en specifik exception, der kan håndteres i controlleren
                return new SearchResult { Hits = new List<DocumentHit>(), ErrorMessage = $"Solr error: {ex.Message}" }; // Indikerer fejl
            }
        }
    }
}