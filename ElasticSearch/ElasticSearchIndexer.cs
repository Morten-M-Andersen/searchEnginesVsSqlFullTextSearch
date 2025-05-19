using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Data;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Transport;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch.IndexManagement;

namespace ElasticSearch
{
    public class ElasticSearchIndexer
    {
        private readonly string _connectionString;
        private readonly string _elasticUrl;
        private readonly string _indexName;
        private readonly ElasticsearchClient _client;

        public ElasticSearchIndexer(string sqlConnectionString, string elasticUrl = "https://localhost:9200", string indexName = "spareparts")
        {
            _connectionString = sqlConnectionString;
            _elasticUrl = elasticUrl;
            _indexName = indexName;

            var settings = new ElasticsearchClientSettings(new Uri(elasticUrl))
                .DefaultIndex(indexName)
                .ServerCertificateValidationCallback((sender, certificate, chain, errors) => true);

            _client = new ElasticsearchClient(settings);
        }

        public async Task SetupIndexAsync()
        {
            // Tjek om indeks eksisterer, og slet det hvis det gør
            var indexExists = await _client.Indices.ExistsAsync(_indexName);
            if (indexExists.Exists)
            {
                var deleteResponse = await _client.Indices.DeleteAsync(_indexName);
                if (!deleteResponse.IsValidResponse)
                {
                    throw new Exception($"Failed to delete existing index: {deleteResponse.DebugInformation}");
                }
                Console.WriteLine($"Existing index '{_indexName}' deleted.");
            }

            // Opret indeks med mapping
            var createIndexResponse = await _client.Indices.CreateAsync<SparePartDocument>(_indexName, c => c
                .Settings(s => s
                    .Analysis(a => a
                        .Analyzers(an => an
                            .Custom("english_analyzer", ca => ca
                                .Tokenizer("standard")
                                .Filter("lowercase", "english_stop", "english_stemmer")
                            )
                        )
                        .TokenFilters(tf => tf
                            .Stop("english_stop", st => st
                                .Stopwords("_english_")
                            )
                            .Stemmer("english_stemmer", st => st
                                .Language("english")
                            )
                        )
                    )
                    .AddOtherSetting("index.max_ngram_diff", 10)
                    .AddOtherSetting("index.mapping.total_fields.limit", 2000)
                )
                .Mappings(m => m
                    .Properties(p => p
                        // Basisfelter
                        .Keyword(f => f.Id)
                        //.Keyword(f => f.UnitGuid)
                        .Text(f => f.SparePartNo, t => t
                            .Analyzer("english_analyzer")
                            .Fields(ff => ff
                                .Keyword("keyword")
                            )
                        )
                        .Text(f => f.SparePartSerialCode, t => t
                            .Analyzer("english_analyzer")
                            .Fields(ff => ff
                                .Keyword("keyword")
                            )
                        )
                        .Text(f => f.Name, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.Description, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.TypeNo, t => t
                            .Analyzer("english_analyzer")
                            .Fields(ff => ff
                                .Keyword("keyword")
                            )
                        )
                        .Text(f => f.Notes, t => t.Analyzer("english_analyzer"))

                        // Manufacturer properties
                        .Text(f => f.ManufacturerNo, t => t
                            .Analyzer("english_analyzer")
                            .Fields(ff => ff
                                .Keyword("keyword")
                            )
                        )
                        .Text(f => f.ManufacturerName, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.ManufacturerNotes, t => t.Analyzer("english_analyzer"))

                        // Category properties
                        .Text(f => f.CategoryNo, t => t
                            .Analyzer("english_analyzer")
                            .Fields(ff => ff
                                .Keyword("keyword")
                            )
                        )
                        .Text(f => f.CategoryName, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.CategoryDescription, t => t.Analyzer("english_analyzer"))

                        // Supplier properties
                        .Text(f => f.SupplierNo, t => t
                            .Analyzer("english_analyzer")
                            .Fields(ff => ff
                                .Keyword("keyword")
                            )
                        )
                        .Text(f => f.SupplierName, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.SupplierContactInfo, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.SupplierNotes, t => t.Analyzer("english_analyzer"))

                        // Location properties
                        .Text(f => f.LocationNo, t => t
                            .Analyzer("english_analyzer")
                            .Fields(ff => ff
                                .Keyword("keyword")
                            )
                        )
                        .Text(f => f.LocationName, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.LocationArea, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.LocationBuilding, t => t.Analyzer("english_analyzer"))
                        .Text(f => f.LocationNotes, t => t.Analyzer("english_analyzer"))

                        // Unit properties
                        .Text(f => f.UnitNo, t => t
                            .Analyzer("english_analyzer")
                            .Fields(ff => ff
                                .Keyword("keyword")
                            )
                        )
                        .Text(f => f.UnitName, t => t.Analyzer("english_analyzer"))

                        // Additional properties
                        //.DoubleNumber(f => f.BasePrice)
                        .Keyword(f => f.Currency)

                    // IDs felter for relaterede entiteter
                    //.Keyword(f => f.ManufacturerId)
                    //.Keyword(f => f.CategoryId)
                    //.Keyword(f => f.SupplierId)
                    //.Keyword(f => f.LocationId)
                    )
                )
            );

            if (!createIndexResponse.IsValidResponse)
            {
                throw new Exception($"Failed to create index: {createIndexResponse.DebugInformation}");
            }

            Console.WriteLine($"Index '{_indexName}' created successfully.");
        }

        //public async Task IndexDataAsync(int batchSize = 10000)
        //{
        //    Console.WriteLine("Starting data indexing...");

        //    // Optimer Elasticsearch for bulk indlæsning
        //    await OptimizeForBulkLoadingAsync();

        //    int totalIndexed = 0;
        //    int currentBatch = 0;

        //    // Opret timere til at måle hastighed
        //    var totalTimer = new System.Diagnostics.Stopwatch();
        //    var batchTimer = new System.Diagnostics.Stopwatch();
        //    long lastCount = 0;

        //    totalTimer.Start();

        //    using (var connection = new SqlConnection(_connectionString))
        //    {
        //        await connection.OpenAsync();

        //        int offset = 0;
        //        bool moreData = true;

        //        while (moreData)
        //        {
        //            batchTimer.Restart();
        //            var documents = new List<SparePartDocument>();

        //            var query = @"
        //                        SELECT 
        //                            SP.Id,

        //                            SP.SparePartNo,
        //                            SP.SparePartSerialCode,
        //                            SP.Name,
        //                            SP.Description,
        //                            SP.TypeNo,
        //                            SP.Notes,
        //                            SP.Currency,

        //                            M.ManufacturerNo,
        //                            M.Name AS ManufacturerName,
        //                            M.Notes AS ManufacturerNotes,

        //                            C.CategoryNo,
        //                            C.Name AS CategoryName,
        //                            C.Description AS CategoryDescription,

        //                            S.SupplierNo,
        //                            S.Name AS SupplierName,
        //                            S.ContactInfo AS SupplierContactInfo,
        //                            S.Notes AS SupplierNotes,

        //                            L.LocationNo,
        //                            L.Name AS LocationName,
        //                            L.Area AS LocationArea,
        //                            L.Building AS LocationBuilding,
        //                            L.Notes AS LocationNotes,

        //                            U.UnitNo,
        //                            U.Name AS UnitName
        //                        FROM 
        //                            SparePart AS SP WITH (NOLOCK)
        //                            LEFT OUTER JOIN Manufacturer AS M WITH (NOLOCK) ON SP.ManufacturerGuid = M.Id
        //                            LEFT OUTER JOIN Category AS C WITH (NOLOCK) ON SP.CategoryGuid = C.Id
        //                            LEFT OUTER JOIN Supplier AS S WITH (NOLOCK) ON SP.SupplierGuid = S.Id
        //                            LEFT OUTER JOIN Location AS L WITH (NOLOCK) ON SP.LocationGuid = L.Id
        //                            LEFT OUTER JOIN Unit AS U WITH (NOLOCK) ON SP.UnitGuid = U.Id
        //                        WHERE
        //                            SP.master_id IS NULL
        //                        ORDER BY 
        //                            SP.Id
        //                        OFFSET @Offset ROWS
        //                        FETCH NEXT @BatchSize ROWS ONLY
        //                        ";

        //            using (var command = new SqlCommand(query, connection))
        //            {
        //                command.Parameters.AddWithValue("@BatchSize", batchSize);
        //                command.Parameters.AddWithValue("@Offset", offset);
        //                command.CommandTimeout = 300; // 5 minutter timeout

        //                using (var reader = await command.ExecuteReaderAsync())
        //                {
        //                    int rowCount = 0;

        //                    while (await reader.ReadAsync())
        //                    {
        //                        rowCount++;

        //                        var doc = new SparePartDocument
        //                        {
        //                            Id = reader["Id"].ToString(),
        //                            SparePartNo = reader["SparePartNo"].ToString(),
        //                            SparePartSerialCode = reader["SparePartSerialCode"].ToString(),
        //                            Name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : null,
        //                            Description = reader["Description"] != DBNull.Value ? reader["Description"].ToString() : null,
        //                            TypeNo = reader["TypeNo"] != DBNull.Value ? reader["TypeNo"].ToString() : null,
        //                            Notes = reader["Notes"] != DBNull.Value ? reader["Notes"].ToString() : null,
        //                            Currency = reader["Currency"] != DBNull.Value ? reader["Currency"].ToString() : null,

        //                            // Manufacturer info
        //                            ManufacturerNo = reader["ManufacturerNo"] != DBNull.Value ? reader["ManufacturerNo"].ToString() : null,
        //                            ManufacturerName = reader["ManufacturerName"] != DBNull.Value ? reader["ManufacturerName"].ToString() : null,
        //                            ManufacturerNotes = reader["ManufacturerNotes"] != DBNull.Value ? reader["ManufacturerNotes"].ToString() : null,

        //                            // Category info
        //                            CategoryNo = reader["CategoryNo"] != DBNull.Value ? reader["CategoryNo"].ToString() : null,
        //                            CategoryName = reader["CategoryName"] != DBNull.Value ? reader["CategoryName"].ToString() : null,
        //                            CategoryDescription = reader["CategoryDescription"] != DBNull.Value ? reader["CategoryDescription"].ToString() : null,

        //                            // Supplier info
        //                            SupplierNo = reader["SupplierNo"] != DBNull.Value ? reader["SupplierNo"].ToString() : null,
        //                            SupplierName = reader["SupplierName"] != DBNull.Value ? reader["SupplierName"].ToString() : null,
        //                            SupplierContactInfo = reader["SupplierContactInfo"] != DBNull.Value ? reader["SupplierContactInfo"].ToString() : null,
        //                            SupplierNotes = reader["SupplierNotes"] != DBNull.Value ? reader["SupplierNotes"].ToString() : null,

        //                            // Location info
        //                            LocationNo = reader["LocationNo"] != DBNull.Value ? reader["LocationNo"].ToString() : null,
        //                            LocationName = reader["LocationName"] != DBNull.Value ? reader["LocationName"].ToString() : null,
        //                            LocationArea = reader["LocationArea"] != DBNull.Value ? reader["LocationArea"].ToString() : null,
        //                            LocationBuilding = reader["LocationBuilding"] != DBNull.Value ? reader["LocationBuilding"].ToString() : null,
        //                            LocationNotes = reader["LocationNotes"] != DBNull.Value ? reader["LocationNotes"].ToString() : null,

        //                            // Unit info
        //                            UnitNo = reader["UnitNo"] != DBNull.Value ? reader["UnitNo"].ToString() : null,
        //                            UnitName = reader["UnitName"] != DBNull.Value ? reader["UnitName"].ToString() : null
        //                        };

        //                        //// Concatenate all text fields for the AllText field
        //                        //var allTextParts = new List<string>
        //                        //{
        //                        //    doc.SparePartNo,
        //                        //    doc.SparePartSerialCode,
        //                        //    doc.Name,
        //                        //    doc.Description,
        //                        //    doc.TypeNo,
        //                        //    doc.Notes,
        //                        //    doc.ManufacturerNo,
        //                        //    doc.ManufacturerName,
        //                        //    doc.ManufacturerNotes,
        //                        //    doc.CategoryNo,
        //                        //    doc.CategoryName,
        //                        //    doc.CategoryDescription,
        //                        //    doc.SupplierNo,
        //                        //    doc.SupplierName,
        //                        //    doc.SupplierContactInfo,
        //                        //    doc.SupplierNotes,
        //                        //    doc.LocationNo,
        //                        //    doc.LocationName,
        //                        //    doc.LocationArea,
        //                        //    doc.LocationBuilding,
        //                        //    doc.LocationNotes,
        //                        //    doc.UnitNo,
        //                        //    doc.UnitName
        //                        //};

        //                        //doc.AllText = string.Join(" ", allTextParts.Where(p => !string.IsNullOrEmpty(p)));

        //                        documents.Add(doc);
        //                    }

        //                    moreData = rowCount > 0;
        //                }
        //            }

        //            if (documents.Count > 0)
        //            {
        //                currentBatch++;
        //                try
        //                {
        //                    // Opret en BulkRequest med operationer
        //                    var bulkRequest = new BulkRequest
        //                    {
        //                        // Initialiserer Operations-listen eksplicit
        //                        Operations = new List<IBulkOperation>()
        //                    };

        //                    foreach (var doc in documents)
        //                    {
        //                        // Tilføj en BulkIndexOperation for hvert dokument
        //                        bulkRequest.Operations.Add(new BulkIndexOperation<SparePartDocument>(doc)
        //                        {
        //                            Id = doc.Id,
        //                            Index = _indexName
        //                        });
        //                    }

        //                    var bulkResponse = await _client.BulkAsync(bulkRequest);

        //                    if (!bulkResponse.IsValidResponse)
        //                    {
        //                        Console.WriteLine($"Error in batch {currentBatch}: {bulkResponse.DebugInformation}");

        //                        // Få mere detaljeret fejlinformation
        //                        if (bulkResponse.Errors)
        //                        {
        //                            foreach (var item in bulkResponse.Items)
        //                            {
        //                                if (item.Error != null)
        //                                {
        //                                    Console.WriteLine($"Error for document {item.Id}: {item.Error.Reason}");
        //                                }
        //                            }
        //                        }
        //                    }
        //                    else
        //                    {
        //                        totalIndexed += documents.Count;

        //                        // Beregn hastigheder
        //                        double elapsedSeconds = batchTimer.Elapsed.TotalSeconds;
        //                        double docsPerSecond = documents.Count / elapsedSeconds;
        //                        double avgDocsPerSecond = totalIndexed / totalTimer.Elapsed.TotalSeconds;

        //                        Console.WriteLine($"Indexed batch {currentBatch} with {documents.Count} documents in {elapsedSeconds:F2}s ({docsPerSecond:F2} docs/sec)");
        //                        Console.WriteLine($"Total indexed: {totalIndexed} documents, average speed: {avgDocsPerSecond:F2} docs/sec");
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    Console.WriteLine($"Exception in batch {currentBatch}: {ex.Message}");
        //                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");

        //                    if (ex.InnerException != null)
        //                    {
        //                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        //                    }
        //                }

        //                offset += batchSize;
        //            }
        //        }
        //    }

        //    totalTimer.Stop();
        //    Console.WriteLine($"Completed indexing with {totalIndexed} documents in {currentBatch} batches.");
        //    Console.WriteLine($"Total time: {totalTimer.Elapsed.TotalMinutes:F2} minutes");
        //    Console.WriteLine($"Average speed: {totalIndexed / totalTimer.Elapsed.TotalSeconds:F2} docs/sec");

        //    // Gendan normale indstillinger og refresh indekset
        //    await RestoreNormalSettingsAsync();

        //    // Refreshing index to make sure all changes are available for search
        //    Console.WriteLine("Final refresh of index...");
        //    await _client.Indices.RefreshAsync(_indexName);
        //    Console.WriteLine("Refresh complete. Index is now ready for search.");
        //}

        public async Task IndexDataAsync(int batchSize = 10000)
        {
            Console.WriteLine("Starting data indexing (fastest method - no order, no offset)...");

            // Optimer Elasticsearch for bulk indlæsning
            await OptimizeForBulkLoadingAsync();

            int totalIndexed = 0;
            int currentBatch = 0;

            // Opret timere til at måle hastighed
            var totalTimer = new System.Diagnostics.Stopwatch();
            var batchTimer = new System.Diagnostics.Stopwatch();

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
                    command.CommandTimeout = 300; // 5 minutter timeout

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var documents = new List<SparePartDocument>();

                        while (await reader.ReadAsync())
                        {
                            var doc = new SparePartDocument
                            {
                                Id = reader["Id"].ToString(),
                                SparePartNo = reader["SparePartNo"].ToString(),
                                SparePartSerialCode = reader["SparePartSerialCode"].ToString(),
                                Name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : null,
                                Description = reader["Description"] != DBNull.Value ? reader["Description"].ToString() : null,
                                TypeNo = reader["TypeNo"] != DBNull.Value ? reader["TypeNo"].ToString() : null,
                                Notes = reader["Notes"] != DBNull.Value ? reader["Notes"].ToString() : null,
                                Currency = reader["Currency"] != DBNull.Value ? reader["Currency"].ToString() : null,
                                ManufacturerNo = reader["ManufacturerNo"] != DBNull.Value ? reader["ManufacturerNo"].ToString() : null,
                                ManufacturerName = reader["ManufacturerName"] != DBNull.Value ? reader["ManufacturerName"].ToString() : null,
                                ManufacturerNotes = reader["ManufacturerNotes"] != DBNull.Value ? reader["ManufacturerNotes"].ToString() : null,
                                CategoryNo = reader["CategoryNo"] != DBNull.Value ? reader["CategoryNo"].ToString() : null,
                                CategoryName = reader["CategoryName"] != DBNull.Value ? reader["CategoryName"].ToString() : null,
                                CategoryDescription = reader["CategoryDescription"] != DBNull.Value ? reader["CategoryDescription"].ToString() : null,
                                SupplierNo = reader["SupplierNo"] != DBNull.Value ? reader["SupplierNo"].ToString() : null,
                                SupplierName = reader["SupplierName"] != DBNull.Value ? reader["SupplierName"].ToString() : null,
                                SupplierContactInfo = reader["SupplierContactInfo"] != DBNull.Value ? reader["SupplierContactInfo"].ToString() : null,
                                SupplierNotes = reader["SupplierNotes"] != DBNull.Value ? reader["SupplierNotes"].ToString() : null,
                                LocationNo = reader["LocationNo"] != DBNull.Value ? reader["LocationNo"].ToString() : null,
                                LocationName = reader["LocationName"] != DBNull.Value ? reader["LocationName"].ToString() : null,
                                LocationArea = reader["LocationArea"] != DBNull.Value ? reader["LocationArea"].ToString() : null,
                                LocationBuilding = reader["LocationBuilding"] != DBNull.Value ? reader["LocationBuilding"].ToString() : null,
                                LocationNotes = reader["LocationNotes"] != DBNull.Value ? reader["LocationNotes"].ToString() : null,
                                UnitNo = reader["UnitNo"] != DBNull.Value ? reader["UnitNo"].ToString() : null,
                                UnitName = reader["UnitName"] != DBNull.Value ? reader["UnitName"].ToString() : null
                            };

                            documents.Add(doc);

                            if (documents.Count >= batchSize)
                            {
                                currentBatch++;
                                try
                                {
                                    // Opret en BulkRequest med operationer
                                    var bulkRequest = new BulkRequest
                                    {
                                        // Initialiserer Operations-listen eksplicit
                                        Operations = new List<IBulkOperation>()
                                    };

                                    foreach (var d in documents)
                                    {
                                        // Tilføj en BulkIndexOperation for hvert dokument
                                        bulkRequest.Operations.Add(new BulkIndexOperation<SparePartDocument>(d)
                                        {
                                            Id = d.Id,
                                            Index = _indexName
                                        });
                                    }

                                    var bulkResponse = await _client.BulkAsync(bulkRequest);

                                    if (!bulkResponse.IsValidResponse)
                                    {
                                        Console.WriteLine($"Error in batch {currentBatch}: {bulkResponse.DebugInformation}");

                                        // Få mere detaljeret fejlinformation
                                        if (bulkResponse.Errors)
                                        {
                                            foreach (var item in bulkResponse.Items)
                                            {
                                                if (item.Error != null)
                                                {
                                                    Console.WriteLine($"Error for document {item.Id}: {item.Error.Reason}");
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        totalIndexed += documents.Count;

                                        // Beregn hastigheder
                                        double elapsedSeconds = batchTimer.Elapsed.TotalSeconds;
                                        double docsPerSecond = documents.Count / elapsedSeconds;
                                        double avgDocsPerSecond = totalIndexed / totalTimer.Elapsed.TotalSeconds;
                                        Console.WriteLine($"Indexed batch {currentBatch} with {documents.Count} documents in {elapsedSeconds:F2}s ({docsPerSecond:F2} docs/sec)");
                                        Console.WriteLine($"Total indexed: {totalIndexed} documents, average speed: {avgDocsPerSecond:F2} docs/sec");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Exception in batch {currentBatch}: {ex.Message}");
                                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                                    if (ex.InnerException != null)
                                    {
                                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                                    }
                                }
                                // **Slut på bulk-indekseringslogik**

                                documents.Clear();
                                batchTimer.Restart();
                            }
                        }

                        // Indsæt eventuelle resterende dokumenter
                        if (documents.Count > 0)
                        {
                            currentBatch++;
                            try
                            {
                                // Opret en BulkRequest med operationer
                                var bulkRequest = new BulkRequest
                                {
                                    // Initialiserer Operations-listen eksplicit
                                    Operations = new List<IBulkOperation>()
                                };

                                foreach (var d in documents)
                                {
                                    // Tilføj en BulkIndexOperation for hvert dokument
                                    bulkRequest.Operations.Add(new BulkIndexOperation<SparePartDocument>(d)
                                    {
                                        Id = d.Id,
                                        Index = _indexName
                                    });
                                }

                                var bulkResponse = await _client.BulkAsync(bulkRequest);

                                if (!bulkResponse.IsValidResponse)
                                {
                                    Console.WriteLine($"Error in final batch {currentBatch}: {bulkResponse.DebugInformation}");

                                    // Få mere detaljeret fejlinformation
                                    if (bulkResponse.Errors)
                                    {
                                        foreach (var item in bulkResponse.Items)
                                        {
                                            if (item.Error != null)
                                            {
                                                Console.WriteLine($"Error for document {item.Id}: {item.Error.Reason}");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    totalIndexed += documents.Count;

                                    // Beregn hastigheder
                                    double elapsedSeconds = batchTimer.Elapsed.TotalSeconds;
                                    double docsPerSecond = documents.Count / elapsedSeconds;
                                    double avgDocsPerSecond = totalIndexed / totalTimer.Elapsed.TotalSeconds;
                                    Console.WriteLine($"Indexed final batch {currentBatch} with {documents.Count} documents in {elapsedSeconds:F2}s ({docsPerSecond:F2} docs/sec)");
                                    Console.WriteLine($"Total indexed: {totalIndexed} documents, average speed: {avgDocsPerSecond:F2} docs/sec");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Exception in final batch {currentBatch}: {ex.Message}");
                                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                                if (ex.InnerException != null)
                                {
                                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                                }
                            }
                        }
                    }
                }

                totalTimer.Stop();
                Console.WriteLine($"Completed indexing with {totalIndexed} documents in {currentBatch} batches.");
                Console.WriteLine($"Total time: {totalTimer.Elapsed.TotalMinutes:F2} minutes");
                Console.WriteLine($"Average speed: {totalIndexed / totalTimer.Elapsed.TotalSeconds:F2} docs/sec");

                // Gendan normale indstillinger og refresh indekset
                await RestoreNormalSettingsAsync();

                // Refreshing index to make sure all changes are available for search
                Console.WriteLine("Final refresh of index...");
                await _client.Indices.RefreshAsync(_indexName);
                Console.WriteLine("Refresh complete. Index is now ready for search.");
            }
        }

        public async Task IndexDataParallelAsync(int batchSize = 10000)
        {
            Console.WriteLine("Starting parallel data indexing...");

            // Optimér Elasticsearch for bulk indlæsning
            await OptimizeForBulkLoadingAsync();

            var totalTimer = new System.Diagnostics.Stopwatch();
            totalTimer.Start();

            int totalProcessed = 0;

            try
            {
                // Brug 8 parallelle threads - godt kompromis mellem parallelisme og overbelastning
                int degreeOfParallelism = 8;

                // Få det samlede antal rækker for at beregne chunks
                int totalRows;
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var countCmd = new SqlCommand("SELECT COUNT(*) FROM SparePart WITH (NOLOCK)", connection);
                    totalRows = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }

                // Beregn hvor mange rækker hver tråd skal behandle
                int rowsPerThread = (int)Math.Ceiling(totalRows / (float)degreeOfParallelism);
                var tasks = new List<Task<int>>();

                Console.WriteLine($"Processing {totalRows} rows with {degreeOfParallelism} threads");
                Console.WriteLine($"Each thread will process approximately {rowsPerThread} rows in batches of {batchSize}");

                // Opret en task per tråd
                for (int thread = 0; thread < degreeOfParallelism; thread++)
                {
                    int threadId = thread;
                    int startRow = thread * rowsPerThread;
                    int endRow = Math.Min((thread + 1) * rowsPerThread, totalRows);

                    // Start task til at behandle en portion af dataene
                    tasks.Add(Task.Run(() => ProcessRowRangeAsync(threadId, startRow, endRow, batchSize)));
                }

                // Vent på alle tasks og summer resultaterne
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

            // Gendan normale indstillinger og refresh indekset
            await RestoreNormalSettingsAsync();

            // Refreshing index to make sure all changes are available for search
            Console.WriteLine("Final refresh of index...");
            await _client.Indices.RefreshAsync(_indexName);
            Console.WriteLine("Refresh complete. Index is now ready for search.");
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
                    var batchTimer = new System.Diagnostics.Stopwatch();
                    batchTimer.Start();

                    var documents = new List<SparePartDocument>();

                    // Beregn faktisk batchstørrelse (sidste batch kan være mindre)
                    int actualBatchSize = Math.Min(batchSize, endRow - offset);

                    // Hent en batch af dokumenter fra databasen
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
                                FETCH NEXT @BatchSize ROWS ONLY
                                ";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@BatchSize", actualBatchSize);
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.CommandTimeout = 300; // 5 minutter timeout

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var doc = new SparePartDocument
                                {
                                    Id = reader["Id"].ToString(),
                                    SparePartNo = reader["SparePartNo"].ToString(),
                                    SparePartSerialCode = reader["SparePartSerialCode"].ToString(),
                                    Name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : null,
                                    Description = reader["Description"] != DBNull.Value ? reader["Description"].ToString() : null,
                                    TypeNo = reader["TypeNo"] != DBNull.Value ? reader["TypeNo"].ToString() : null,
                                    Notes = reader["Notes"] != DBNull.Value ? reader["Notes"].ToString() : null,
                                    Currency = reader["Currency"] != DBNull.Value ? reader["Currency"].ToString() : null,

                                    // Manufacturer info
                                    ManufacturerNo = reader["ManufacturerNo"] != DBNull.Value ? reader["ManufacturerNo"].ToString() : null,
                                    ManufacturerName = reader["ManufacturerName"] != DBNull.Value ? reader["ManufacturerName"].ToString() : null,
                                    ManufacturerNotes = reader["ManufacturerNotes"] != DBNull.Value ? reader["ManufacturerNotes"].ToString() : null,

                                    // Category info
                                    CategoryNo = reader["CategoryNo"] != DBNull.Value ? reader["CategoryNo"].ToString() : null,
                                    CategoryName = reader["CategoryName"] != DBNull.Value ? reader["CategoryName"].ToString() : null,
                                    CategoryDescription = reader["CategoryDescription"] != DBNull.Value ? reader["CategoryDescription"].ToString() : null,

                                    // Supplier info
                                    SupplierNo = reader["SupplierNo"] != DBNull.Value ? reader["SupplierNo"].ToString() : null,
                                    SupplierName = reader["SupplierName"] != DBNull.Value ? reader["SupplierName"].ToString() : null,
                                    SupplierContactInfo = reader["SupplierContactInfo"] != DBNull.Value ? reader["SupplierContactInfo"].ToString() : null,
                                    SupplierNotes = reader["SupplierNotes"] != DBNull.Value ? reader["SupplierNotes"].ToString() : null,

                                    // Location info
                                    LocationNo = reader["LocationNo"] != DBNull.Value ? reader["LocationNo"].ToString() : null,
                                    LocationName = reader["LocationName"] != DBNull.Value ? reader["LocationName"].ToString() : null,
                                    LocationArea = reader["LocationArea"] != DBNull.Value ? reader["LocationArea"].ToString() : null,
                                    LocationBuilding = reader["LocationBuilding"] != DBNull.Value ? reader["LocationBuilding"].ToString() : null,
                                    LocationNotes = reader["LocationNotes"] != DBNull.Value ? reader["LocationNotes"].ToString() : null,

                                    // Unit info
                                    UnitNo = reader["UnitNo"] != DBNull.Value ? reader["UnitNo"].ToString() : null,
                                    UnitName = reader["UnitName"] != DBNull.Value ? reader["UnitName"].ToString() : null
                                };

                                // AllText-felt - komprimeret ( NOPE )
                                //var allTextParts = new List<string>
                                //{
                                //    doc.SparePartNo, doc.SparePartSerialCode, doc.Name,
                                //    doc.Description, doc.TypeNo, doc.Notes,
                                //    doc.ManufacturerNo, doc.ManufacturerName, doc.ManufacturerNotes,
                                //    doc.CategoryNo, doc.CategoryName, doc.CategoryDescription,
                                //    doc.SupplierNo, doc.SupplierName, doc.SupplierContactInfo, doc.SupplierNotes,
                                //    doc.LocationNo, doc.LocationName, doc.LocationArea,
                                //    doc.LocationBuilding, doc.LocationNotes, doc.UnitNo, doc.UnitName
                                //};

                                //doc.AllText = string.Join(" ", allTextParts.Where(p => !string.IsNullOrEmpty(p)));
                                documents.Add(doc);
                            }
                        }
                    }

                    if (documents.Count > 0)
                    {
                        try
                        {
                            // Opret bulk request
                            var bulkRequest = new BulkRequest
                            {
                                Operations = new List<IBulkOperation>()
                            };

                            foreach (var doc in documents)
                            {
                                bulkRequest.Operations.Add(new BulkIndexOperation<SparePartDocument>(doc)
                                {
                                    Id = doc.Id,
                                    Index = _indexName
                                });
                            }

                            var bulkResponse = await _client.BulkAsync(bulkRequest);

                            if (bulkResponse.IsValidResponse)
                            {
                                processed += documents.Count;
                                batchNumber++;

                                double elapsedSeconds = batchTimer.Elapsed.TotalSeconds;
                                double docsPerSecond = documents.Count / elapsedSeconds;

                                Console.WriteLine($"Thread {threadId}: Indexed batch {batchNumber} with {documents.Count} documents in {elapsedSeconds:F2}s ({docsPerSecond:F2} docs/sec)");
                                Console.WriteLine($"Thread {threadId}: Total processed: {processed}");
                            }
                            else
                            {
                                Console.WriteLine($"Error in thread {threadId}, batch {batchNumber}: {bulkResponse.DebugInformation}");
                            }
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

        // Hjælpemetode til at optimere Elasticsearch for bulkindlæsning
        private async Task OptimizeForBulkLoadingAsync()
        {
            Console.WriteLine("Optimizing Elasticsearch for bulk loading...");
            try
            {
                string settingsJson = @"{
                    ""index"": {
                        ""refresh_interval"": ""-1"",
                        ""number_of_replicas"": 0
                    },
                    ""translog"": {
                        ""durability"": ""async"",
                        ""flush_threshold_size"": ""1gb""
                    }
                }";
                // Brug den direkte HTTP-metode
                var response = await _client.Transport.RequestAsync<StringResponse>(
                    Elastic.Transport.HttpMethod.PUT,
                    $"{_indexName}/_settings",
                    PostData.String(settingsJson)
                );

                if (response.ApiCallDetails.HasSuccessfulStatusCode)
                {
                    Console.WriteLine("Elasticsearch optimized for bulk loading");
                }
                else
                {
                    Console.WriteLine($"Warning: Could not optimize settings: {response.ApiCallDetails.DebugInformation}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not optimize settings: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private async Task RestoreNormalSettingsAsync()
        {
            Console.WriteLine("Restoring normal Elasticsearch settings...");
            try
            {
                string settingsJson = @"{
                    ""index"": {
                        ""refresh_interval"": ""1s"",
                        ""number_of_replicas"": 1
                    },
                    ""translog"": {
                        ""durability"": ""request""
                    }
                }";
                // Brug den direkte HTTP-metode
                var response = await _client.Transport.RequestAsync<StringResponse>(
                    Elastic.Transport.HttpMethod.PUT,
                    $"{_indexName}/_settings",
                    PostData.String(settingsJson)
                );

                if (response.ApiCallDetails.HasSuccessfulStatusCode)
                {
                    Console.WriteLine("Normal Elasticsearch settings restored");
                }
                else
                {
                    Console.WriteLine($"Warning: Could not restore settings: {response.ApiCallDetails.DebugInformation}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not restore settings: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

    }

    public class DecimalToDoubleConverter : JsonConverter<double?>
    {
        public override double? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.Null)
                return null;

            if (reader.TokenType == System.Text.Json.JsonTokenType.Number)
            {
                return reader.GetDouble();
            }

            if (reader.TokenType == System.Text.Json.JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (double.TryParse(stringValue, out double value))
                {
                    return value;
                }
            }

            return null;
        }

        public override void Write(System.Text.Json.Utf8JsonWriter writer, double? value, System.Text.Json.JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }
    }

    // Document model - ingen ændringer nødvendig her
    public class SparePartDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        //[JsonPropertyName("unitGuid")]
        //public string UnitGuid { get; set; }

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

        //[JsonPropertyName("basePrice")]
        //[JsonConverter(typeof(DecimalToDoubleConverter))]
        //public double? BasePrice { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        // Manufacturer info
        //[JsonPropertyName("manufacturerId")]
        //public string ManufacturerId { get; set; }

        [JsonPropertyName("manufacturerNo")]
        public string ManufacturerNo { get; set; }

        [JsonPropertyName("manufacturerName")]
        public string ManufacturerName { get; set; }

        [JsonPropertyName("manufacturerNotes")]
        public string ManufacturerNotes { get; set; }

        // Category info
        //[JsonPropertyName("categoryId")]
        //public string CategoryId { get; set; }

        [JsonPropertyName("categoryNo")]
        public string CategoryNo { get; set; }

        [JsonPropertyName("categoryName")]
        public string CategoryName { get; set; }

        [JsonPropertyName("categoryDescription")]
        public string CategoryDescription { get; set; }

        // Supplier info
        //[JsonPropertyName("supplierId")]
        //public string SupplierId { get; set; }

        [JsonPropertyName("supplierNo")]
        public string SupplierNo { get; set; }

        [JsonPropertyName("supplierName")]
        public string SupplierName { get; set; }

        [JsonPropertyName("supplierContactInfo")]
        public string SupplierContactInfo { get; set; }

        [JsonPropertyName("supplierNotes")]
        public string SupplierNotes { get; set; }

        // Location info
        //[JsonPropertyName("locationId")]
        //public string LocationId { get; set; }

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

        // Unit info
        [JsonPropertyName("unitNo")]
        public string UnitNo { get; set; }

        [JsonPropertyName("unitName")]
        public string UnitName { get; set; }

        // Combined field for full-text search across all text fields
        //[JsonPropertyName("allText")]
        //public string AllText { get; set; }
    }
}