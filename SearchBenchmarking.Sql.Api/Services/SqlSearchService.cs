using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchBenchmarking.Library.Interfaces;
using BenchmarkDTO = SearchBenchmarking.Library.DTOs; // Alias for DTOs
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics; // For Stopwatch
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchBenchmarking.Sql.Api.Services
{
    // Hjælpeklasse til at holde konfiguration for tabeller/kolonner
    public class TableSearchConfig
    {
        public string TableName { get; set; }
        public string IdColumn { get; set; }
        public List<string> FullTextColumns { get; set; } = new List<string>();
    }

    public class SqlSearchService : ISearchService
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlSearchService> _logger;
        private readonly List<TableSearchConfig> _searchConfigurations;
        private readonly int _defaultMaxResults;

        public SqlSearchService(IConfiguration configuration, ILogger<SqlSearchService> logger)
        {
            _connectionString = configuration.GetConnectionString("SparePartsDBConnection")
                ?? throw new InvalidOperationException("Connection string 'SparePartsDBConnection' not found.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _searchConfigurations = configuration.GetSection("SqlSearch:TablesAndColumns")
                                        .Get<List<TableSearchConfig>>()
                                    ?? new List<TableSearchConfig>();

            if (!_searchConfigurations.Any())
            {
                _logger.LogWarning("SqlSearch:TablesAndColumns configuration is missing or empty. No tables will be searched.");
            }
            _defaultMaxResults = configuration.GetValue<int>("SqlSearch:MaxResults", 100);
        }

        public async Task<BenchmarkDTO.SearchResult> SearchAsync(BenchmarkDTO.SearchRequest request)
        {
            // Validering af at request-objektet ikke er null
            if (request == null)
            {
                _logger.LogWarning("SearchRequest objektet var null for MSSQL.");
                return new BenchmarkDTO.SearchResult { ErrorMessage = "Search request cannot be null." };
            }
            // Validering om query-strengen indeholder noget
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                _logger.LogInformation("Tom søgestreng modtaget for MSSQL. Returnerer ingen resultater.");
                return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), TotalHits = -1, QueryTimeMs = 0 };
            }
            // Validering af at appsettings indeholder konfigurationer for tabeller/kolonner
            if (!_searchConfigurations.Any())
            {
                _logger.LogWarning("Ingen tabeller er konfigureret til MSSQL full-text søgning.");
                return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), TotalHits = 0, QueryTimeMs = -1 };
            }

            var allHits = new List<BenchmarkDTO.DocumentHit>();

            // Forberede SQL Full-Text Search udtryk
            // Simplet prefix search: "term*"
            // Andre muligheder er -> CONTAINS (kan være langsommere, men mere fleksibelt): '"term*"' ELLER 'FORMSOF(INFLECTIONAL, "term")'
            // Lige nu sigter vi efter noget der ligner Solr/ES's `term*` eller default `multi_match`
            // En simpel prefix søgning med FREETEXTTABLE eller CONTAINSTABLE:
            // '"søgeord*"'
            string ftsQueryTerm = $"\"{request.Query.Replace("\"", "\"\"")}*\""; // Escape anførselstegn og tilføj wildcard

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var allFoundIds = new HashSet<Guid>(); // For at undgå dupletter hvis et ID findes via flere tabeller

                    // Byg en UNION ALL query for at søge på tværs af alle konfigurerede tabeller
                    var queryBuilder = new StringBuilder();
                    var parameters = new List<SqlParameter>();
                    int paramIndex = 0;

                    foreach (var config in _searchConfigurations)
                    {
                        // Går ud af 'foreach' loop fordi der mangler værdier i appsettings, fanges af if(...Length == 0) nedenfor
                        if (!config.FullTextColumns.Any()) continue;
                        // Sammensætter en string med ',' mellem hvert FullTextColumn-navn fra appsettings
                        string columnsToSearch = string.Join(", ", config.FullTextColumns);
                        // Hvis der allerede er tekst i queryBuilder (dvs. springer over første gang)
                        if (queryBuilder.Length > 0)
                        {
                            queryBuilder.AppendLine("UNION ALL");
                        }

                        // Bruger CONTAINSTABLE for at få relevansscore (RANK)
                        // IdColumn er nødvendig for at joine tilbage og hente det faktiske ID.
                        // For CONTAINSTABLE skal man angive KEY(IdColumn).
                        queryBuilder.AppendLine($"SELECT T.[{config.IdColumn}] AS Id, CT.[RANK] AS Score");
                        queryBuilder.AppendLine($"FROM {config.TableName} AS T");
                        queryBuilder.AppendLine($"INNER JOIN CONTAINSTABLE({config.TableName}, ({columnsToSearch}), @p{paramIndex}) AS CT ON T.[{config.IdColumn}] = CT.[KEY]");

                        parameters.Add(new SqlParameter($"@p{paramIndex}", SqlDbType.NVarChar) { Value = ftsQueryTerm });
                        paramIndex++;
                    }

                    if (queryBuilder.Length == 0)
                    {
                        _logger.LogWarning("Ingen gyldige tabeller/kolonner fundet til søgning efter konfigurationscheck.");
                        return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), TotalHits = 0, QueryTimeMs = 0 };
                    }

                    // Tilføj ORDER BY, OFFSET, FETCH for paginering
                    // Bemærk: TotalHits vil kræve et separat COUNT(*) eller at hente alle resultater først
                    // For nu henter vi kun den ønskede side og TotalHits vil være antallet på siden.
                    // For korrekt TotalHits, skal en separat count query køres, eller vi fjerner paginering her og gør det i memory.

                    // For at få total count, er det nemmest at køre en subquery eller CTE.
                    string finalQuery = $@"
                        DECLARE @ServerStartTime DATETIME2(7) = SYSDATETIME();
                        WITH MatchedResults AS (
                            {queryBuilder.ToString()}
                        ),
                        RankedResults AS (
                            SELECT Id, Score, ROW_NUMBER() OVER (ORDER BY Score DESC, Id) as rn -- Id for stabil sortering
                            FROM MatchedResults
                            -- Duplikat ID håndtering: GROUP BY Id, vælg MAX(Score)
                            -- Dette kan gøres her, eller vi kan filtrere i C#
                            -- For nu, lad os antage at vi håndterer duplikater i C# eller at Id'er er unikke på tværs
                        )
                        SELECT Id, Score
                        FROM RankedResults
                        WHERE rn > @StartFrom
                        AND rn <= (@StartFrom + @PageSize)
                        ORDER BY Score DESC, Id;

                        SELECT COUNT_BIG(DISTINCT Id) 
                        FROM (
                            {queryBuilder.ToString()}
                        ) AS TotalCountSubQuery;

                        -- Beregn og returner serverens eksekveringstid
                        DECLARE @ServerEndTime DATETIME2(7) = SYSDATETIME();
                        SELECT DATEDIFF(MILLISECOND, @ServerStartTime, @ServerEndTime) AS ServerExecutionTimeMs;
                    ";

                    // Tilføj parametre for paginering
                    parameters.Add(new SqlParameter("@StartFrom", SqlDbType.Int) { Value = request.StartFrom });

                    // Sæt en standard PageSize hvis den ikke er angivet eller er mindre end 1
                    int pageSize = (request.PageSize <= 0) ? _defaultMaxResults : request.PageSize;
                    parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });

                    _logger.LogDebug("Executing SQL FTS Query: {Query} with term: {FtsTerm}", finalQuery, ftsQueryTerm);

                    using (var command = new SqlCommand(finalQuery, connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());

                        long totalHits = 0;
                        int serverExecutionTimeMs = 0;

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Læs resultater for den aktuelle side
                            while (await reader.ReadAsync())
                            {
                                var id = reader.GetGuid(reader.GetOrdinal("Id"));
                                // SQL Full-Text RANK er integer, typisk 0-1000.
                                // Score fra CONTAINSTABLE er int. Vi konverterer til float ift. Library -> SearchResult.cs.
                                var score = (float)reader.GetInt32(reader.GetOrdinal("Score"));

                                if (allFoundIds.Add(id)) // Undgå at tilføje samme ID flere gange med forskellig score fra forskellige tabeller
                                {
                                    allHits.Add(new BenchmarkDTO.DocumentHit { Id = id.ToString(), Score = score });
                                } // Evt. tilføje 'else'-logik for at håndtere duplikater her, hvis nødvendigt - evt. kun gemme den med højest score!?
                            }

                            // Det fulde antal af hits på søgningen
                            if (await reader.NextResultAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    totalHits = reader.GetInt64(0);
                                }
                            }

                            // SQL Server Execution Time fra 'DATEDIFF' i sidste SELECT
                            if (await reader.NextResultAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    serverExecutionTimeMs = reader.GetInt32(reader.GetOrdinal("ServerExecutionTimeMs"));
                                }
                            }
                        }

                        // Sorter i C# hvis UNION ALL + ROW_NUMBER ikke var nok til at fjerne duplikater med højeste score
                        // For nu stoler vi på ROW_NUMBER() og DISTINCT i COUNT.
                        // Hvis et ID kan komme fra flere tabeller, skal du have en strategi for hvilken score der tæller.
                        // Den nuværende query vil returnere et ID for hver tabel det matcher i.
                        // For at få unikke ID'er med højeste score, ville GROUP BY i SQL være bedre.

                        // En simpel måde at få unikke ID'er (tager den første forekomst baseret på Score DESC):
                        var distinctHits = allHits
                            .GroupBy(h => h.Id)
                            .Select(g => g.OrderByDescending(h => h.Score).First())
                            .OrderByDescending(h => h.Score) // Sorter igen efter at have valgt unikke
                            .ToList();


                        return new BenchmarkDTO.SearchResult
                        {
                            Hits = distinctHits, // Brug de unikke hits
                            TotalHits = totalHits,
                            QueryTimeMs = serverExecutionTimeMs
                        };
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "SQL-fejl under Full-Text søgning for query: {Query}", request.Query);
                return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), ErrorMessage = $"SQL error: {sqlEx.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Uventet fejl under MSSQL søgning for query: {Query}", request.Query);
                return new BenchmarkDTO.SearchResult { Hits = new List<BenchmarkDTO.DocumentHit>(), ErrorMessage = $"An unexpected error occurred: {ex.Message}" };
            }
        }
    }
}