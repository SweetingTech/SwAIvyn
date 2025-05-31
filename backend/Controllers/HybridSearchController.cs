using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using SwAIvyn.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for hybrid search functionality that combines SQL, Weaviate, and Neo4j databases
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HybridSearchController : ControllerBase
    {
        private readonly IHybridSearchService _hybridSearchService;
        private readonly ISimpleLoggerService _logger;

        public HybridSearchController(
            IHybridSearchService hybridSearchService,
            ISimpleLoggerService logger)
        {
            _hybridSearchService = hybridSearchService;
            _logger = logger;
        }

        /// <summary>
        /// Performs a hybrid search across all databases (SQLite, Neo4j, Weaviate)
        /// </summary>
        /// <param name="userId">User ID to filter results</param>
        /// <param name="query">Search query</param>
        /// <param name="maxResults">Maximum number of results to return (default: 10)</param>
        /// <param name="hybridAlpha">Weight for text vs semantic search (0.0 = only text, 1.0 = only semantic, default: 0.5)</param>
        /// <returns>Ranked hybrid search results</returns>
        [HttpGet("search")]
        public async Task<IActionResult> HybridSearch(
            [FromQuery] Guid userId,
            [FromQuery] string query,
            [FromQuery] int maxResults = 10,
            [FromQuery] double hybridAlpha = 0.5)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { error = "Query parameter is required" });
                }

                _logger.LogInfo($"[HYBRID_SEARCH] User: {userId}, Query: '{query}', MaxResults: {maxResults}, Alpha: {hybridAlpha}");

                var results = await _hybridSearchService.SearchAsync(userId, query, maxResults, hybridAlpha);

                return Ok(new
                {
                    success = true,
                    query = query,
                    userId = userId,
                    maxResults = maxResults,
                    hybridAlpha = hybridAlpha,
                    totalResults = results.Count,
                    results = results,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[HYBRID_SEARCH] Error performing hybrid search for user {userId}", ex);
                return StatusCode(500, new
                {
                    success = false,
                    error = "An error occurred while performing hybrid search",
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Gets the status of the Python hybrid search service
        /// </summary>
        /// <returns>Service status information</returns>
        [HttpGet("status")]
        public async Task<IActionResult> GetSearchServiceStatus()
        {
            try
            {
                var status = await _hybridSearchService.GetServiceStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError("[HYBRID_SEARCH] Error getting service status", ex);
                return StatusCode(500, new
                {
                    success = false,
                    error = "Failed to get search service status",
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}
