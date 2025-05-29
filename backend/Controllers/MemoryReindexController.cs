using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services.Memory;
using SwAIvyn.Services;
using System;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for memory re-indexing operations
    /// </summary>
    [ApiController]
    [Route("api/memory/reindex")]
    public class MemoryReindexController : ControllerBase
    {
        private readonly MemoryReindexService _reindexService;
        private readonly ISimpleLoggerService _logger;

        public MemoryReindexController(
            MemoryReindexService reindexService,
            ISimpleLoggerService logger)
        {
            _reindexService = reindexService;
            _logger = logger;
        }

        /// <summary>
        /// Re-indexes all memories into Neo4j vector store
        /// </summary>
        /// <returns>Number of memories successfully re-indexed</returns>
        [HttpPost("all")]
        public async Task<IActionResult> ReindexAllMemories()
        {
            try
            {
                _logger.LogInfo("[MEMORY_REINDEX_API] Starting re-index all memories request");

                var count = await _reindexService.ReindexAllMemoriesAsync();

                _logger.LogInfo($"[MEMORY_REINDEX_API] Re-indexed {count} memories successfully");

                return Ok(new {
                    success = true,
                    message = $"Successfully re-indexed {count} memories",
                    count = count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("[MEMORY_REINDEX_API] Failed to re-index all memories", ex);
                return StatusCode(500, new {
                    success = false,
                    message = "Failed to re-index memories",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Re-indexes memories for a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Number of memories successfully re-indexed</returns>
        [HttpPost("user/{userId}")]
        public async Task<IActionResult> ReindexUserMemories(Guid userId)
        {
            try
            {
                _logger.LogInfo($"[MEMORY_REINDEX_API] Starting re-index memories for user {userId}");

                var count = await _reindexService.ReindexUserMemoriesAsync(userId);

                _logger.LogInfo($"[MEMORY_REINDEX_API] Re-indexed {count} memories for user {userId}");

                return Ok(new {
                    success = true,
                    message = $"Successfully re-indexed {count} memories for user {userId}",
                    userId = userId,
                    count = count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MEMORY_REINDEX_API] Failed to re-index memories for user {userId}", ex);
                return StatusCode(500, new {
                    success = false,
                    message = $"Failed to re-index memories for user {userId}",
                    error = ex.Message,
                    userId = userId,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Gets the status of memory indexing
        /// </summary>
        /// <returns>Indexing status information</returns>
        [HttpGet("status")]
        public async Task<IActionResult> GetIndexingStatus()
        {
            try
            {
                _logger.LogInfo("[MEMORY_REINDEX_API] Getting indexing status");

                var status = await _reindexService.GetIndexingStatusAsync();

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError("[MEMORY_REINDEX_API] Failed to get indexing status", ex);
                return StatusCode(500, new {
                    success = false,
                    message = "Failed to get indexing status",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Checks if a specific memory is indexed
        /// </summary>
        /// <param name="memoryId">Memory ID to check</param>
        /// <returns>Whether the memory is indexed</returns>
        [HttpGet("check/{memoryId}")]
        public async Task<IActionResult> CheckMemoryIndexed(Guid memoryId)
        {
            try
            {
                _logger.LogInfo($"[MEMORY_REINDEX_API] Checking if memory {memoryId} is indexed");

                var isIndexed = await _reindexService.IsMemoryIndexedAsync(memoryId);

                return Ok(new {
                    memoryId = memoryId,
                    isIndexed = isIndexed,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MEMORY_REINDEX_API] Failed to check if memory {memoryId} is indexed", ex);
                return StatusCode(500, new {
                    success = false,
                    message = $"Failed to check memory {memoryId}",
                    error = ex.Message,
                    memoryId = memoryId,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}
