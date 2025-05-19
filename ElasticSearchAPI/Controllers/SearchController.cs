using ElasticSearchAPI;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ElasticSearchAPI
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly IElasticSearchService _elasticSearchService;

        public SearchController(IElasticSearchService elasticSearchService)
        {
            _elasticSearchService = elasticSearchService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(string query, int size = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query is required");
            }

            try
            {
                var result = await _elasticSearchService.SearchAsync(query, size);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Search error: {ex.Message}");
            }
        }

        [HttpPost("advanced-search")]
        public async Task<IActionResult> AdvancedSearch([FromBody] SearchRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest("Search query is required");
            }

            try
            {
                var result = await _elasticSearchService.AdvancedSearchAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Search error: {ex.Message}");
            }
        }
    }
}