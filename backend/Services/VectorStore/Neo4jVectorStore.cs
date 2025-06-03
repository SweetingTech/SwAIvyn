using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SwAIvyn.Services.Graph;
using SwAIvyn.Services;

namespace SwAIvyn.Services.VectorStore
{
    /// <summary>
    /// Neo4j-based vector store implementation for brain memories
    /// </summary>
    public class Neo4jVectorStore : IVectorStore
    {
        private readonly INeo4jService _neo4jService;
        private readonly ISimpleLoggerService _logger;
        private readonly IConfiguration _configuration;
        private readonly int _vectorDimensions;
        private bool _isInitialized = false;

        public Neo4jVectorStore(
            INeo4jService neo4jService,
            ISimpleLoggerService logger,
            IConfiguration configuration)
        {
            _neo4jService = neo4jService;
            _logger = logger;
            _configuration = configuration;
            _vectorDimensions = configuration.GetValue<int>("AppSettings:VectorDimensions", 768);
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Neo4j vector store...");

                // Initialize Neo4j service if not already done
                await _neo4jService.InitializeAsync();
                _logger.LogInfo("Neo4j service initialization completed");

                // Create vector index for memories if it doesn't exist
                _logger.LogInfo("Creating Neo4j vector index...");
                await CreateVectorIndexAsync();
                _logger.LogInfo("Neo4j vector index creation completed");

                _isInitialized = true;
                _logger.LogInfo("Neo4j vector store initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Neo4j vector store", ex);
                _isInitialized = false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> StoreVectorAsync(Guid id, float[] embedding, Dictionary<string, string>? metadata = null, VectorScope scope = VectorScope.Core)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (!_isInitialized)
            {
                _logger.LogWarning("Attempted to store vector, but Neo4j vector store is not initialized");
                return false;
            }

            try
            {
                // Prepare properties for the Memory node
                var properties = new Dictionary<string, object>
                {
                    { "id", id.ToString() },
                    { "embedding", embedding },
                    { "createdAt", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                };

                // Add metadata to properties
                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        properties[kvp.Key] = kvp.Value;
                    }
                }

                // Create or update the Memory node with vector embedding
                var query = @"
                    MERGE (m:Memory {id: $id})
                    SET m += $props
                    RETURN m";

                var parameters = new Dictionary<string, object>
                {
                    { "id", id.ToString() },
                    { "props", properties }
                };

                var result = await _neo4jService.ExecuteQueryAsync(query, parameters);

                _logger.LogInfo($"Successfully stored vector for memory ID {id} in Neo4j");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to store vector for memory ID {id} in Neo4j", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<SearchHit>> SearchAsync(float[] queryVector, int limit = 10, VectorScope scope = VectorScope.All)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (!_isInitialized)
            {
                _logger.LogWarning("Attempted to search vectors, but Neo4j vector store is not initialized");
                return new List<SearchHit>();
            }

            try
            {
                _logger.LogInfo($"🔍 Performing Neo4j vector search with limit {limit}");

                // Use Neo4j vector similarity search
                var query = @"
                    CALL db.index.vector.queryNodes('memory_embedding_vector', $limit, $queryVector)
                    YIELD node, score
                    RETURN node.id as id, node.content as content, node.category as category,
                           node.userId as userId, score
                    ORDER BY score DESC";

                var parameters = new Dictionary<string, object>
                {
                    { "queryVector", queryVector },
                    { "limit", limit }
                };

                _logger.LogInfo("Executing Neo4j vector search query...");
                var result = await _neo4jService.ExecuteQueryAsync(query, parameters);
                _logger.LogInfo($"Neo4j query returned {result.Count} raw results");

                var searchHits = new List<SearchHit>();
                foreach (var record in result)
                {
                    if (record.TryGetValue("id", out var idObj) &&
                        record.TryGetValue("score", out var scoreObj))
                    {
                        var metadata = new Dictionary<string, string>();

                        if (record.TryGetValue("content", out var contentObj))
                            metadata["content"] = contentObj?.ToString() ?? "";
                        if (record.TryGetValue("category", out var categoryObj))
                            metadata["category"] = categoryObj?.ToString() ?? "";
                        if (record.TryGetValue("userId", out var userIdObj))
                            metadata["userId"] = userIdObj?.ToString() ?? "";

                        searchHits.Add(new SearchHit
                        {
                            Id = Guid.Parse(idObj.ToString()),
                            Score = Convert.ToSingle(scoreObj),
                            Metadata = metadata
                        });
                    }
                }

                _logger.LogInfo($"✅ Neo4j vector search returned {searchHits.Count} processed results");
                return searchHits;
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Failed to search vectors in Neo4j", ex);
                return new List<SearchHit>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteVectorAsync(Guid id, VectorScope scope = VectorScope.Core)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (!_isInitialized)
            {
                _logger.LogWarning("Attempted to delete vector, but Neo4j vector store is not initialized");
                return false;
            }

            try
            {
                var query = "MATCH (m:Memory {id: $id}) DELETE m";
                var parameters = new Dictionary<string, object> { { "id", id.ToString() } };

                await _neo4jService.ExecuteQueryAsync(query, parameters);

                _logger.LogInfo($"Successfully deleted vector for memory ID {id} from Neo4j");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete vector for memory ID {id} from Neo4j", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            var status = new Dictionary<string, object>
            {
                { "type", "Neo4j" },
                { "initialized", _isInitialized }
            };

            if (_isInitialized)
            {
                try
                {
                    // Get count of memory nodes
                    var query = "MATCH (m:Memory) RETURN count(m) as count";
                    var result = await _neo4jService.ExecuteQueryAsync(query, new Dictionary<string, object>());

                    if (result.Count > 0 && result[0].TryGetValue("count", out var countObj))
                    {
                        status["memoryCount"] = countObj;
                    }
                }
                catch (Exception ex)
                {
                    status["error"] = ex.Message;
                }
            }

            return status;
        }

        /// <inheritdoc/>
        public async Task<bool> HealthCheckAsync()
        {
            if (!_isInitialized)
                return false;

            try
            {
                var query = "RETURN 1 as test";
                var result = await _neo4jService.ExecuteQueryAsync(query, new Dictionary<string, object>());
                return result.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates the vector index for memory embeddings if it doesn't exist
        /// </summary>
        private async Task CreateVectorIndexAsync()
        {
            try
            {
                _logger.LogInfo("Checking if Neo4j vector index exists...");

                // Check if the new index exists
                var checkQuery = "SHOW INDEXES YIELD name WHERE name = 'memory_embedding_vector'";
                var result = await _neo4jService.ExecuteQueryAsync(checkQuery, new Dictionary<string, object>());

                _logger.LogInfo($"Index check returned {result.Count} results");

                if (result.Count == 0)
                {
                    _logger.LogInfo("Vector index does not exist, creating it...");

                    // First, drop the old index if it exists (different dimensions)
                    try
                    {
                        var dropQuery = "DROP INDEX memory_embeddings IF EXISTS";
                        await _neo4jService.ExecuteQueryAsync(dropQuery, new Dictionary<string, object>());
                        _logger.LogInfo("🗑️ Dropped old memory_embeddings index");
                    }
                    catch (Exception dropEx)
                    {
                        _logger.LogWarning($"Could not drop old index (may not exist): {dropEx.Message}");
                    }

                    // Create new vector index for memory embeddings with correct dimensions
                    var createQuery = $@"
                        CREATE VECTOR INDEX memory_embedding_vector IF NOT EXISTS
                        FOR (m:Memory) ON (m.embedding)
                        OPTIONS {{indexConfig: {{
                            `vector.dimensions`: {_vectorDimensions},
                            `vector.similarity_function`: 'cosine'
                        }}}}";

                    await _neo4jService.ExecuteQueryAsync(createQuery, new Dictionary<string, object>());
                    _logger.LogInfo("✅ Created Neo4j vector index for memory embeddings with correct dimensions");
                }
                else
                {
                    _logger.LogInfo("✅ Neo4j vector index for memory embeddings already exists");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to create/check Neo4j vector index: {ex.Message}", ex);
                // Don't fail initialization if index creation fails
            }
        }
    }
}
