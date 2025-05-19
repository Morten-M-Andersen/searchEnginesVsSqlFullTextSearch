using ElasticSearch;
using System;
using System.Threading.Tasks;

class Program
{
    // Konfiguration - juster disse værdier efter behov
    private const string ConnectionString = "Server=localhost;Database=SparePartsDB;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string ElasticUrl = "http://localhost:9200";
    private const string IndexName = "spareparts";
    private const int BatchSize = 10000;

    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("===== ELASTICSEARCH INDEXER =====");
            Console.WriteLine($"Tidspunkt: {DateTime.Now}");
            Console.WriteLine();

            // Vis konfiguration
            Console.WriteLine("Konfiguration:");
            Console.WriteLine($"- Elasticsearch URL: {ElasticUrl}");
            Console.WriteLine($"- Indeks navn: {IndexName}");
            Console.WriteLine($"- Batch størrelse: {BatchSize}");
            Console.WriteLine($"- SQL Connection: {ConnectionString}");
            Console.WriteLine();

            // Opret indexer
            var indexer = new ElasticSearchIndexer(
                ConnectionString,
                ElasticUrl,
                IndexName
            );

            // Menu
            while (true)
            {
                Console.WriteLine("\nVælg en handling:");
                Console.WriteLine("1. Opret/Nulstil Elasticsearch indeks");
                Console.WriteLine("2. Indekser data fra SQL Server");
                Console.WriteLine("3. Udfør begge handlinger (anbefalet første gang)");
                Console.WriteLine("4. Indekser data fra SQL Server - Parallelt");
                Console.WriteLine("0. Afslut");
                Console.Write("\nValg: ");

                if (!int.TryParse(Console.ReadLine(), out int choice))
                {
                    Console.WriteLine("Ugyldigt valg. Prøv igen.");
                    continue;
                }

                switch (choice)
                {
                    case 0:
                        Console.WriteLine("\nAfslutter programmet...");
                        return;

                    case 1:
                        Console.WriteLine("\nOpretter/nulstiller Elasticsearch indeks...");
                        await indexer.SetupIndexAsync();
                        Console.WriteLine("Indeks oprettet/nulstillet!");
                        break;

                    case 2:
                        Console.WriteLine("\nIndekserer data fra SQL Server...");
                        Console.WriteLine($"(Dette kan tage noget tid afhængigt af datamængde)");
                        await indexer.IndexDataAsync(BatchSize);
                        Console.WriteLine("Indeksering færdig!");
                        break;

                    case 3:
                        Console.WriteLine("\nOpretter/nulstiller indeks og indekserer data fra SQL Server...");
                        await indexer.SetupIndexAsync();
                        Console.WriteLine("Indeks oprettet/nulstillet!");
                        Console.WriteLine("Indekserer data fra SQL Server...");
                        await indexer.IndexDataAsync(BatchSize);
                        Console.WriteLine("Indeksering færdig!");
                        break;

                    case 4:
                        Console.WriteLine("\nIndekserer data fra SQL Server... Parallelt");
                        Console.WriteLine($"(Dette kan tage noget tid afhængigt af datamængde)");
                        await indexer.IndexDataParallelAsync(BatchSize);
                        Console.WriteLine("Indeksering færdig!");
                        break;

                    default:
                        Console.WriteLine("Ugyldigt valg. Prøv igen.");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FEJL: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.ResetColor();
            Console.WriteLine("\nTryk en tast for at afslutte...");
            Console.ReadKey();
        }
    }
}