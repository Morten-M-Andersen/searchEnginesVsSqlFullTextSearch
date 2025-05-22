using ApacheSolr;
using System;
using System.Threading.Tasks;

class Program
{
    // Configuration - adjust these values as needed
    private const string ConnectionString = "Server=localhost;Database=SparePartsDB;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string SolrUrl = "http://localhost:8983/solr";
    private const string CoreName = "spareparts";
    private const int BatchSize = 10000;

    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("===== APACHE SOLR INDEXER =====");
            Console.WriteLine($"Timestamp: {DateTime.Now}");
            Console.WriteLine();

            // Show configuration
            Console.WriteLine("Configuration:");
            Console.WriteLine($"- Solr URL: {SolrUrl}");
            Console.WriteLine($"- Core name: {CoreName}");
            Console.WriteLine($"- Batch size: {BatchSize}");
            Console.WriteLine($"- SQL Connection: {ConnectionString}");
            Console.WriteLine();

            // Create indexer
            var indexer = new SolrIndexer(
                ConnectionString,
                SolrUrl,
                CoreName
            );

            // Menu
            while (true)
            {
                Console.WriteLine("\nSelect an action:");
                Console.WriteLine("1. Setup/Reset Solr core and schema");
                Console.WriteLine("2. Index data from SQL Server");
                Console.WriteLine("3. Perform both actions (recommended for first time)");
                Console.WriteLine("4. Index data from SQL Server - Parallel");
                Console.WriteLine("0. Exit");
                Console.Write("\nChoice: ");

                if (!int.TryParse(Console.ReadLine(), out int choice))
                {
                    Console.WriteLine("Invalid choice. Try again.");
                    continue;
                }

                switch (choice)
                {
                    case 0:
                        Console.WriteLine("\nExiting program...");
                        return;

                    case 1:
                        Console.WriteLine("\nSetting up Solr core and schema...");
                        await indexer.SetupCoreAsync();
                        Console.WriteLine("Core setup completed!");
                        break;

                    case 2:
                        Console.WriteLine("\nIndexing data from SQL Server...");
                        Console.WriteLine($"(This may take some time depending on data volume)");
                        await indexer.IndexDataAsync(BatchSize);
                        Console.WriteLine("Indexing completed!");
                        break;

                    case 3:
                        Console.WriteLine("\nSetting up core and indexing data from SQL Server...");
                        await indexer.SetupCoreAsync();
                        Console.WriteLine("Core setup completed!");
                        Console.WriteLine("Indexing data from SQL Server...");
                        await indexer.IndexDataAsync(BatchSize);
                        Console.WriteLine("Indexing completed!");
                        break;

                    case 4:
                        Console.WriteLine("\nIndexing data from SQL Server... Parallel");
                        Console.WriteLine($"(This may take some time depending on data volume)");
                        await indexer.IndexDataParallelAsync(BatchSize);
                        Console.WriteLine("Indexing completed!");
                        break;

                    default:
                        Console.WriteLine("Invalid choice. Try again.");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.ResetColor();
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}