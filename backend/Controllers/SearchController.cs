using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly IHybridSearchService _hybridSearch;

        public SearchController(IHybridSearchService hybridSearch)
        {
            _hybridSearch = hybridSearch;
        }

        [HttpPost]
        public async Task<IActionResult> Search([FromBody] SearchRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
                return BadRequest(new { error = "Missing 'query'" });

            // Parse user id if provided; otherwise use empty GUID (service will handle scoping as needed)
            Guid userId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(request.UserId) && Guid.TryParse(request.UserId, out var parsed))
            {
                userId = parsed;
            }

            var topK = request.TopK <= 0 ? 10 : request.TopK;
            var results = await _hybridSearch.SearchAsync(request.Query, userId, topK);
            return Ok(results);
        }

        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            var ok = await _hybridSearch.IsHealthyAsync();
            return ok ? Ok(new { status = "ok" }) : StatusCode(503, new { status = "unavailable" });
        }
    }
}

