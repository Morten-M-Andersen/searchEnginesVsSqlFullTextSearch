using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using BenchmarkDTO = SearchBenchmarking.Library.DTOs;
using SearchBenchmarking.Library.Interfaces;
using SearchBenchmarking.Elasticsearch.Api.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SearchBenchmarking.Elasticsearch.Api.Services
{
    public class ElasticsearchSearchService : ISearchService
    {
        private readonly ElasticsearchClient _esClient;
        private readonly ILogger<ElasticsearchSearchService> _logger;
        private readonly string _defaultIndex;
        private readonly string[]? _searchableFields; // Kan være null hvis ikke konfigureret

        public ElasticsearchSearchService(ElasticsearchClient esClient, IConfiguration configuration, ILogger<ElasticsearchSearchService> logger)
        {
            _esClient = esClient ?? throw new ArgumentNullException(nameof(esClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _defaultIndex = configuration.GetValue<string>("Elasticsearch:DefaultIndex") ?? "spareparts";
            _searchableFields = configuration.GetSection("Elasticsearch:QueryFields").Get<string[]>();

            if (_searchableFields == null || !_searchableFields.Any())
            {
                _logger.LogWarning("Elasticsearch:QueryFields er ikke konfigureret eller er tom. MultiMatch vil bruge Elasticsearch defaults eller fejle, hvis ingen default query field er sat.");
            }
        }

        public async Task<BenchmarkDTO.SearchResult> SearchAsync(BenchmarkDTO.SearchRequest request)
        {
            if (request == null)
            {
                _logger.LogWarning("SearchRequest objektet var null for Elasticsearch.");
                // Sørg for at din SearchResult DTO har ErrorMessage property
                return new BenchmarkDTO.SearchResult { ErrorMessage = "Search request cannot be null." };
            }

            if (string.IsNullOrWhiteSpace(request.Query))
            {
                _logger.LogInformation("Tom søgestreng modtaget for Elasticsearch. Returnerer ingen resultater.");
                return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), TotalHits = 0, QueryTimeMs = 0 };
            }

            try
            {
                _logger.LogDebug(
                    "Udfører Elasticsearch query: '{Query}' på index '{Index}', Fields: '{QueryFields}', Size: {Size}, From: {From}",
                    request.Query,
                    _defaultIndex,
                    (_searchableFields != null && _searchableFields.Any()) ? string.Join(",", _searchableFields) : "Elasticsearch Default",
                    request.PageSize,
                    request.StartFrom);

                // Elasticsearch returnerer _score automatisk, så vi behøver ikke specificere det i _source filter
                // men vi skal stadig begrænse _source til kun 'id'
                var response = await _esClient.SearchAsync<SparePartElasticDocument>(s => s
                    .Indices(_defaultIndex)
                    .From(request.StartFrom)
                    .Size(request.PageSize)
                    //.Query(q => q
                    //    .MultiMatch(mm =>
                    //    {
                    //        mm.Query(request.Query);
                    //        // Giv _searchableFields direkte til Fields() metoden, hvis de findes
                    //        if (_searchableFields != null && _searchableFields.Any())
                    //        {
                    //            mm.Fields(_searchableFields); // <-- Rettet her
                    //        }
                    //        // Andre MultiMatch options kan tilføjes her, f.eks. .Type(TextQueryType.BestFields)
                    //    })
                    //)
                    .Query(q => q
                        .QueryString(qs => qs
                            .Query(request.Query)// + "*") // trailing wildcard search
                            // .Query(request.Query) // Hvis søgeteksten selv indeholder * (ikke wildcard)
                            .Fields(_searchableFields) // Specificer felter
                            .AnalyzeWildcard(true) // Vigtigt for performance af ledende wildcards
                            .DefaultOperator(Operator.Or) // Eller Operator.And
                            )
                        )
                    // Begrænser _source til kun 'id' mhp. effektivitet
                    .Source(src => src
                        .Filter(f => f
                            .Includes(Infer.Field<SparePartElasticDocument>(p => p.Id))
                        )
                    )
                // .TrackScores(true) // Er typisk default, men kan specificeres eksplicit hvis man er i tvivl
                );

                if (response.IsValidResponse)
                {
                    return new BenchmarkDTO.SearchResult
                    {
                        //Ids = response.Documents.Select(doc => doc.Id).ToList(),
                        TotalHits = response.Total,
                        QueryTimeMs = (int)response.Took,
                        Hits = response.Hits.Select(hit => new BenchmarkDTO.DocumentHit
                        {
                            Id = hit.Source?.Id, // Antager Id er i _source
                            Score = (float)(hit.Score ?? 0.0) // konverter til float
                        }).ToList()
                    };
                }
                else
                {
                    string errorMessage = $"Elasticsearch query failed. Status: {response.ApiCallDetails.HttpStatusCode}.";
                    if (response.ElasticsearchServerError?.Error != null)
                    {
                        errorMessage += $" Reason: {response.ElasticsearchServerError.Error.Reason}. Type: {response.ElasticsearchServerError.Error.Type}";
                    }
                    else if (!string.IsNullOrEmpty(response.ApiCallDetails.DebugInformation))
                    {
                        errorMessage += $" DebugInfo: {response.ApiCallDetails.DebugInformation}";
                    }
                    _logger.LogError(errorMessage);
                    return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), ErrorMessage = errorMessage };
                }
            }
            catch (Elastic.Transport.TransportException transportEx)
            {
                _logger.LogError(transportEx, "Elastic TransportException under søgning for query: {Query}. DebugInformation: {DebugInfo}",
                                 request.Query,
                                 (transportEx.ApiCallDetails != null ? transportEx.ApiCallDetails.DebugInformation : "N/A"));
                return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), ErrorMessage = $"Elastic transport error: {transportEx.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Uventet fejl under Elasticsearch søgning for query: {Query}", request.Query);
                return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), ErrorMessage = $"An unexpected error occurred: {ex.Message}" };
            }
        }
    }
}