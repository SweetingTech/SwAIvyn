using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SwAIvyn.Services;

namespace SwAIvyn.Services.VectorStore
{
    /// <summary>
    /// Router that orchestrates vector operations between Neo4j (memories) and Weaviate (uploads)
    /// </summary>
    public class VectorRouter : IVectorRouter
    {
        private readonly Neo4jVectorStore _neo4jStore;
        private readonly WeaviateVectorStore _weaviateStore;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISimpleLoggerService _logger;

        public VectorRouter(
            Neo4jVectorStore neo4jStore,
            WeaviateVectorStore weaviateStore,
            IEmbeddingService embeddingService,
            ISimpleLoggerService logger)
        {
            _neo4jStore = neo4jStore;
            _weaviateStore = weaviateStore;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SearchHit>> SearchMemoryAsync(Guid userId, string query, int k = 8)
        {
            try
            {
                _logger.LogInfo($"[VECTOR_ROUTER] Searching memories with query: '{query}' (single-user app, no userId filtering)");

                // Generate embedding for the query
                var queryVector = await _embeddingService.EmbedTextAsync(query);

                // Search in Neo4j for memories
                var results = await _neo4jStore.SearchAsync(queryVector, k, VectorScope.All);

                // For single-user application, return all memory results without userId filtering
                // Memory functionality should not use userID filtering per application requirements
                _logger.LogInfo($"[VECTOR_ROUTER] Found {results.Count} memory results (no user filtering applied)");
                return results.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[VECTOR_ROUTER] Error searching memories", ex);
                return new List<SearchHit>().AsReadOnly();
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SearchHit>> SearchUploadsAsync(Guid userId, string query, int k = 8)
        {
            try
            {
                _logger.LogInfo($"[VECTOR_ROUTER] Searching uploads for user {userId} with query: '{query}'");

                // Generate embedding for the query
                var queryVector = await _embeddingService.EmbedTextAsync(query);

                // Search in Weaviate for uploads
                var results = await _weaviateStore.SearchAsync(queryVector, k, VectorScope.All);

                // Filter by userId and upload scope if metadata contains it
                var filteredResults = results
                    .Where(hit => hit.Metadata?.GetValueOrDefault("userId") == userId.ToString() &&
                                  hit.Metadata?.GetValueOrDefault("scope") == "upload")
                    .ToList();

                _logger.LogInfo($"[VECTOR_ROUTER] Found {filteredResults.Count} upload results for user {userId}");
                return filteredResults.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[VECTOR_ROUTER] Error searching uploads for user {userId}", ex);
                return new List<SearchHit>().AsReadOnly();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> StoreMemoryAsync(Guid id, float[] vector, IDictionary<string, string> metadata)
        {
            try
            {
                _logger.LogInfo($"[VECTOR_ROUTER] Storing memory vector for ID {id}");

                // Ensure scope is set to memory
                var memoryMetadata = new Dictionary<string, string>(metadata)
                {
                    ["scope"] = "memory"
                };

                var success = await _neo4jStore.StoreVectorAsync(id, vector, memoryMetadata, VectorScope.Core);

                if (success)
                    _logger.LogInfo($"[VECTOR_ROUTER] Successfully stored memory vector for ID {id}");
                else
                    _logger.LogWarning($"[VECTOR_ROUTER] Failed to store memory vector for ID {id}");

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[VECTOR_ROUTER] Error storing memory vector for ID {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> StoreUploadChunkAsync(Guid id, float[] vector, IDictionary<string, string> metadata)
        {
            try
            {
                _logger.LogInfo($"[VECTOR_ROUTER] Storing upload chunk vector for ID {id}");

                // Ensure scope is set to upload
                var uploadMetadata = new Dictionary<string, string>(metadata)
                {
                    ["scope"] = "upload"
                };

                var success = await _weaviateStore.StoreVectorAsync(id, vector, uploadMetadata, VectorScope.Core);

                if (success)
                    _logger.LogInfo($"[VECTOR_ROUTER] Successfully stored upload chunk vector for ID {id}");
                else
                    _logger.LogWarning($"[VECTOR_ROUTER] Failed to store upload chunk vector for ID {id}");

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[VECTOR_ROUTER] Error storing upload chunk vector for ID {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteMemoryAsync(Guid id)
        {
            try
            {
                _logger.LogInfo($"[VECTOR_ROUTER] Deleting memory vector for ID {id}");

                var success = await _neo4jStore.DeleteVectorAsync(id, VectorScope.Core);

                if (success)
                    _logger.LogInfo($"[VECTOR_ROUTER] Successfully deleted memory vector for ID {id}");
                else
                    _logger.LogWarning($"[VECTOR_ROUTER] Failed to delete memory vector for ID {id}");

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[VECTOR_ROUTER] Error deleting memory vector for ID {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteUploadChunkAsync(Guid id)
        {
            try
            {
                _logger.LogInfo($"[VECTOR_ROUTER] Deleting upload chunk vector for ID {id}");

                var success = await _weaviateStore.DeleteVectorAsync(id, VectorScope.Core);

                if (success)
                    _logger.LogInfo($"[VECTOR_ROUTER] Successfully deleted upload chunk vector for ID {id}");
                else
                    _logger.LogWarning($"[VECTOR_ROUTER] Failed to delete upload chunk vector for ID {id}");

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[VECTOR_ROUTER] Error deleting upload chunk vector for ID {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            var status = new Dictionary<string, object>();

            try
            {
                var neo4jStatus = await _neo4jStore.GetStatusAsync();
                var weaviateStatus = await _weaviateStore.GetStatusAsync();

                status["neo4j"] = neo4jStatus;
                status["weaviate"] = weaviateStatus;
                status["router"] = new Dictionary<string, object>
                {
                    { "initialized", true },
                    { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                };
            }
            catch (Exception ex)
            {
                status["error"] = ex.Message;
            }

            return status;
        }

        /// <inheritdoc/>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var neo4jHealthy = await _neo4jStore.HealthCheckAsync();
                var weaviateHealthy = await _weaviateStore.HealthCheckAsync();

                // Router is healthy if at least one store is healthy
                // Neo4j is primary for memories, so prioritize it
                return neo4jHealthy || weaviateHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError("[VECTOR_ROUTER] Error during health check", ex);
                return false;
            }
        }
    }
}
