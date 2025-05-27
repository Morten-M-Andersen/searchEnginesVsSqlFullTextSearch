using Microsoft.AspNetCore.Mvc;
using SolrNet;
using System.Linq;
using System.Threading.Tasks;

namespace ApacheSolrAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly ISolrOperations<SparePartDocument> _solr;

        public DebugController(ISolrOperations<SparePartDocument> solr)
        {
            _solr = solr;
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetDocumentCount()
        {
            try
            {
                // Get all documents count
                var allDocsQuery = SolrQuery.All;
                var results = await Task.Run(() => _solr.Query(allDocsQuery, new SolrNet.Commands.Parameters.QueryOptions { Rows = 0 }));

                return Ok(new
                {
                    TotalDocuments = results.NumFound,
                    Message = results.NumFound == 0 ? "No documents in Solr core. You need to index data first." : $"Found {results.NumFound} documents"
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message, Type = ex.GetType().Name });
            }
        }

        [HttpGet("sample")]
        public async Task<IActionResult> GetSampleDocuments()
        {
            try
            {
                // Get first 5 documents
                var allDocsQuery = SolrQuery.All;
                var results = await Task.Run(() => _solr.Query(allDocsQuery, new SolrNet.Commands.Parameters.QueryOptions { Rows = 5 }));

                return Ok(new
                {
                    TotalDocuments = results.NumFound,
                    SampleCount = results.Count,
                    Samples = results.Select(doc => new
                    {
                        doc.Id,
                        doc.Name,
                        doc.SparePartNo,
                        doc.Description,
                        doc.ManufacturerName,
                        doc.CategoryName
                    })
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("search-all")]
        public async Task<IActionResult> SearchAllFields(string query)
        {
            try
            {
                // Try searching with *:* syntax
                var searchQuery = new SolrQuery($"*{query}*");
                var results = await Task.Run(() => _solr.Query(searchQuery, new SolrNet.Commands.Parameters.QueryOptions { Rows = 10 }));

                return Ok(new
                {
                    Query = query,
                    TotalFound = results.NumFound,
                    Results = results.Select(doc => new
                    {
                        doc.Id,
                        doc.Name,
                        doc.SparePartNo,
                        doc.Description
                    })
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("fields")]
        public IActionResult GetSearchableFields()
        {
            return Ok(new
            {
                Message = "These are the fields we're searching in:",
                Fields = new[]
                {
                    "id", "sparePartNo", "sparePartSerialCode", "name", "description",
                    "typeNo", "manufacturerName", "manufacturerNo", "categoryName",
                    "categoryNo", "supplierName", "locationName", "unitName", "unitNo",
                    "notes", "manufacturerNotes", "categoryDescription", "supplierContactInfo",
                    "supplierNotes", "locationArea", "locationBuilding", "locationNotes"
                }
            });
        }
    }
}