using Microsoft.Extensions.Configuration;
using SwAIvyn.Services.VectorStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for the brain service
    /// </summary>
    public interface IBrainService
    {
        /// <summary>
        /// Adds a memory to the brain
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <param name="text">Memory text</param>
        /// <param name="metadata">Optional metadata</param>
        /// <param name="scope">Vector scope</param>
        /// <returns>True if successful</returns>
        Task<bool> AddMemoryAsync(Guid id, string text, Dictionary<string, string>? metadata = null, VectorScope scope = VectorScope.Core);

        /// <summary>
        /// Searches the brain for similar memories
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="limit">Maximum number of results</param>
        /// <param name="scope">Vector scope</param>
        /// <returns>List of search hits</returns>
        Task<List<SearchHit>> SearchAsync(string query, int limit = 10, VectorScope scope = VectorScope.All);

        /// <summary>
        /// Deletes a memory from the brain
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <param name="scope">Vector scope</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteMemoryAsync(Guid id, VectorScope scope = VectorScope.Core);

        /// <summary>
        /// Gets the status of the brain
        /// </summary>
        /// <returns>Status information</returns>
        Task<Dictionary<string, object>> GetStatusAsync();
    }    /// <summary>
    /// Service that manages the brain functionality
    /// </summary>
    public class BrainService : IBrainService
    {
        private readonly IVectorRouter _vectorRouter;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISimpleLoggerService _logger;
        private readonly bool _vectorServerAvailable;

        // Default user ID for single-user application
        private static readonly Guid DefaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        /// <summary>
        /// Initializes a new instance of the BrainService
        /// </summary>
        /// <param name="vectorRouter">Vector router</param>
        /// <param name="embeddingService">Embedding service</param>
        /// <param name="logger">Logger service</param>
        /// <param name="configuration">Application configuration</param>
        public BrainService(
            IVectorRouter vectorRouter,
            IEmbeddingService embeddingService,
            ISimpleLoggerService logger,
            IConfiguration configuration)
        {
            _vectorRouter = vectorRouter;
            _embeddingService = embeddingService;
            _logger = logger;
            _vectorServerAvailable = configuration.GetValue<bool>("AppSettings:VectorServerAvailable", false);
        }        /// <inheritdoc/>
        public async Task<bool> AddMemoryAsync(Guid id, string text, Dictionary<string, string>? metadata = null, VectorScope scope = VectorScope.Core)
        {
            try
            {
                // Ensure metadata exists and has userId
                metadata ??= new Dictionary<string, string>();
                
                // Extract userId from metadata or use default
                var userId = DefaultUserId;
                if (metadata.ContainsKey("userId") && Guid.TryParse(metadata["userId"], out var parsedUserId))
                {
                    userId = parsedUserId;
                }
                else
                {
                    // Add userId to metadata if not present
                    metadata["userId"] = userId.ToString();
                }

                // Generate embedding for the text
                var embedding = await _embeddingService.EmbedTextAsync(text);

                // Store the memory in Neo4j through the router
                return await _vectorRouter.StoreMemoryAsync(id, embedding, metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to add memory {id}", ex);
                return false;
            }
        }        /// <inheritdoc/>
        public async Task<List<SearchHit>> SearchAsync(string query, int limit = 10, VectorScope scope = VectorScope.All)
        {
            try
            {
                // If vector server is not available and scope is Remote or All, adjust scope
                if (!_vectorServerAvailable && (scope == VectorScope.Remote || scope == VectorScope.All))
                {
                    scope = VectorScope.Core;
                    _logger.LogWarning("Vector server is not available. Falling back to local search only.");
                }

                // For the router, we search memories in Neo4j (which represents "core" memories)
                // Use the default user ID for single-user application
                var userId = DefaultUserId;
                
                // Search memories through the router
                var results = await _vectorRouter.SearchMemoryAsync(userId, query, limit);
                
                // Convert IReadOnlyList<SearchHit> to List<SearchHit>
                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to search brain", ex);
                return new List<SearchHit>();
            }
        }        /// <inheritdoc/>
        public async Task<bool> DeleteMemoryAsync(Guid id, VectorScope scope = VectorScope.Core)
        {
            try
            {
                // Delete memory from Neo4j through the router
                return await _vectorRouter.DeleteMemoryAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete memory {id}", ex);
                return false;
            }
        }        /// <inheritdoc/>
        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            try
            {
                var status = await _vectorRouter.GetStatusAsync();
                status["VectorServerAvailable"] = _vectorServerAvailable;
                status["EmbeddingDimensions"] = _embeddingService.Dimensions;
                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get brain status", ex);
                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message
                };
            }
        }
    }
}
