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
            if (request == null)
            {
                _logger.LogWarning("SearchRequest objektet var null.");
                // Overvej at kaste ArgumentNullException eller returnere et tomt/fejl resultat
                return new SearchResult { Ids = new List<string>(), TotalHits = 0, QueryTimeMs = -1 };
            }

            if (string.IsNullOrWhiteSpace(request.Query))
            {
                _logger.LogInformation("Tom søgestreng modtaget. Returnerer ingen resultater.");
                return new SearchResult { Ids = new List<string>(), TotalHits = 0, QueryTimeMs = 0 };
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

            var queryOptions = new QueryOptions
            {
                Rows = request.PageSize,
                StartOrCursor = new StartOrCursor.Start(request.StartFrom),
                // VIGTIGT: Specificer at kun 'id'-feltet skal returneres fra Solr.
                // Dette forudsætter, at din SparePartSolrDocument klasse har en 'Id' property,
                // og at SolrNet mapper dette korrekt, selvom du kun henter dette felt.
                Fields = new[] { "id" },
                ExtraParams = new Dictionary<string, string>
        {
            { "defType", "edismax" },
            {"qf", qfValue}, // Brug den sammensatte qf værdi
            //{"mm", "2<75%"} // Minimum match parameter, kan justeres efter behov
            // Andre parametre kan tilføjes her efter behov, f.eks. "mm" for minimum match, "pf" for phrase fields osv.
        }
            };

            // Tilføj kun qf parameteren, hvis den faktisk har en værdi.
            // At sende en tom "qf=" kan have uønsket adfærd i Solr.
            //if (!string.IsNullOrWhiteSpace(qfValue))
            //{
            //    queryOptions.ExtraParams.Add("qf", qfValue);
            //}
            //else
            //{
            //    // Hvis qfValue er tom, og edismax kræver qf, kan Solr returnere en fejl.
            //    // Overvej om du skal logge en mere alvorlig advarsel eller kaste en fejl her,
            //    // hvis en tom qf ikke er en acceptabel tilstand for din applikation.
            //    _logger.LogWarning("qf parameteren er tom for edismax søgning. Resultater kan være uforudsigelige.");
            //}

            try
            {
                _logger.LogDebug("Udfører Solr query: '{Query}' med qf: '{QueryFields}', Rows: {Rows}, Start: {Start}",
                    request.Query, qfValue, request.PageSize, request.StartFrom);

                // _solr skal være af typen ISolrOperations<SparePartSolrDocument>
                var solrQuery = new SolrQuery(request.Query);
                var solrResults = await _solr.QueryAsync(solrQuery, queryOptions);

                // Map SolrNet's resultat (ISolrQueryResults<SparePartSolrDocument>) 
                // til din fælles SearchResult DTO.
                // solrResults vil indeholde SparePartSolrDocument objekter, hvor kun Id-property
                // (og evt. score) er udfyldt, fordi vi specificerede Fields = new[] { "id" }.
                return new SearchResult
                {
                    TotalHits = solrResults.NumFound,
                    QueryTimeMs = solrResults.Header?.QTime ?? 0, // QTime er Solr's interne query tid
                    Ids = solrResults.Select(doc => doc.Id).ToList() // Sørg for at SparePartSolrDocument har en 'Id' string property
                    
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl under udførelse af Solr query for: {Query}", request.Query);
                // Returner et tomt resultat eller kast en specifik exception, der kan håndteres i controlleren
                return new SearchResult { Ids = new List<string>(), TotalHits = 0, QueryTimeMs = -1 }; // Indikerer fejl
            }
        }
    }
}