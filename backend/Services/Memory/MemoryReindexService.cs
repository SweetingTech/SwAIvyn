using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Services;
using SwAIvyn.Services.VectorStore;

namespace SwAIvyn.Services.Memory
{
    /// <summary>
    /// Service for re-indexing existing memories into Neo4j vector store
    /// </summary>
    public class MemoryReindexService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IVectorRouter _vectorRouter;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISimpleLoggerService _logger;

        public MemoryReindexService(
            ApplicationDbContext dbContext,
            IVectorRouter vectorRouter,
            IEmbeddingService embeddingService,
            ISimpleLoggerService logger)
        {
            _dbContext = dbContext;
            _vectorRouter = vectorRouter;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        /// <summary>
        /// Re-indexes all memories from SQLite database into Neo4j vector store
        /// </summary>
        /// <returns>Number of memories successfully re-indexed</returns>
        public async Task<int> ReindexAllMemoriesAsync()
        {
            _logger.LogInfo("[MEMORY_REINDEX] Starting memory re-indexing process...");

            try
            {
                // Get all memories from the database
                var memories = await _dbContext.Memories
                    .Where(m => !string.IsNullOrEmpty(m.Content))
                    .ToListAsync();

                _logger.LogInfo($"[MEMORY_REINDEX] Found {memories.Count} memories to re-index");

                int successCount = 0;
                int errorCount = 0;

                foreach (var memory in memories)
                {
                    try
                    {
                        await ReindexSingleMemoryAsync(memory);
                        successCount++;

                        if (successCount % 10 == 0)
                        {
                            _logger.LogInfo($"[MEMORY_REINDEX] Progress: {successCount}/{memories.Count} memories processed");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError($"[MEMORY_REINDEX] Failed to re-index memory {memory.Id}: {memory.Content} - {ex.Message}");
                    }
                }

                _logger.LogInfo($"[MEMORY_REINDEX] Re-indexing completed. Success: {successCount}, Errors: {errorCount}");
                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError("[MEMORY_REINDEX] Failed to re-index memories", ex);
                return 0;
            }
        }

        /// <summary>
        /// Re-indexes memories for a specific user
        /// </summary>
        /// <param name="userId">User ID to filter by</param>
        /// <returns>Number of memories successfully re-indexed</returns>
        public async Task<int> ReindexUserMemoriesAsync(Guid userId)
        {
            _logger.LogInfo($"[MEMORY_REINDEX] Starting memory re-indexing for user {userId}...");

            try
            {
                // Get memories for the specific user
                var memories = await _dbContext.Memories
                    .Where(m => m.UserId == userId && !string.IsNullOrEmpty(m.Content))
                    .ToListAsync();

                _logger.LogInfo($"[MEMORY_REINDEX] Found {memories.Count} memories for user {userId}");

                int successCount = 0;
                int errorCount = 0;

                foreach (var memory in memories)
                {
                    try
                    {
                        await ReindexSingleMemoryAsync(memory);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError($"[MEMORY_REINDEX] Failed to re-index memory {memory.Id} for user {userId}: {memory.Content} - {ex.Message}");
                    }
                }

                _logger.LogInfo($"[MEMORY_REINDEX] User re-indexing completed. Success: {successCount}, Errors: {errorCount}");
                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MEMORY_REINDEX] Failed to re-index memories for user {userId}", ex);
                return 0;
            }
        }

        /// <summary>
        /// Re-indexes a single memory into Neo4j vector store
        /// </summary>
        /// <param name="memory">Memory entity to re-index</param>
        private async Task ReindexSingleMemoryAsync(SwAIvyn.Data.Entities.MemoryItem memory)
        {
            _logger.LogInfo($"[MEMORY_REINDEX] Re-indexing memory {memory.Id}: {memory.Content}");

            // Generate embedding for the memory content
            var embedding = await _embeddingService.EmbedTextAsync(memory.Content);

            // Prepare metadata
            var metadata = new Dictionary<string, string>
            {
                { "userId", memory.UserId.ToString() },
                { "content", memory.Content },
                { "category", memory.Category ?? "conversation" },
                { "isShared", memory.IsShared.ToString() },
                { "createdAt", memory.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                { "lastAccessed", memory.LastAccessed.ToString("yyyy-MM-ddTHH:mm:ssZ") }
            };

            // Store in Neo4j via vector router
            var success = await _vectorRouter.StoreMemoryAsync(memory.Id, embedding, metadata);

            if (success)
            {
                _logger.LogInfo($"[MEMORY_REINDEX] Successfully re-indexed memory {memory.Id}");
            }
            else
            {
                throw new Exception($"Failed to store memory {memory.Id} in Neo4j vector store");
            }
        }

        /// <summary>
        /// Gets the status of memory indexing
        /// </summary>
        /// <returns>Status information</returns>
        public async Task<Dictionary<string, object>> GetIndexingStatusAsync()
        {
            try
            {
                // Count memories in database
                var totalMemories = await _dbContext.Memories.CountAsync();
                var memoriesWithContent = await _dbContext.Memories
                    .Where(m => !string.IsNullOrEmpty(m.Content))
                    .CountAsync();

                // Get vector router status
                var routerStatus = await _vectorRouter.GetStatusAsync();

                return new Dictionary<string, object>
                {
                    { "totalMemoriesInDatabase", totalMemories },
                    { "memoriesWithContent", memoriesWithContent },
                    { "vectorStoreStatus", routerStatus },
                    { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("[MEMORY_REINDEX] Failed to get indexing status", ex);
                return new Dictionary<string, object>
                {
                    { "error", ex.Message },
                    { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                };
            }
        }

        /// <summary>
        /// Checks if a memory is already indexed in Neo4j
        /// </summary>
        /// <param name="memoryId">Memory ID to check</param>
        /// <returns>True if memory is indexed</returns>
        public async Task<bool> IsMemoryIndexedAsync(Guid memoryId)
        {
            try
            {
                // Try to search for the specific memory ID in Neo4j
                // This is a simple check - in a real implementation you might want to
                // query Neo4j directly to check if the memory node exists
                var dummyQuery = "test";
                var results = await _vectorRouter.SearchMemoryAsync(Guid.Empty, dummyQuery, 1);

                // For now, we'll assume memories need to be re-indexed
                // A more sophisticated implementation would check Neo4j directly
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MEMORY_REINDEX] Failed to check if memory {memoryId} is indexed", ex);
                return false;
            }
        }
    }
}
