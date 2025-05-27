using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace ApacheSolrAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RawDebugController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new HttpClient();

        public RawDebugController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("raw-query")]
        public async Task<IActionResult> RawQuery(string q = "*:*", int rows = 5)
        {
            var solrUrl = _configuration.GetValue<string>("Solr:Url") ?? "http://localhost:8983/solr";
            var coreName = _configuration.GetValue<string>("Solr:CoreName") ?? "spareparts";

            try
            {
                // Direct HTTP call to Solr
                var url = $"{solrUrl}/{coreName}/select?q={q}&rows={rows}&wt=json&indent=true";
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                // Parse as JSON to make it readable
                var json = JsonDocument.Parse(content);

                return Ok(new
                {
                    Url = url,
                    StatusCode = response.StatusCode,
                    RawResponse = json
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("check-schema")]
        public async Task<IActionResult> CheckSchema()
        {
            var solrUrl = _configuration.GetValue<string>("Solr:Url") ?? "http://localhost:8983/solr";
            var coreName = _configuration.GetValue<string>("Solr:CoreName") ?? "spareparts";

            try
            {
                // Get schema field types
                var url = $"{solrUrl}/{coreName}/schema/fields";
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                var json = JsonDocument.Parse(content);

                return Ok(new
                {
                    Url = url,
                    Schema = json
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("test-search")]
        public async Task<IActionResult> TestSearch(string query)
        {
            var solrUrl = _configuration.GetValue<string>("Solr:Url") ?? "http://localhost:8983/solr";
            var coreName = _configuration.GetValue<string>("Solr:CoreName") ?? "spareparts";

            try
            {
                // Try different query approaches
                var tests = new[]
                {
                    $"{solrUrl}/{coreName}/select?q={query}&df=_text_&wt=json",
                    $"{solrUrl}/{coreName}/select?q=name:{query}&wt=json",
                    $"{solrUrl}/{coreName}/select?q=name:*{query}*&wt=json",
                    $"{solrUrl}/{coreName}/select?q=_text_:{query}&wt=json",
                    $"{solrUrl}/{coreName}/select?q={query}&defType=edismax&qf=name+description+sparePartNo&wt=json"
                };

                var results = new System.Collections.Generic.List<object>();

                foreach (var url in tests)
                {
                    var response = await _httpClient.GetAsync(url);
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonDocument.Parse(content);

                    results.Add(new
                    {
                        TestUrl = url,
                        NumFound = json.RootElement.GetProperty("response").GetProperty("numFound").GetInt32()
                    });
                }

                return Ok(new
                {
                    Query = query,
                    Tests = results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}