using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Net.Http;
using System.Threading.Tasks;
using SearchBenchmarking.Library.DTOs;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace SearchBenchmarking.PerformanceTests.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 10)]
    [MemoryDiagnoser]
    [RankColumn, MinColumn, MaxColumn, Q1Column, Q3Column, MeanColumn, MedianColumn, StdDevColumn] // AllStatisticsColumn]
    public class SearchApiBenchmarks
    {
        private HttpClient _httpClient;
        private List<string> _searchTerms;

        // --- Parametre for at vælge API og søgeterm ---
        // BenchmarkDotNet vil køre en kombination for hver værdi af ApiName og CurrentSearchTerm.

        [Params(ApiTarget.Solr, ApiTarget.Elasticsearch, ApiTarget.Sql)] // Definer hvilke API'er der skal testes
        public ApiTarget TargetApi { get; set; }

        public string CurrentSearchTerm { get; set; }

        private string _apiBaseUrl;

        // Enum for at gøre valget af API mere læseligt
        public enum ApiTarget
        {
            Solr,
            Elasticsearch,
            Sql
        }

        // API URL'er - juster portnumre efter behov
        private const string SolrApiBaseUrl = "http://localhost:5015/api/Search";
        private const string ElasticsearchApiBaseUrl = "http://localhost:5139/api/Search";
        private const string SqlApiBaseUrl = "http://localhost:5229/api/Search";

        [GlobalSetup]
        public void GlobalSetup()
        {
            _httpClient = new HttpClient();
            _searchTerms = File.ReadAllLines("TestData/SearchTerms.txt").Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            if (!_searchTerms.Any())
            {
                _searchTerms.Add("default term");
            }
            //string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "SearchTerms.txt");
            //Console.WriteLine($"Attempting to load search terms from: {filePath}");

            //if (File.Exists(filePath))
            //{
            //    _searchTerms = File.ReadAllLines(filePath)
            //                       .Where(line => !string.IsNullOrWhiteSpace(line))
            //                       .Select(line => line.Trim()) // Fjern evt. foranstillet/efterstillet whitespace
            //                       .ToList();
            //}

            //if (_searchTerms == null || !_searchTerms.Any())
            //{
            //    Console.WriteLine("SearchTerms.txt not found or is empty. Using default fallback term.");
            //    _searchTerms = new List<string> { "default term" }; // Fallback
            //}
            Console.WriteLine($"GlobalSetup: Loaded {_searchTerms.Count} search terms.");
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Sæt den korrekte API URL baseret på TargetApi parameteren
            switch (TargetApi)
            {
                case ApiTarget.Solr:
                    _apiBaseUrl = SolrApiBaseUrl;
                    break;
                case ApiTarget.Elasticsearch:
                    _apiBaseUrl = ElasticsearchApiBaseUrl;
                    break;
                case ApiTarget.Sql:
                    _apiBaseUrl = SqlApiBaseUrl;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(TargetApi), "Ukendt API target specificeret.");
            }
            Console.WriteLine($"IterationSetup: API = {TargetApi}, Term = {CurrentSearchTerm}, URL = {_apiBaseUrl}");
        }


        [Benchmark]
        public async Task SearchAllTermsFromFile()
        {
            if (!_searchTerms.Any()) return; // Ingen termer at søge på

            // Sæt OperationsPerInvoke dynamisk eller hårdkod det til det forventede antal termer,
            // hvis det er svært at sætte dynamisk for BenchmarkDotNet før kørsel.
            // For nu sætter vi det ikke, og tiden vil være for HELE loopet.
            // Man kan så dividere Mean tiden med _searchTerms.Count manuelt.
            // Alternativt, hvis du vil have BDN til at gøre det:
            // Sæt OperationsPerInvoke i attributten: [Benchmark(OperationsPerInvoke = ANTAL_TERMER_DU_KØRER_MED_I_LOOPET)]

            foreach (var term in _searchTerms)
            {
                var requestUrl = $"{_apiBaseUrl}?Query={Uri.EscapeDataString(term)}&PageSize=10&StartFrom=0";
                var response = await _httpClient.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                // Valgfrit: Læs/deserialiser content
            }
        }

        // Alternativ: Benchmark for én enkelt, repræsentativ term (hvis du stadig vil have det)
        // Du kan have flere [Benchmark] metoder.
        // [Benchmark]
        // public async Task SearchSingleRepresentativeTerm()
        // {
        //     string representativeTerm = "butterfly"; // Eller tag den første fra _searchTerms
        //     if (_searchTerms.Any()) representativeTerm = _searchTerms.First();

        //     var requestUrl = $"{_apiBaseUrl}?Query={Uri.EscapeDataString(representativeTerm)}&PageSize=10&StartFrom=0";
        //     var response = await _httpClient.GetAsync(requestUrl);
        //     response.EnsureSuccessStatusCode();
        // }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _httpClient?.Dispose();
            Console.WriteLine("GlobalCleanup: HttpClient disposed.");
        }
    }
}