using SwAIvyn.Data.Entities;
using SwAIvyn.Enums;
using SwAIvyn.Services.Graph;
using SwAIvyn.Services.Interfaces;
using SwAIvyn.Services.VectorStore;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SwAIvyn.Services
{    /// <summary>
    /// Vector router implementation that intelligently routes operations between Neo4j and Weaviate.
    /// </summary>
    public class VectorRouter : SwAIvyn.Services.Interfaces.IVectorRouter
    {
        private readonly IBrainGraphService _brainGraphService;
        private readonly IVectorStore _weaviateVectorStore;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<VectorRouter> _logger;

        public VectorRouter(
            IBrainGraphService brainGraphService,
            IVectorStore weaviateVectorStore,
            IEmbeddingService embeddingService,
            ILogger<VectorRouter> logger)
        {
            _brainGraphService = brainGraphService;
            _weaviateVectorStore = weaviateVectorStore;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        /// <summary>
        /// Determines optimal vector store based on content analysis and metadata.
        /// </summary>
        public VectorTarget DetermineOptimalStore(MemoryItem memory, Dictionary<string, string>? metadata = null)
        {
            try
            {
                // Check explicit metadata directive
                if (metadata?.TryGetValue("forceTarget", out var forceTarget) == true)
                {
                    if (Enum.TryParse<VectorTarget>(forceTarget, true, out var forcedTarget))
                    {
                        _logger.LogInformation($"🎯 Forced routing to {forcedTarget} for memory {memory.Id}");
                        return forcedTarget;
                    }
                }

                // Analyze content type and category to determine optimal store
                var content = memory.Content?.ToLowerInvariant() ?? "";
                var category = memory.Category?.ToLowerInvariant() ?? "";

                // Document/file patterns → Weaviate
                if (IsDocumentContent(content, metadata))
                {
                    _logger.LogInformation($"📄 Routing to Weaviate (document content) for memory {memory.Id}");
                    return VectorTarget.Weaviate;
                }

                // Personal/relational content → Neo4j (brain graph)
                if (IsPersonalOrRelationalContent(content, category))
                {
                    _logger.LogInformation($"🧠 Routing to Neo4j (brain graph) for memory {memory.Id}");
                    return VectorTarget.Neo4j;
                }

                // Default fallback based on category
                var defaultTarget = GetDefaultTargetByCategory(category);
                _logger.LogInformation($"🎯 Default routing to {defaultTarget} for category '{category}' memory {memory.Id}");
                return defaultTarget;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error determining optimal store for memory {memory.Id}, defaulting to Neo4j");
                return VectorTarget.Neo4j; // Safe default
            }
        }

        /// <summary>
        /// Adds memory to specified vector store.
        /// </summary>
        public async Task<bool> AddToVectorStoreAsync(MemoryItem memory, VectorTarget targetStore, Dictionary<string, string>? metadata = null)
        {
            try
            {
                metadata ??= new Dictionary<string, string>();
                
                // Add standard metadata
                metadata["userId"] = memory.UserId.ToString();
                metadata["category"] = memory.Category;
                metadata["isShared"] = memory.IsShared.ToString();
                metadata["createdAt"] = memory.CreatedAt.ToString("O");
                metadata["targetStore"] = targetStore.ToString();
                metadata["content"] = memory.Content ?? "";

                switch (targetStore)
                {
                    case VectorTarget.Neo4j:
                        var success = await _brainGraphService.AddMemoryAsync(memory.Id, memory.Content, metadata);
                        _logger.LogInformation($"📊 Added memory {memory.Id} to Neo4j: {(success ? "✅" : "❌")}");
                        return success;

                    case VectorTarget.Weaviate:
                        // Use StoreVectorAsync instead of AddMemoryAsync
                        var embedding = await _embeddingService.EmbedTextAsync(memory.Content ?? "");
                        await _weaviateVectorStore.StoreVectorAsync(memory.Id, embedding, metadata, VectorScope.Core);
                        _logger.LogInformation($"🗄️ Added memory {memory.Id} to Weaviate: ✅");
                        return true;

                    default:
                        _logger.LogWarning($"⚠️ Unknown target store: {targetStore}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to add memory {memory.Id} to {targetStore}");
                return false;
            }
        }

        /// <summary>
        /// Updates memory in specified vector store.
        /// </summary>
        public async Task<bool> UpdateInVectorStoreAsync(Guid memoryId, string content, VectorTarget targetStore, Dictionary<string, string>? metadata = null)
        {
            try
            {
                switch (targetStore)
                {
                    case VectorTarget.Neo4j:
                        // Use AddMemoryAsync with existing ID instead of UpdateMemoryAsync
                        var success = await _brainGraphService.AddMemoryAsync(memoryId, content, metadata);
                        _logger.LogInformation($"📊 Updated memory {memoryId} in Neo4j: {(success ? "✅" : "❌")}");
                        return success;

                    case VectorTarget.Weaviate:
                        // For Weaviate, delete and re-add with new embedding
                        var embedding = await _embeddingService.EmbedTextAsync(content);
                        await _weaviateVectorStore.DeleteVectorAsync(memoryId, VectorScope.Core);
                        await _weaviateVectorStore.StoreVectorAsync(memoryId, embedding, metadata ?? new Dictionary<string, string>(), VectorScope.Core);
                        _logger.LogInformation($"🗄️ Updated memory {memoryId} in Weaviate: ✅");
                        return true;

                    default:
                        _logger.LogWarning($"⚠️ Unknown target store: {targetStore}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update memory {memoryId} in {targetStore}");
                return false;
            }
        }

        /// <summary>
        /// Removes memory from specified vector store.
        /// </summary>
        public async Task<bool> RemoveFromVectorStoreAsync(Guid memoryId, VectorTarget targetStore)
        {
            try
            {
                switch (targetStore)
                {
                    case VectorTarget.Neo4j:
                        var success = await _brainGraphService.DeleteMemoryAsync(memoryId);
                        _logger.LogInformation($"📊 Removed memory {memoryId} from Neo4j: {(success ? "✅" : "❌")}");
                        return success;

                    case VectorTarget.Weaviate:
                        // Use DeleteVectorAsync instead of DeleteMemoryAsync
                        await _weaviateVectorStore.DeleteVectorAsync(memoryId, VectorScope.Core);
                        _logger.LogInformation($"🗄️ Removed memory {memoryId} from Weaviate: ✅");
                        return true;

                    default:
                        _logger.LogWarning($"⚠️ Unknown target store: {targetStore}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to remove memory {memoryId} from {targetStore}");
                return false;
            }
        }

        /// <summary>
        /// Searches specified vector store for similar memories.
        /// </summary>
        public async Task<List<(Guid MemoryId, string Content, float Similarity)>> SearchVectorStoreAsync(string query, VectorTarget targetStore, Guid userId, int maxResults = 10)
        {
            try
            {
                var embedding = await _embeddingService.EmbedTextAsync(query);
                
                switch (targetStore)
                {
                    case VectorTarget.Neo4j:
                        // Use SearchAsync with correct parameters
                        var neoResults = await _brainGraphService.SearchAsync(query, maxResults);
                        return neoResults
                            .Where(r => r.Hit.Metadata?.GetValueOrDefault("userId") == userId.ToString())
                            .Select(r => (
                                MemoryId: r.Hit.Id, 
                                Content: r.Hit.Metadata?.GetValueOrDefault("content", "") ?? "", 
                                Similarity: r.Hit.Score
                            ))
                            .ToList();

                    case VectorTarget.Weaviate:
                        // Use SearchAsync with correct parameters
                        var weaviateResults = await _weaviateVectorStore.SearchAsync(embedding, maxResults);
                        return weaviateResults
                            .Where(r => r.Metadata?.GetValueOrDefault("userId") == userId.ToString())
                            .Select(r => (
                                MemoryId: r.Id, // Already a Guid, don't use Parse
                                Content: r.Metadata?.GetValueOrDefault("content", "") ?? "", 
                                Similarity: r.Score
                            ))
                            .ToList();

                    default:
                        _logger.LogWarning($"⚠️ Unknown target store: {targetStore}");
                        return new List<(Guid, string, float)>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to search {targetStore} for query: {query}");
                return new List<(Guid, string, float)>();
            }
        }

        /// <summary>
        /// Fan-out search across all vector stores with intelligent result merging.
        /// </summary>
        public async Task<List<(Guid MemoryId, string Content, float Similarity, VectorTarget Source)>> FanOutSearchAsync(string query, Guid userId, int maxResults = 10)
        {
            try
            {
                _logger.LogInformation($"🔍 Fan-out search for user {userId}: '{query}'");

                // Execute searches in parallel
                var searchTasks = new[]
                {
                    SearchVectorStoreWithSource(query, VectorTarget.Neo4j, userId, maxResults),
                    SearchVectorStoreWithSource(query, VectorTarget.Weaviate, userId, maxResults)
                };

                var results = await Task.WhenAll(searchTasks);

                // Merge and rank results
                var mergedResults = results
                    .SelectMany(r => r)
                    .OrderByDescending(r => r.Similarity)
                    .Take(maxResults)
                    .ToList();

                _logger.LogInformation($"🔍 Fan-out search returned {mergedResults.Count} results from {results.Length} stores");
                return mergedResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed fan-out search for query: {query}");
                return new List<(Guid, string, float, VectorTarget)>();
            }
        }

        #region Private Helper Methods

        private async Task<List<(Guid MemoryId, string Content, float Similarity, VectorTarget Source)>> SearchVectorStoreWithSource(string query, VectorTarget targetStore, Guid userId, int maxResults)
        {
            var results = await SearchVectorStoreAsync(query, targetStore, userId, maxResults);
            return results.Select(r => (r.MemoryId, r.Content, r.Similarity, targetStore)).ToList();
        }

        private static bool IsDocumentContent(string content, Dictionary<string, string>? metadata)
        {
            // Check for document indicators in metadata
            if (metadata != null)
            {
                if (metadata.ContainsKey("fileType") || metadata.ContainsKey("fileName") || metadata.ContainsKey("documentSource"))
                    return true;
                
                if (metadata.TryGetValue("source", out var source) && 
                    (source.Contains("upload") || source.Contains("file") || source.Contains("document")))
                    return true;
            }

            // Check for document-like content patterns
            var documentPatterns = new[]
            {
                @"\b(document|file|pdf|doc|txt|uploaded?|attachment)\b",
                @"\b(article|paper|report|manual|guide)\b",
                @"^(title|abstract|summary|conclusion):",
                @"\b(page \d+|chapter \d+|section \d+)\b"
            };

            return documentPatterns.Any(pattern => Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase));
        }

        private static bool IsPersonalOrRelationalContent(string content, string category)
        {
            // Personal categories → Neo4j for graph relationships
            var personalCategories = new[] { "personal", "facts", "events", "explicit", "auto-detected", "conversation" };
            if (personalCategories.Contains(category))
                return true;

            // Personal content patterns
            var personalPatterns = new[]
            {
                @"\b(i am|my name|i like|i prefer|i work|i live)\b",
                @"\b(remember that i|don't forget|keep in mind)\b",
                @"\b(family|friend|colleague|relationship|meeting)\b",
                @"\b(birthday|anniversary|appointment|event)\b"
            };

            return personalPatterns.Any(pattern => Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase));
        }

        private static VectorTarget GetDefaultTargetByCategory(string category)
        {
            return category switch
            {
                "document" or "upload" or "file" or "knowledge" => VectorTarget.Weaviate,
                "personal" or "facts" or "events" or "shared" or "conversation" or "explicit" or "auto-detected" => VectorTarget.Neo4j,
                _ => VectorTarget.Neo4j // Default to brain graph for relationships
            };
        }

        #endregion
    }
}
