using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;
using SwAIvyn.Services;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ISimpleLoggerService _logger;

        public SearchController(IHttpClientFactory httpClientFactory, ISimpleLoggerService logger)
        {
            _httpClient = httpClientFactory.CreateClient("searchService");
            _logger = logger;
        }

        /// <summary>
        /// Performs hybrid search across SQLite, Neo4j, and Weaviate databases
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="userId">User ID for filtering results</param>
        /// <param name="topK">Maximum number of results to return</param>
        /// <returns>Search results from all databases</returns>
        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] string query,
            [FromQuery] Guid userId,
            [FromQuery] int topK = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query cannot be empty");
                }

                _logger.LogInfo($"🔍 Hybrid search request: query='{query}', userId={userId}, topK={topK}");

                var searchRequest = new
                {
                    query = query,
                    userId = userId.ToString(),
                    topK = topK,
                    filters = new Dictionary<string, object>
                    {
                        ["userId"] = userId.ToString()
                    }
                };

                var response = await _httpClient.PostAsJsonAsync("/search", searchRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Search service error: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, new { error = errorContent });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var searchResults = JsonSerializer.Deserialize<dynamic>(responseContent);

                _logger.LogInfo($"✅ Search completed successfully");

                return Ok(searchResults);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"❌ Failed to connect to search service: {ex.Message}");
                return StatusCode(503, new { error = "Search service unavailable", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Search error: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Gets explanation of how search results were obtained
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="userId">User ID</param>
        /// <returns>Search explanation</returns>
        [HttpGet("explain")]
        public async Task<IActionResult> ExplainSearch(
            [FromQuery] string query,
            [FromQuery] Guid userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query cannot be empty");
                }

                _logger.LogInfo($"🔍 Search explanation request: query='{query}', userId={userId}");

                var response = await _httpClient.GetAsync($"/search/explain/{Uri.EscapeDataString(query)}?user_id={userId}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Search explanation error: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, new { error = errorContent });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var explanation = JsonSerializer.Deserialize<dynamic>(responseContent);

                return Ok(explanation);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"❌ Failed to connect to search service: {ex.Message}");
                return StatusCode(503, new { error = "Search service unavailable", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Search explanation error: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Gets the status of the search service and its database connections
        /// </summary>
        /// <returns>Search service status</returns>
        [HttpGet("status")]
        public async Task<IActionResult> GetSearchStatus()
        {
            try
            {
                _logger.LogInfo("🔍 Checking search service status");

                var response = await _httpClient.GetAsync("/search/status");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"⚠️ Search service status check failed: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, new { error = errorContent });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var status = JsonSerializer.Deserialize<dynamic>(responseContent);

                return Ok(status);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"❌ Failed to connect to search service: {ex.Message}");
                return StatusCode(503, new { 
                    error = "Search service unavailable", 
                    details = ex.Message,
                    status = "offline"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Search status check error: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Health check for the search service
        /// </summary>
        /// <returns>Health status</returns>
        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(503, new { status = "unhealthy", message = "Search service is down" });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var health = JsonSerializer.Deserialize<dynamic>(responseContent);

                return Ok(health);
            }
            catch (HttpRequestException)
            {
                return StatusCode(503, new { status = "unhealthy", message = "Cannot connect to search service" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }
    }
}
