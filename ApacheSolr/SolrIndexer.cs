using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Diagnostics;

namespace ApacheSolr
{
    public class SolrIndexer
    {
        private readonly string _connectionString;
        private readonly string _solrUrl;
        private readonly string _coreName;
        private readonly HttpClient _httpClient;
        private readonly int _commitInterval = 50000; // Commit every 50k documents

        public SolrIndexer(string sqlConnectionString, string solrUrl = "http://localhost:8983/solr", string coreName = "spareparts")
        {
            _connectionString = sqlConnectionString;
            _solrUrl = solrUrl;
            _coreName = coreName;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task SetupCoreAsync()
        {
            Console.WriteLine($"Setting up Solr core '{_coreName}'...");

            try
            {
                // First, delete all existing documents
                //await DeleteAllDocumentsAsync();

                // Create or update the schema
                await UpdateSchemaAsync();

                Console.WriteLine($"Solr core '{_coreName}' setup completed.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to setup Solr core: {ex.Message}", ex);
            }
        }

        private async Task DeleteAllDocumentsAsync()
        {
            Console.WriteLine("Deleting all existing documents...");

            var deleteQuery = new
            {
                delete = new { query = "*:*" }
            };

            var json = JsonSerializer.Serialize(deleteQuery);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_solrUrl}/{_coreName}/update?commit=true", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to delete documents: {error}");
            }

            Console.WriteLine("All documents deleted.");
        }

        private async Task UpdateSchemaAsync()
        {
            Console.WriteLine("Updating Solr schema...");

            // Define field types if they don't exist
            var fieldTypes = new[]
            {
                new
                {
                    name = "text_en",
                    @class = "solr.TextField",
                    positionIncrementGap = 100,
                    analyzer = new
                    {
                        tokenizer = new { @class = "solr.StandardTokenizerFactory" },
                        filters = new object[]
                        {
                            new { @class = "solr.LowerCaseFilterFactory" },
                            new { @class = "solr.StopFilterFactory", words = "lang/stopwords_en.txt" },
                            new { @class = "solr.PorterStemFilterFactory" }
                        }
                    }
                }
            };

            // Define fields
            var fields = new[]
            {
                // ID field (already exists by default)
                new { name = "sparePartNo", type = "string", indexed = true, stored = true },
                new { name = "sparePartSerialCode", type = "string", indexed = true, stored = true },
                new { name = "name", type = "text_en", indexed = true, stored = true },
                new { name = "description", type = "text_en", indexed = true, stored = true },
                new { name = "typeNo", type = "string", indexed = true, stored = true },
                new { name = "notes", type = "text_en", indexed = true, stored = true },
                new { name = "currency", type = "string", indexed = true, stored = true },
                
                // Manufacturer fields
                new { name = "manufacturerNo", type = "string", indexed = true, stored = true },
                new { name = "manufacturerName", type = "text_en", indexed = true, stored = true },
                new { name = "manufacturerNotes", type = "text_en", indexed = true, stored = true },
                
                // Category fields
                new { name = "categoryNo", type = "string", indexed = true, stored = true },
                new { name = "categoryName", type = "text_en", indexed = true, stored = true },
                new { name = "categoryDescription", type = "text_en", indexed = true, stored = true },
                
                // Supplier fields
                new { name = "supplierNo", type = "string", indexed = true, stored = true },
                new { name = "supplierName", type = "text_en", indexed = true, stored = true },
                new { name = "supplierContactInfo", type = "text_en", indexed = true, stored = true },
                new { name = "supplierNotes", type = "text_en", indexed = true, stored = true },
                
                // Location fields
                new { name = "locationNo", type = "string", indexed = true, stored = true },
                new { name = "locationName", type = "text_en", indexed = true, stored = true },
                new { name = "locationArea", type = "text_en", indexed = true, stored = true },
                new { name = "locationBuilding", type = "text_en", indexed = true, stored = true },
                new { name = "locationNotes", type = "text_en", indexed = true, stored = true },
                
                // Unit fields
                new { name = "unitNo", type = "string", indexed = true, stored = true },
                new { name = "unitName", type = "text_en", indexed = true, stored = true }
            };

            // Add field types
            //foreach (var fieldType in fieldTypes)
            foreach (var fieldTypeDefinition in fieldTypes)
            {
                try
                {
                    //var addFieldTypeJson = JsonSerializer.Serialize(new { @addFieldType = fieldType });
                    //var response = await _httpClient.PostAsync(
                    //    $"{_solrUrl}/{_coreName}/schema",
                    //    new StringContent(addFieldTypeJson, Encoding.UTF8, "application/json")
                    //);
                    // NYT
                    var commandPayload = new Dictionary<string, object>
                    {
                        ["add-field-type"] = fieldTypeDefinition
                    };
                    var commandJson = JsonSerializer.Serialize(commandPayload);

                    var response = await _httpClient.PostAsync(
                        $"{_solrUrl}/{_coreName}/schema",
                        new StringContent(commandJson, Encoding.UTF8, "application/json")
                    );
                    // NYT


                    // It's OK if field type already exists
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        if (!error.Contains("already exists"))
                        {
                            //Console.WriteLine($"Warning: Could not add field type {fieldType.name}: {error}");
                            Console.WriteLine($"Warning: Could not add field type {fieldTypeDefinition.name}: {error}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error adding field type: {ex.Message}");
                }
            }

            // Add fields
            //foreach (var field in fields)
            foreach (var fieldDefinition in fields)
            {
                try
                {
                    //var addFieldJson = JsonSerializer.Serialize(new { @addField = field });
                    //var response = await _httpClient.PostAsync(
                    //    $"{_solrUrl}/{_coreName}/schema",
                    //    new StringContent(addFieldJson, Encoding.UTF8, "application/json")
                    //);
                    // NYT
                    var commandPayload = new Dictionary<string, object>
                    {
                        ["add-field"] = fieldDefinition
                    };
                    var commandJson = JsonSerializer.Serialize(commandPayload);

                    var response = await _httpClient.PostAsync(
                        $"{_solrUrl}/{_coreName}/schema",
                        new StringContent(commandJson, Encoding.UTF8, "application/json")
                    );
                    // NYT

                    // It's OK if field already exists
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        if (!error.Contains("already exists"))
                        {
                            //Console.WriteLine($"Warning: Could not add field {field.name}: {error}");
                            Console.WriteLine($"Warning: Could not add field {fieldDefinition.name}: {error}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Warning: Error adding field {field.name}: {ex.Message}");
                    Console.WriteLine($"Warning: Error adding field {fieldDefinition.name}: {ex.Message}");
                }
            }

            // Add copy fields for full-text search
            var copyFields = new[]
            {
                new { source = "sparePartNo", dest = "_text_" },
                new { source = "sparePartSerialCode", dest = "_text_" },
                new { source = "name", dest = "_text_" },
                new { source = "description", dest = "_text_" },
                new { source = "typeNo", dest = "_text_" },
                new { source = "notes", dest = "_text_" },
                new { source = "manufacturerNo", dest = "_text_" },
                new { source = "manufacturerName", dest = "_text_" },
                new { source = "categoryName", dest = "_text_" },
                new { source = "supplierName", dest = "_text_" },
                new { source = "locationName", dest = "_text_" },
                new { source = "unitName", dest = "_text_" }
            };

            //foreach (var copyField in copyFields)
            foreach (var copyFieldDefinition in copyFields)
            {
                try
                {
                    //var addCopyFieldJson = JsonSerializer.Serialize(new { @addCopyField = copyField });
                    //var response = await _httpClient.PostAsync(
                    //    $"{_solrUrl}/{_coreName}/schema",
                    //    new StringContent(addCopyFieldJson, Encoding.UTF8, "application/json")
                    //);
                    // NYT
                    var commandPayload = new Dictionary<string, object>
                    {
                        ["add-copy-field"] = copyFieldDefinition
                    };
                    var commandJson = JsonSerializer.Serialize(commandPayload);

                    var response = await _httpClient.PostAsync(
                        $"{_solrUrl}/{_coreName}/schema",
                        new StringContent(commandJson, Encoding.UTF8, "application/json")
                    );
                    // NYT

                    // It's OK if copy field already exists
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        if (!error.Contains("already exists"))
                        {
                            //Console.WriteLine($"Warning: Could not add copy field {copyField.source} -> {copyField.dest}: {error}");
                            Console.WriteLine($"Warning: Could not add copy field {copyFieldDefinition.source} -> {copyFieldDefinition.dest}: {error}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error adding copy field: {ex.Message}");
                }
            }

            Console.WriteLine("Schema update completed.");
        }

        public async Task IndexDataAsync(int batchSize = 10000)
        {
            Console.WriteLine("Starting data indexing (optimized for speed)...");

            int totalIndexed = 0;
            int currentBatch = 0;

            var totalTimer = new Stopwatch();
            var batchTimer = new Stopwatch();

            totalTimer.Start();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT
                        SP.Id,
                        SP.SparePartNo,
                        SP.SparePartSerialCode,
                        SP.Name,
                        SP.Description,
                        SP.TypeNo,
                        SP.Notes,
                        SP.Currency,
                        M.ManufacturerNo,
                        M.Name AS ManufacturerName,
                        M.Notes AS ManufacturerNotes,
                        C.CategoryNo,
                        C.Name AS CategoryName,
                        C.Description AS CategoryDescription,
                        S.SupplierNo,
                        S.Name AS SupplierName,
                        S.ContactInfo AS SupplierContactInfo,
                        S.Notes AS SupplierNotes,
                        L.LocationNo,
                        L.Name AS LocationName,
                        L.Area AS LocationArea,
                        L.Building AS LocationBuilding,
                        L.Notes AS LocationNotes,
                        U.UnitNo,
                        U.Name AS UnitName
                    FROM
                        SparePart AS SP WITH (NOLOCK)
                        LEFT OUTER JOIN Manufacturer AS M WITH (NOLOCK) ON SP.ManufacturerGuid = M.Id
                        LEFT OUTER JOIN Category AS C WITH (NOLOCK) ON SP.CategoryGuid = C.Id
                        LEFT OUTER JOIN Supplier AS S WITH (NOLOCK) ON SP.SupplierGuid = S.Id
                        LEFT OUTER JOIN Location AS L WITH (NOLOCK) ON SP.LocationGuid = L.Id
                        LEFT OUTER JOIN Unit AS U WITH (NOLOCK) ON SP.UnitGuid = U.Id
                    WHERE
                        SP.master_id IS NULL;";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var documents = new List<SolrDocument>();

                        while (await reader.ReadAsync())
                        {
                            var doc = new SolrDocument
                            {
                                id = reader["Id"].ToString(),
                                sparePartNo = reader["SparePartNo"].ToString(),
                                sparePartSerialCode = reader["SparePartSerialCode"].ToString(),
                                name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : null,
                                description = reader["Description"] != DBNull.Value ? reader["Description"].ToString() : null,
                                typeNo = reader["TypeNo"] != DBNull.Value ? reader["TypeNo"].ToString() : null,
                                notes = reader["Notes"] != DBNull.Value ? reader["Notes"].ToString() : null,
                                currency = reader["Currency"] != DBNull.Value ? reader["Currency"].ToString() : null,
                                manufacturerNo = reader["ManufacturerNo"] != DBNull.Value ? reader["ManufacturerNo"].ToString() : null,
                                manufacturerName = reader["ManufacturerName"] != DBNull.Value ? reader["ManufacturerName"].ToString() : null,
                                manufacturerNotes = reader["ManufacturerNotes"] != DBNull.Value ? reader["ManufacturerNotes"].ToString() : null,
                                categoryNo = reader["CategoryNo"] != DBNull.Value ? reader["CategoryNo"].ToString() : null,
                                categoryName = reader["CategoryName"] != DBNull.Value ? reader["CategoryName"].ToString() : null,
                                categoryDescription = reader["CategoryDescription"] != DBNull.Value ? reader["CategoryDescription"].ToString() : null,
                                supplierNo = reader["SupplierNo"] != DBNull.Value ? reader["SupplierNo"].ToString() : null,
                                supplierName = reader["SupplierName"] != DBNull.Value ? reader["SupplierName"].ToString() : null,
                                supplierContactInfo = reader["SupplierContactInfo"] != DBNull.Value ? reader["SupplierContactInfo"].ToString() : null,
                                supplierNotes = reader["SupplierNotes"] != DBNull.Value ? reader["SupplierNotes"].ToString() : null,
                                locationNo = reader["LocationNo"] != DBNull.Value ? reader["LocationNo"].ToString() : null,
                                locationName = reader["LocationName"] != DBNull.Value ? reader["LocationName"].ToString() : null,
                                locationArea = reader["LocationArea"] != DBNull.Value ? reader["LocationArea"].ToString() : null,
                                locationBuilding = reader["LocationBuilding"] != DBNull.Value ? reader["LocationBuilding"].ToString() : null,
                                locationNotes = reader["LocationNotes"] != DBNull.Value ? reader["LocationNotes"].ToString() : null,
                                unitNo = reader["UnitNo"] != DBNull.Value ? reader["UnitNo"].ToString() : null,
                                unitName = reader["UnitName"] != DBNull.Value ? reader["UnitName"].ToString() : null
                            };

                            documents.Add(doc);

                            if (documents.Count >= batchSize)
                            {
                                currentBatch++;
                                batchTimer.Restart();

                                await IndexBatchAsync(documents, currentBatch);

                                totalIndexed += documents.Count;

                                double elapsedSeconds = batchTimer.Elapsed.TotalSeconds;
                                double docsPerSecond = documents.Count / elapsedSeconds;
                                double avgDocsPerSecond = totalIndexed / totalTimer.Elapsed.TotalSeconds;

                                Console.WriteLine($"Indexed batch {currentBatch} with {documents.Count} documents in {elapsedSeconds:F2}s ({docsPerSecond:F2} docs/sec)");
                                Console.WriteLine($"Total indexed: {totalIndexed} documents, average speed: {avgDocsPerSecond:F2} docs/sec");

                                documents.Clear();

                                // Commit periodically for better performance
                                if (totalIndexed % _commitInterval == 0)
                                {
                                    await CommitAsync();
                                    Console.WriteLine("Intermediate commit completed.");
                                }
                            }
                        }

                        // Index remaining documents
                        if (documents.Count > 0)
                        {
                            currentBatch++;
                            batchTimer.Restart();

                            await IndexBatchAsync(documents, currentBatch);

                            totalIndexed += documents.Count;

                            double elapsedSeconds = batchTimer.Elapsed.TotalSeconds;
                            double docsPerSecond = documents.Count / elapsedSeconds;
                            double avgDocsPerSecond = totalIndexed / totalTimer.Elapsed.TotalSeconds;

                            Console.WriteLine($"Indexed final batch {currentBatch} with {documents.Count} documents in {elapsedSeconds:F2}s ({docsPerSecond:F2} docs/sec)");
                            Console.WriteLine($"Total indexed: {totalIndexed} documents, average speed: {avgDocsPerSecond:F2} docs/sec");
                        }
                    }
                }

                totalTimer.Stop();
                Console.WriteLine($"Completed indexing with {totalIndexed} documents in {currentBatch} batches.");
                Console.WriteLine($"Total time: {totalTimer.Elapsed.TotalMinutes:F2} minutes");
                Console.WriteLine($"Average speed: {totalIndexed / totalTimer.Elapsed.TotalSeconds:F2} docs/sec");

                // Final commit and optimize
                Console.WriteLine("Performing final commit and optimization...");
                await CommitAsync();
                await OptimizeAsync();
                Console.WriteLine("Index is now ready for search.");
            }
        }

        public async Task IndexDataParallelAsync(int batchSize = 10000)
        {
            Console.WriteLine("Starting parallel data indexing...");

            var totalTimer = new Stopwatch();
            totalTimer.Start();

            int totalProcessed = 0;

            try
            {
                int degreeOfParallelism = 8;

                // Get total row count
                int totalRows;
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var countCmd = new SqlCommand("SELECT COUNT(*) FROM SparePart WITH (NOLOCK) WHERE master_id IS NULL", connection);
                    totalRows = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }

                int rowsPerThread = (int)Math.Ceiling(totalRows / (float)degreeOfParallelism);
                var tasks = new List<Task<int>>();

                Console.WriteLine($"Processing {totalRows} rows with {degreeOfParallelism} threads");
                Console.WriteLine($"Each thread will process approximately {rowsPerThread} rows in batches of {batchSize}");

                for (int thread = 0; thread < degreeOfParallelism; thread++)
                {
                    int threadId = thread;
                    int startRow = thread * rowsPerThread;
                    int endRow = Math.Min((thread + 1) * rowsPerThread, totalRows);

                    tasks.Add(Task.Run(() => ProcessRowRangeAsync(threadId, startRow, endRow, batchSize)));
                }

                var results = await Task.WhenAll(tasks);
                totalProcessed = results.Sum();

                Console.WriteLine($"Completed indexing with {totalProcessed} documents.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during parallel indexing: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            totalTimer.Stop();
            Console.WriteLine($"Total time: {totalTimer.Elapsed.TotalMinutes:F2} minutes");
            Console.WriteLine($"Average speed: {totalProcessed / totalTimer.Elapsed.TotalSeconds:F2} docs/sec");

            // Final commit and optimize
            Console.WriteLine("Performing final commit and optimization...");
            await CommitAsync();
            await OptimizeAsync();
            Console.WriteLine("Index is now ready for search.");
        }

        private async Task<int> ProcessRowRangeAsync(int threadId, int startRow, int endRow, int batchSize)
        {
            int processed = 0;
            int batchNumber = 0;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                for (int offset = startRow; offset < endRow; offset += batchSize)
                {
                    var batchTimer = new Stopwatch();
                    batchTimer.Start();

                    var documents = new List<SolrDocument>();
                    int actualBatchSize = Math.Min(batchSize, endRow - offset);

                    var query = @"
                        SELECT 
                            SP.Id,
                            SP.SparePartNo,
                            SP.SparePartSerialCode,
                            SP.Name,
                            SP.Description,
                            SP.TypeNo,
                            SP.Notes,
                            SP.Currency,
                            M.ManufacturerNo,
                            M.Name AS ManufacturerName,
                            M.Notes AS ManufacturerNotes,
                            C.CategoryNo,
                            C.Name AS CategoryName,
                            C.Description AS CategoryDescription,
                            S.SupplierNo,
                            S.Name AS SupplierName,
                            S.ContactInfo AS SupplierContactInfo,
                            S.Notes AS SupplierNotes,
                            L.LocationNo,
                            L.Name AS LocationName,
                            L.Area AS LocationArea,
                            L.Building AS LocationBuilding,
                            L.Notes AS LocationNotes,
                            U.UnitNo,
                            U.Name AS UnitName
                        FROM 
                            SparePart AS SP WITH (NOLOCK)
                            LEFT OUTER JOIN Manufacturer AS M WITH (NOLOCK) ON SP.ManufacturerGuid = M.Id
                            LEFT OUTER JOIN Category AS C WITH (NOLOCK) ON SP.CategoryGuid = C.Id
                            LEFT OUTER JOIN Supplier AS S WITH (NOLOCK) ON SP.SupplierGuid = S.Id
                            LEFT OUTER JOIN Location AS L WITH (NOLOCK) ON SP.LocationGuid = L.Id
                            LEFT OUTER JOIN Unit AS U WITH (NOLOCK) ON SP.UnitGuid = U.Id
                        WHERE
                            SP.master_id IS NULL
                        ORDER BY 
                            SP.Id
                        OFFSET @Offset ROWS
                        FETCH NEXT @BatchSize ROWS ONLY";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@BatchSize", actualBatchSize);
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.CommandTimeout = 300;

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var doc = new SolrDocument
                                {
                                    id = reader["Id"].ToString(),
                                    sparePartNo = reader["SparePartNo"].ToString(),
                                    sparePartSerialCode = reader["SparePartSerialCode"].ToString(),
                                    name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : null,
                                    description = reader["Description"] != DBNull.Value ? reader["Description"].ToString() : null,
                                    typeNo = reader["TypeNo"] != DBNull.Value ? reader["TypeNo"].ToString() : null,
                                    notes = reader["Notes"] != DBNull.Value ? reader["Notes"].ToString() : null,
                                    currency = reader["Currency"] != DBNull.Value ? reader["Currency"].ToString() : null,
                                    manufacturerNo = reader["ManufacturerNo"] != DBNull.Value ? reader["ManufacturerNo"].ToString() : null,
                                    manufacturerName = reader["ManufacturerName"] != DBNull.Value ? reader["ManufacturerName"].ToString() : null,
                                    manufacturerNotes = reader["ManufacturerNotes"] != DBNull.Value ? reader["ManufacturerNotes"].ToString() : null,
                                    categoryNo = reader["CategoryNo"] != DBNull.Value ? reader["CategoryNo"].ToString() : null,
                                    categoryName = reader["CategoryName"] != DBNull.Value ? reader["CategoryName"].ToString() : null,
                                    categoryDescription = reader["CategoryDescription"] != DBNull.Value ? reader["CategoryDescription"].ToString() : null,
                                    supplierNo = reader["SupplierNo"] != DBNull.Value ? reader["SupplierNo"].ToString() : null,
                                    supplierName = reader["SupplierName"] != DBNull.Value ? reader["SupplierName"].ToString() : null,
                                    supplierContactInfo = reader["SupplierContactInfo"] != DBNull.Value ? reader["SupplierContactInfo"].ToString() : null,
                                    supplierNotes = reader["SupplierNotes"] != DBNull.Value ? reader["SupplierNotes"].ToString() : null,
                                    locationNo = reader["LocationNo"] != DBNull.Value ? reader["LocationNo"].ToString() : null,
                                    locationName = reader["LocationName"] != DBNull.Value ? reader["LocationName"].ToString() : null,
                                    locationArea = reader["LocationArea"] != DBNull.Value ? reader["LocationArea"].ToString() : null,
                                    locationBuilding = reader["LocationBuilding"] != DBNull.Value ? reader["LocationBuilding"].ToString() : null,
                                    locationNotes = reader["LocationNotes"] != DBNull.Value ? reader["LocationNotes"].ToString() : null,
                                    unitNo = reader["UnitNo"] != DBNull.Value ? reader["UnitNo"].ToString() : null,
                                    unitName = reader["UnitName"] != DBNull.Value ? reader["UnitName"].ToString() : null
                                };

                                documents.Add(doc);
                            }
                        }
                    }

                    if (documents.Count > 0)
                    {
                        try
                        {
                            await IndexBatchAsync(documents, batchNumber, threadId);
                            processed += documents.Count;
                            batchNumber++;

                            double elapsedSeconds = batchTimer.Elapsed.TotalSeconds;
                            double docsPerSecond = documents.Count / elapsedSeconds;

                            Console.WriteLine($"Thread {threadId}: Indexed batch {batchNumber} with {documents.Count} documents in {elapsedSeconds:F2}s ({docsPerSecond:F2} docs/sec)");
                            Console.WriteLine($"Thread {threadId}: Total processed: {processed}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception in thread {threadId}, batch {batchNumber}: {ex.Message}");
                        }
                    }
                }
            }

            return processed;
        }

        private async Task IndexBatchAsync(List<SolrDocument> documents, int batchNumber, int? threadId = null)
        {
            try
            {
                var json = JsonSerializer.Serialize(documents);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_solrUrl}/{_coreName}/update/json/docs", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    var prefix = threadId.HasValue ? $"Thread {threadId}" : "Main";
                    Console.WriteLine($"{prefix}: Error in batch {batchNumber}: {error}");
                }
            }
            catch (Exception ex)
            {
                var prefix = threadId.HasValue ? $"Thread {threadId}" : "Main";
                Console.WriteLine($"{prefix}: Exception in batch {batchNumber}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private async Task CommitAsync()
        {
            var response = await _httpClient.GetAsync($"{_solrUrl}/{_coreName}/update?commit=true");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Warning: Commit failed: {error}");
            }
        }

        private async Task OptimizeAsync()
        {
            var response = await _httpClient.GetAsync($"{_solrUrl}/{_coreName}/update?optimize=true");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Warning: Optimize failed: {error}");
            }
        }
    }

    // Solr document model
    public class SolrDocument
    {
        public string id { get; set; }
        public string sparePartNo { get; set; }
        public string sparePartSerialCode { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string typeNo { get; set; }
        public string notes { get; set; }
        public string currency { get; set; }

        // Manufacturer info
        public string manufacturerNo { get; set; }
        public string manufacturerName { get; set; }
        public string manufacturerNotes { get; set; }

        // Category info
        public string categoryNo { get; set; }
        public string categoryName { get; set; }
        public string categoryDescription { get; set; }

        // Supplier info
        public string supplierNo { get; set; }
        public string supplierName { get; set; }
        public string supplierContactInfo { get; set; }
        public string supplierNotes { get; set; }

        // Location info
        public string locationNo { get; set; }
        public string locationName { get; set; }
        public string locationArea { get; set; }
        public string locationBuilding { get; set; }
        public string locationNotes { get; set; }

        // Unit info
        public string unitNo { get; set; }
        public string unitName { get; set; }
    }
}
