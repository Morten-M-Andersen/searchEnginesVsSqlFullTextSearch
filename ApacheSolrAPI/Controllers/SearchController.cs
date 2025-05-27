using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApacheSolrAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly SolrSearchService _solrSearchService;

        public SearchController(SolrSearchService solrSearchService)
        {
            _solrSearchService = solrSearchService;
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
                var result = await _solrSearchService.SearchAsync(query, size);
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
                var result = await _solrSearchService.AdvancedSearchAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Search error: {ex.Message}");
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", service = "Apache Solr API" });
        }
    }
}