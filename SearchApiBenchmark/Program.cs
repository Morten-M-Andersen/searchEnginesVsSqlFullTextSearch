using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SearchApiBenchmark.SearchModels;

namespace SearchApiBenchmark
{
    public class Program
    {
        // Konfigurer URL til dit API. Juster portnummeret hvis nødvendigt.
        private static readonly string ApiBaseUrl = "http://localhost:7017"; // <<-- JUSTER DENNE!
        private static readonly HttpClient client = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };

        // Liste af søgeforespørgsler til test
        private static readonly List<string> SimpleSearchQueries = new List<string>
        {
            "engine",
            "pump",
            "butterfly",
            "gasket",
            "screw",
            "pressure",
            "enmegetsjældentingderikkefindes" // Query, der giver 0 resultater
        };

        private static readonly List<SearchRequest> AdvancedSearchRequests = new List<SearchRequest>
        {
            new SearchRequest { Query = "sensor", UnitNo = "UNIT-001" },
            new SearchRequest { Query = "kabel", Size = 10 },
            new SearchRequest { Query = "filter", UnitNo = "UNIT-002", Size = 5, From = 0 },
            new SearchRequest { Query = "olie", UnitNo = "ENHED-IKKE-FUNDET" }
        };

        // Antal gange hver query skal køres
        private static readonly int Iterations = 10;
        // Antal samtidige forespørgsler
        private static readonly int ConcurrencyLevel = 5;


        public static async Task Main(string[] args)
        {
            Console.WriteLine($"Starter benchmark af API på: {ApiBaseUrl}");
            Console.WriteLine($"Iterationer pr. query: {Iterations}");
            Console.WriteLine($"Samtidighedsniveau: {ConcurrencyLevel}");
            Console.WriteLine("----------------------------------------------------");

            // Opvarmning (kør et par requests for at JIT compiler, cache etc. kan "varme op")
            Console.WriteLine("Opvarmer API...");
            await RunSimpleSearchAsync(SimpleSearchQueries.First(), 1);
            await RunAdvancedSearchAsync(AdvancedSearchRequests.First(), 1);
            Console.WriteLine("Opvarmning færdig.\n");


            Console.WriteLine("### Benchmark: Simple Search (/api/Search/search) ###");
            var simpleSearchTimings = new List<BenchmarkResult>();
            foreach (var query in SimpleSearchQueries)
            {
                var results = await RunSimpleSearchAsync(query, Iterations, ConcurrencyLevel);
                simpleSearchTimings.AddRange(results);
                Console.WriteLine($"Query: '{query}' - Gennemsnit: {results.Average(r => r.Duration.TotalMilliseconds):F2} ms, Solr Took Avg: {results.Where(r => r.SolrTookMs.HasValue).Average(r => r.SolrTookMs):F2} ms ({results.Count(r => r.Success)}/{results.Count} succes)");
            }
            PrintAggregatedResults("Simple Search", simpleSearchTimings);


            Console.WriteLine("\n### Benchmark: Advanced Search (/api/Search/advanced-search) ###");
            var advancedSearchTimings = new List<BenchmarkResult>();
            foreach (var request in AdvancedSearchRequests)
            {
                var results = await RunAdvancedSearchAsync(request, Iterations, ConcurrencyLevel);
                advancedSearchTimings.AddRange(results);
                Console.WriteLine($"Query: '{request.Query}', UnitNo: '{request.UnitNo ?? "N/A"}' - Gennemsnit: {results.Average(r => r.Duration.TotalMilliseconds):F2} ms, Solr Took Avg: {results.Where(r => r.SolrTookMs.HasValue).Average(r => r.SolrTookMs):F2} ms ({results.Count(r => r.Success)}/{results.Count} succes)");
            }
            PrintAggregatedResults("Advanced Search", advancedSearchTimings);

            Console.WriteLine("\nBenchmark færdig. Tryk på en tast for at afslutte.");
            Console.ReadKey();
        }

        private static async Task<List<BenchmarkResult>> RunSimpleSearchAsync(string query, int iterations, int concurrencyLevel = 1)
        {
            var results = new List<BenchmarkResult>();
            var tasks = new List<Task>();
            var stopwatch = new Stopwatch();

            for (int i = 0; i < iterations; i++)
            {
                // Begræns samtidighed
                if (tasks.Count >= concurrencyLevel)
                {
                    var completedTask = await Task.WhenAny(tasks);
                    tasks.Remove(completedTask);
                }

                tasks.Add(Task.Run(async () =>
                {
                    stopwatch.Restart();
                    bool success = false;
                    int? solrTookMs = null;
                    int totalHits = 0;
                    try
                    {
                        var response = await client.GetAsync($"api/Search/search?query={Uri.EscapeDataString(query)}&size=50");
                        success = response.IsSuccessStatusCode;
                        if (success)
                        {
                            var searchResult = await response.Content.ReadFromJsonAsync<SearchResult>();
                            if (searchResult != null)
                            {
                                solrTookMs = searchResult.Took;
                                totalHits = searchResult.Total;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Fejl ved simpel søgning '{query}': {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Undtagelse ved simpel søgning '{query}': {ex.Message}");
                        success = false;
                    }
                    stopwatch.Stop();
                    lock (results) // Sørg for trådsikker tilføjelse til listen
                    {
                        results.Add(new BenchmarkResult(stopwatch.Elapsed, success, solrTookMs, totalHits, $"Simple: {query}"));
                    }
                }));
            }
            await Task.WhenAll(tasks); // Vent på resterende tasks
            return results;
        }

        private static async Task<List<BenchmarkResult>> RunAdvancedSearchAsync(SearchRequest searchRequest, int iterations, int concurrencyLevel = 1)
        {
            var results = new List<BenchmarkResult>();
            var tasks = new List<Task>();
            var stopwatch = new Stopwatch();

            for (int i = 0; i < iterations; i++)
            {
                // Begræns samtidighed
                if (tasks.Count >= concurrencyLevel)
                {
                    var completedTask = await Task.WhenAny(tasks);
                    tasks.Remove(completedTask);
                }

                tasks.Add(Task.Run(async () =>
                {
                    stopwatch.Restart();
                    bool success = false;
                    int? solrTookMs = null;
                    int totalHits = 0;
                    try
                    {
                        var response = await client.PostAsJsonAsync("api/Search/advanced-search", searchRequest);
                        success = response.IsSuccessStatusCode;
                        if (success)
                        {
                            var searchResult = await response.Content.ReadFromJsonAsync<SearchResult>();
                            if (searchResult != null)
                            {
                                solrTookMs = searchResult.Took;
                                totalHits = searchResult.Total;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Fejl ved avanceret søgning '{searchRequest.Query}': {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Undtagelse ved avanceret søgning '{searchRequest.Query}': {ex.Message}");
                        success = false;
                    }
                    stopwatch.Stop();
                    lock (results) // Sørg for trådsikker tilføjelse til listen
                    {
                        results.Add(new BenchmarkResult(stopwatch.Elapsed, success, solrTookMs, totalHits, $"Advanced: {searchRequest.Query} | Unit: {searchRequest.UnitNo}"));
                    }
                }));
            }
            await Task.WhenAll(tasks); // Vent på resterende tasks
            return results;
        }

        private static void PrintAggregatedResults(string title, List<BenchmarkResult> allTimings)
        {
            if (!allTimings.Any())
            {
                Console.WriteLine($"Ingen resultater for {title}.");
                return;
            }

            var successfulTimings = allTimings.Where(t => t.Success).ToList();
            if (!successfulTimings.Any())
            {
                Console.WriteLine($"Ingen succesfulde resultater for {title}.");
                return;
            }

            Console.WriteLine($"\n--- Samlet Statistik for {title} ({successfulTimings.Count} succesfulde / {allTimings.Count} total) ---");
            Console.WriteLine($"Total tid for alle requests: {TimeSpan.FromMilliseconds(successfulTimings.Sum(t => t.Duration.TotalMilliseconds)).TotalSeconds:F2} sekunder");
            Console.WriteLine($"Min. svartid: {successfulTimings.Min(t => t.Duration.TotalMilliseconds):F2} ms");
            Console.WriteLine($"Max. svartid: {successfulTimings.Max(t => t.Duration.TotalMilliseconds):F2} ms");
            Console.WriteLine($"Gns. svartid: {successfulTimings.Average(t => t.Duration.TotalMilliseconds):F2} ms");

            var solrTimings = successfulTimings.Where(t => t.SolrTookMs.HasValue).Select(t => t.SolrTookMs.Value).ToList();
            if (solrTimings.Any())
            {
                Console.WriteLine($"Gns. Solr QTime: {solrTimings.Average():F2} ms");
            }

            // Beregn percentiler for svartider
            var sortedDurations = successfulTimings.Select(t => t.Duration.TotalMilliseconds).OrderBy(d => d).ToList();
            Console.WriteLine($"Median (50th percentile): {GetPercentile(sortedDurations, 0.50):F2} ms");
            Console.WriteLine($"90th percentile: {GetPercentile(sortedDurations, 0.90):F2} ms");
            Console.WriteLine($"95th percentile: {GetPercentile(sortedDurations, 0.95):F2} ms");
            Console.WriteLine($"99th percentile: {GetPercentile(sortedDurations, 0.99):F2} ms");
            Console.WriteLine("----------------------------------------------------");
        }

        // Simpel metode til at beregne percentil
        public static double GetPercentile(List<double> sortedData, double percentile)
        {
            if (!sortedData.Any()) return 0;
            int N = sortedData.Count;
            double n = (N - 1) * percentile + 1;
            if (n == 1d) return sortedData[0];
            if (n == N) return sortedData[N - 1];
            int k = (int)n;
            double d = n - k;
            return sortedData[k - 1] + d * (sortedData[k] - sortedData[k - 1]);
        }
    }

    // En lille klasse til at holde benchmark resultater
    public class BenchmarkResult
    {
        public TimeSpan Duration { get; }
        public bool Success { get; }
        public int? SolrTookMs { get; } // Hvor lang tid Solr internt brugte
        public int TotalHits { get; }
        public string QueryIdentifier { get; }


        public BenchmarkResult(TimeSpan duration, bool success, int? solrTookMs, int totalHits, string queryIdentifier)
        {
            Duration = duration;
            Success = success;
            SolrTookMs = solrTookMs;
            TotalHits = totalHits;
            QueryIdentifier = queryIdentifier;
        }
    }
}