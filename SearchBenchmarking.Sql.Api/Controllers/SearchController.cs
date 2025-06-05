using Microsoft.AspNetCore.Mvc;
using SearchBenchmarking.Library.DTOs;
using SearchBenchmarking.Library.Interfaces;
using System;
using System.Threading.Tasks;

namespace SearchBenchmarking.Sql.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Rute bliver /api/Search
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ISearchService searchService, ILogger<SearchController> logger)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Searches the Solr index based on the provided query and parameters.
        /// </summary>
        /// <param name="request">The search request parameters.</param>
        /// <returns>A list of IDs matching the search criteria.</returns>
        /// <remarks>
        /// Eksempel på kald:
        /// 
        ///     GET /api/Search?Query=test&PageSize=10&StartFrom=0
        ///
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(SearchResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Search([FromQuery] SearchRequest request) // [FromQuery] binder URL query parametre (Query, PageSize, StartFrom) til SearchRequest objektet.
        {
            // Hvis request er null, returner BadRequest med en passende fejlmeddelelse.
            if (request == null)
            {
                return BadRequest("Search request cannot be null.");
            }
            // Validering af Query
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new ProblemDetails { Title = "Search query is required", Status = StatusCodes.Status400BadRequest });
            }
            // Validering af PageSize
            if (request.PageSize <= 0)
            {
                return BadRequest(new ProblemDetails { Title = "PageSize must be greater than zero.", Status = StatusCodes.Status400BadRequest });
            }
            // Validering af StartFrom
            if (request.StartFrom < 0)
            {
                // request.StartFrom = 0; // Sæt en default eller returner BadRequest
                return BadRequest(new ProblemDetails { Title = "StartFrom cannot be negative.", Status = StatusCodes.Status400BadRequest });
            }

            try
            {
                _logger.LogInformation("Received search request: Query='{Query}', PageSize={PageSize}, StartFrom={StartFrom}",
                                       request.Query, request.PageSize, request.StartFrom);

                SearchResult result = await _searchService.SearchAsync(request);

                if (result == null)
                {
                    // Dette scenarie bør håndteres i SearchService, men er tilføjet her som en ekstra sikkerhed:
                    _logger.LogWarning("Search service returned null for query: {Query}", request.Query);
                    return Ok(new SearchResult()); // Returner et tomt, men gyldigt, resultat
                }

                _logger.LogInformation("Search successful for Query='{Query}'. Found {TotalHits} hits. Returning {IdCount} IDs.",
                                       request.Query, result.TotalHits, result.Hits.Count);

                return Ok(result);
            }
            catch (ArgumentException argEx) // Kan kastes fra servicen, hvis input er ugyldigt på et dybere niveau
            {
                // Log den fulde exception for debugging
                _logger.LogWarning(argEx, "Invalid argument for search request: {Query}", request.Query);
                // Returner en generisk fejl til klienten for at undgå at lække følsomme detaljer
                return BadRequest(new ProblemDetails { Title = "Invalid search parameters.", Status = StatusCodes.Status400BadRequest });
            }
            catch (Exception ex)
            {
                // Log den fulde exception for debugging
                _logger.LogError(ex, "An unexpected error occurred during search for query: {Query}", request.Query);

                // Returner en generisk fejl til klienten for at undgå at lække følsomme detaljer
                return StatusCode(StatusCodes.Status500InternalServerError,
                                  new ProblemDetails { Title = "An unexpected error occurred while processing your request.", Status = StatusCodes.Status500InternalServerError });
            }
        }
    }
}