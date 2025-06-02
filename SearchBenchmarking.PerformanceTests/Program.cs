using BenchmarkDotNet.Running;
using SearchBenchmarking.PerformanceTests.Benchmarks; // Sørg for korrekt namespace

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Starting unified API benchmarks...");

        // Kør den samlede benchmark-klasse.
        // Den vil køre for hver kombination af [Params] (ApiTarget og CurrentSearchTerm).
        var summary = BenchmarkRunner.Run<SearchApiBenchmarks>();

        Console.WriteLine("Benchmarking finished.");
    }
}