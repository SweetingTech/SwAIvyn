using Microsoft.Extensions.Configuration;
using SwAIvyn.Services.VectorStore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services.Graph
{
    /// <summary>
    /// Interface for the brain graph service
    /// </summary>
    public interface IBrainGraphService
    {
        /// <summary>
        /// Initializes the brain graph service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Adds a memory to the brain graph
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <param name="text">Memory text</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>True if successful</returns>
        Task<bool> AddMemoryAsync(Guid id, string text, Dictionary<string, string> metadata = null);

        /// <summary>
        /// Adds a relationship between two nodes
        /// </summary>
        /// <param name="sourceId">Source node ID</param>
        /// <param name="targetId">Target node ID</param>
        /// <param name="relationshipType">Relationship type</param>
        /// <param name="properties">Optional relationship properties</param>
        /// <returns>True if successful</returns>
        Task<bool> AddRelationshipAsync(Guid sourceId, Guid targetId, string relationshipType, Dictionary<string, object> properties = null);

        /// <summary>
        /// Searches the brain graph for related memories
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="limit">Maximum number of results</param>
        /// <returns>List of search hits with relationships</returns>
        Task<List<BrainSearchResult>> SearchAsync(string query, int limit = 10);

        /// <summary>
        /// Searches for relevant conversation chunks
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="limit">Maximum number of results</param>
        /// <returns>List of conversation search results</returns>
        Task<List<BrainSearchResult>> SearchConversationAsync(string query, int limit = 10);

        /// <summary>
        /// Adds a conversation chunk to the brain graph
        /// </summary>
        /// <param name="id">Conversation chunk ID</param>
        /// <param name="text">Conversation text</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>True if successful</returns>
        Task<bool> AddConversationChunkAsync(Guid id, string text, Dictionary<string, string> metadata = null);

        /// <summary>
        /// Gets the graph visualization data for a memory
        /// </summary>
        /// <param name="memoryId">Memory ID</param>
        /// <param name="depth">Relationship depth</param>
        /// <returns>Graph visualization data</returns>
        Task<GraphVisualizationData> GetGraphVisualizationAsync(Guid memoryId, int depth = 2);

        /// <summary>
        /// Deletes a memory from the brain graph
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteMemoryAsync(Guid id);        /// <summary>
        /// Gets the status of the brain graph service
        /// </summary>
        /// <returns>Status information</returns>
        Task<Dictionary<string, object>> GetStatusAsync();

        /// <summary>
        /// Gets all memory IDs for a specific user from Neo4j
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of memory IDs</returns>
        Task<List<Guid>> GetAllMemoryIdsAsync(Guid userId);

        /// <summary>
        /// Simple search that returns memory content as strings
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="limit">Maximum number of results</param>
        /// <returns>List of memory content strings</returns>
        Task<List<string>> SearchMemoriesAsync(string query, int limit = 10);
    }

    /// <summary>
    /// Represents a search result from the brain graph
    /// </summary>
    public class BrainSearchResult
    {
        /// <summary>
        /// Gets or sets the search hit
        /// </summary>
        public SearchHit Hit { get; set; }

        /// <summary>
        /// Gets or sets the related nodes
        /// </summary>
        public List<GraphNode> RelatedNodes { get; set; }

        /// <summary>
        /// Gets or sets the relationships
        /// </summary>
        public List<GraphRelationship> Relationships { get; set; }
    }

    /// <summary>
    /// Represents graph visualization data
    /// </summary>
    public class GraphVisualizationData
    {
        /// <summary>
        /// Gets or sets the nodes
        /// </summary>
        public List<GraphNode> Nodes { get; set; }

        /// <summary>
        /// Gets or sets the relationships
        /// </summary>
        public List<GraphRelationship> Relationships { get; set; }
    }

    /// <summary>
    /// Service for managing the brain graph
    /// </summary>
    public class BrainGraphService : IBrainGraphService
    {
        private readonly INeo4jService _neo4jService;
        private readonly Neo4jVectorStore _neo4jVectorStore;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISimpleLoggerService _logger;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the BrainGraphService
        /// </summary>
        /// <param name="neo4jService">Neo4j service</param>
        /// <param name="neo4jVectorStore">Neo4j vector store for memories and conversations</param>
        /// <param name="embeddingService">Embedding service</param>
        /// <param name="logger">Logger service</param>
        public BrainGraphService(
            INeo4jService neo4jService,
            Neo4jVectorStore neo4jVectorStore,
            IEmbeddingService embeddingService,
            ISimpleLoggerService logger)
        {
            _neo4jService = neo4jService;
            _neo4jVectorStore = neo4jVectorStore;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing brain graph service...");

                // Initialize Neo4j service (this will be skipped if Neo4j is not embedded)
                await _neo4jService.InitializeAsync();

                // Initialize Neo4j vector store for memories and conversations
                await _neo4jVectorStore.InitializeAsync();

                _isInitialized = true;
                _logger.LogInfo("Brain graph service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize brain graph service", ex);
                // Don't throw the exception, just log it
                _isInitialized = true; // Set to true anyway so we can continue
            }
        }

        /// <inheritdoc/>
        public async Task<bool> AddMemoryAsync(Guid id, string text, Dictionary<string, string> metadata = null)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                // Generate embedding for the text
                var embedding = await _embeddingService.EmbedTextAsync(text);

                // Store the embedding in Neo4j vector store
                var vectorStoreSuccess = await _neo4jVectorStore.StoreVectorAsync(id, embedding, metadata);

                try
                {
                    // Create a node in Neo4j (this will be skipped if Neo4j is not available)
                    var properties = new Dictionary<string, object>
                    {
                        { "id", id.ToString() },
                        { "text", text }
                    };

                    // Add metadata to properties
                    if (metadata != null)
                    {
                        foreach (var kvp in metadata)
                        {
                            properties[kvp.Key] = kvp.Value;
                        }
                    }

                    await _neo4jService.CreateNodeAsync(new List<string> { "Memory" }, properties);
                }
                catch (Exception ex)
                {
                    // Just log the error and continue
                    _logger.LogError($"Failed to create Neo4j node for memory {id}: {ex.Message}");
                }

                return vectorStoreSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to add memory {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> AddRelationshipAsync(Guid sourceId, Guid targetId, string relationshipType, Dictionary<string, object> properties = null)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                try
                {
                    // Create a relationship in Neo4j (this will be skipped if Neo4j is not available)
                    await _neo4jService.CreateRelationshipAsync(
                        sourceId.ToString(),
                        targetId.ToString(),
                        relationshipType,
                        properties);
                }
                catch (Exception ex)
                {
                    // Just log the error and continue
                    _logger.LogError($"Failed to create Neo4j relationship between {sourceId} and {targetId}: {ex.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to add relationship between {sourceId} and {targetId}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<BrainSearchResult>> SearchAsync(string query, int limit = 10)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                // Generate embedding for the query
                var queryEmbedding = await _embeddingService.EmbedTextAsync(query);

                // Search Neo4j vector store for memories
                var hits = await _neo4jVectorStore.SearchAsync(queryEmbedding, limit);

                var results = new List<BrainSearchResult>();
                foreach (var hit in hits)
                {
                    var relatedNodes = new List<GraphNode>();
                    var relationships = new List<GraphRelationship>();

                    try
                    {
                        // Get related nodes and relationships from Neo4j (this will be skipped if Neo4j is not available)
                        var cypher = "MATCH (m:Memory {id: $id})-[r]-(n) RETURN m, r, n";
                        var parameters = new Dictionary<string, object>
                        {
                            { "id", hit.Id.ToString() }
                        };

                        var queryResult = await _neo4jService.ExecuteQueryAsync(cypher, parameters);

                        // Process query results
                        foreach (var row in queryResult)
                        {
                            if (row.ContainsKey("n"))
                            {
                                var nodeData = row["n"] as Dictionary<string, object>;
                                var node = new GraphNode
                                {
                                    Id = nodeData["id"].ToString(),
                                    Properties = nodeData
                                };
                                relatedNodes.Add(node);
                            }

                            if (row.ContainsKey("r"))
                            {
                                var relData = row["r"] as Dictionary<string, object>;
                                var rel = new GraphRelationship
                                {
                                    Id = relData["id"].ToString(),
                                    Type = relData["type"].ToString(),
                                    StartNodeId = relData["startId"].ToString(),
                                    EndNodeId = relData["endId"].ToString(),
                                    Properties = relData
                                };
                                relationships.Add(rel);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Just log the error and continue
                        _logger.LogError($"Failed to get Neo4j data for memory {hit.Id}: {ex.Message}");
                    }

                    results.Add(new BrainSearchResult
                    {
                        Hit = hit,
                        RelatedNodes = relatedNodes,
                        Relationships = relationships
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to search brain graph", ex);
                return new List<BrainSearchResult>();
            }
        }

        /// <inheritdoc/>
        public async Task<GraphVisualizationData> GetGraphVisualizationAsync(Guid memoryId, int depth = 2)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var nodes = new List<GraphNode>();
                var relationships = new List<GraphRelationship>();

                try
                {
                    // Get graph visualization data from Neo4j (this will be skipped if Neo4j is not available)
                    var cypher = @"
                        MATCH path = (m:Memory {id: $id})-[*1..$depth]-(n)
                        RETURN nodes(path) as nodes, relationships(path) as rels";
                    var parameters = new Dictionary<string, object>
                    {
                        { "id", memoryId.ToString() },
                        { "depth", depth }
                    };

                    var queryResult = await _neo4jService.ExecuteQueryAsync(cypher, parameters);

                    // Process query results
                    foreach (var row in queryResult)
                    {
                        if (row.ContainsKey("nodes"))
                        {
                            var nodesList = row["nodes"] as List<Dictionary<string, object>>;
                            foreach (var nodeData in nodesList)
                            {
                                var node = new GraphNode
                                {
                                    Id = nodeData["id"].ToString(),
                                    Properties = nodeData
                                };
                                nodes.Add(node);
                            }
                        }

                        if (row.ContainsKey("rels"))
                        {
                            var relsList = row["rels"] as List<Dictionary<string, object>>;
                            foreach (var relData in relsList)
                            {
                                var rel = new GraphRelationship
                                {
                                    Id = relData["id"].ToString(),
                                    Type = relData["type"].ToString(),
                                    StartNodeId = relData["startId"].ToString(),
                                    EndNodeId = relData["endId"].ToString(),
                                    Properties = relData
                                };
                                relationships.Add(rel);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Just log the error and continue
                    _logger.LogError($"Failed to get Neo4j graph visualization for memory {memoryId}: {ex.Message}");
                }

                return new GraphVisualizationData
                {
                    Nodes = nodes,
                    Relationships = relationships
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get graph visualization for memory {memoryId}", ex);
                return new GraphVisualizationData
                {
                    Nodes = new List<GraphNode>(),
                    Relationships = new List<GraphRelationship>()
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteMemoryAsync(Guid id)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                // Delete from Neo4j vector store
                var vectorStoreSuccess = await _neo4jVectorStore.DeleteVectorAsync(id);

                try
                {
                    // Delete from Neo4j (this will be skipped if Neo4j is not available)
                    var cypher = "MATCH (m:Memory {id: $id}) DETACH DELETE m";
                    var parameters = new Dictionary<string, object>
                    {
                        { "id", id.ToString() }
                    };

                    await _neo4jService.ExecuteQueryAsync(cypher, parameters);
                }
                catch (Exception ex)
                {
                    // Just log the error and continue
                    _logger.LogError($"Failed to delete Neo4j node for memory {id}: {ex.Message}");
                }

                return vectorStoreSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete memory {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<BrainSearchResult>> SearchConversationAsync(string query, int limit = 10)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                // Generate embedding for the query
                var queryEmbedding = await _embeddingService.EmbedTextAsync(query);

                // Search Neo4j vector store for conversation chunks
                var hits = await _neo4jVectorStore.SearchAsync(queryEmbedding, limit);

                var results = new List<BrainSearchResult>();
                foreach (var hit in hits)
                {
                    // Filter for conversation chunks only
                    if (hit.Metadata?.GetValueOrDefault("type") == "conversation")
                    {
                        var relatedNodes = new List<GraphNode>();
                        var relationships = new List<GraphRelationship>();

                        try
                        {
                            // Get related nodes and relationships from Neo4j (this will be skipped if Neo4j is not available)
                            var nodeQuery = $"MATCH (n:ConversationChunk {{id: '{hit.Id}'}}) RETURN n";
                            var nodeResults = await _neo4jService.ExecuteQueryAsync(nodeQuery);

                            if (nodeResults.Count > 0)
                            {
                                var nodeData = (Dictionary<string, object>)nodeResults[0]["n"];
                                relatedNodes.Add(new GraphNode
                                {
                                    Id = nodeData["id"].ToString(),
                                    Labels = new List<string> { "ConversationChunk" },
                                    Properties = nodeData
                                });
                            }

                            // Get relationships
                            var relationshipQuery = $"MATCH (n:ConversationChunk {{id: '{hit.Id}'}})-[r]-(m) RETURN r, m";
                            var relationshipResults = await _neo4jService.ExecuteQueryAsync(relationshipQuery);

                            foreach (var result in relationshipResults)
                            {
                                var relationshipData = (Dictionary<string, object>)result["r"];
                                var relatedNodeData = (Dictionary<string, object>)result["m"];

                                relationships.Add(new GraphRelationship
                                {
                                    Id = relationshipData.GetValueOrDefault("id", Guid.NewGuid().ToString()).ToString(),
                                    Type = relationshipData.GetValueOrDefault("type", "RELATED").ToString(),
                                    StartNodeId = hit.Id.ToString(),
                                    EndNodeId = relatedNodeData["id"].ToString(),
                                    Properties = relationshipData
                                });

                                relatedNodes.Add(new GraphNode
                                {
                                    Id = relatedNodeData["id"].ToString(),
                                    Labels = new List<string> { relatedNodeData.GetValueOrDefault("type", "Unknown").ToString() },
                                    Properties = relatedNodeData
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            // Just log the error and continue
                            _logger.LogError($"Failed to get Neo4j data for conversation chunk {hit.Id}: {ex.Message}");
                        }

                        results.Add(new BrainSearchResult
                        {
                            Hit = hit,
                            RelatedNodes = relatedNodes,
                            Relationships = relationships
                        });
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to search conversation chunks", ex);
                return new List<BrainSearchResult>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> AddConversationChunkAsync(Guid id, string text, Dictionary<string, string> metadata = null)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                // Generate embedding for the text
                var embedding = await _embeddingService.EmbedTextAsync(text);

                // Add type metadata to identify as conversation chunk
                var conversationMetadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>())
                {
                    ["type"] = "conversation"
                };

                // Store the embedding in Neo4j vector store
                var vectorStoreSuccess = await _neo4jVectorStore.StoreVectorAsync(id, embedding, conversationMetadata);

                try
                {
                    // Create a node in Neo4j (this will be skipped if Neo4j is not available)
                    var properties = new Dictionary<string, object>
                    {
                        { "id", id.ToString() },
                        { "text", text },
                        { "type", "conversation" }
                    };

                    // Add metadata to properties
                    if (metadata != null)
                    {
                        foreach (var kvp in metadata)
                        {
                            properties[kvp.Key] = kvp.Value;
                        }
                    }

                    await _neo4jService.CreateNodeAsync(new List<string> { "ConversationChunk" }, properties);
                }
                catch (Exception ex)
                {
                    // Just log the error and continue
                    _logger.LogError($"Failed to create Neo4j node for conversation chunk {id}: {ex.Message}");
                }

                return vectorStoreSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to add conversation chunk {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            try
            {
                var status = new Dictionary<string, object>();

                try
                {
                    // Get Neo4j status (this will be skipped if Neo4j is not available)
                    var neo4jStatus = await _neo4jService.GetStatusAsync();
                    status["Neo4j"] = neo4jStatus;
                }
                catch (Exception ex)
                {
                    // Just log the error and continue
                    _logger.LogError($"Failed to get Neo4j status: {ex.Message}");
                    status["Neo4j"] = new Dictionary<string, object>
                    {
                        ["Connected"] = false,
                        ["Error"] = ex.Message
                    };
                }

                try
                {
                    // Get Neo4j vector store status
                    var vectorStoreStatus = await _neo4jVectorStore.GetStatusAsync();
                    status["Neo4jVectorStore"] = vectorStoreStatus;
                }
                catch (Exception ex)
                {
                    // Just log the error and continue
                    _logger.LogError($"Failed to get Neo4j vector store status: {ex.Message}");
                    status["Neo4jVectorStore"] = new Dictionary<string, object>
                    {
                        ["Error"] = ex.Message
                    };
                }

                // Get memory count
                try
                {
                    var memoryCountQuery = "MATCH (m:Memory) RETURN count(m) as memoryCount";
                    var memoryCountResult = await _neo4jService.ExecuteQueryAsync(memoryCountQuery);

                    if (memoryCountResult.Count > 0 && memoryCountResult[0].ContainsKey("memoryCount"))
                    {
                        status["memoryCount"] = memoryCountResult[0]["memoryCount"];
                    }
                    else
                    {
                        status["memoryCount"] = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to get memory count: {ex.Message}");
                    status["memoryCount"] = 0;
                }

                status["Initialized"] = _isInitialized;

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get brain graph status", ex);                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message
                };
            }
        }

        /// <inheritdoc/>
        public async Task<List<Guid>> GetAllMemoryIdsAsync(Guid userId)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                _logger.LogInfo($"Getting all memory IDs from Neo4j (single-user app)");

                // Query Neo4j for ALL memory nodes (single-user application)
                // Since this is a single-user app, we get all Memory nodes regardless of userId
                var query = "MATCH (m:Memory) RETURN m.id as memoryId";
                var parameters = new Dictionary<string, object>();

                var result = await _neo4jService.ExecuteQueryAsync(query, parameters);
                var memoryIds = new List<Guid>();

                foreach (var record in result)
                {
                    if (record.TryGetValue("memoryId", out var idValue) && idValue != null)
                    {
                        if (Guid.TryParse(idValue.ToString(), out var memoryId))
                        {
                            memoryIds.Add(memoryId);
                            _logger.LogInfo($"Found memory ID: {memoryId}");
                        }
                        else
                        {
                            _logger.LogWarning($"Could not parse memory ID: {idValue}");
                        }
                    }
                }

                _logger.LogInfo($"Found {memoryIds.Count} total memory IDs in Neo4j");
                return memoryIds;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get memory IDs from Neo4j: {ex.Message}", ex);
                // Return empty list instead of throwing to allow the sync status to continue
                return new List<Guid>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> SearchMemoriesAsync(string query, int limit = 10)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                // Generate embedding for the query
                var queryEmbedding = await _embeddingService.EmbedTextAsync(query);

                // Search Neo4j vector store for memories
                var hits = await _neo4jVectorStore.SearchAsync(queryEmbedding, limit);

                // Extract just the text content from the search hits
                var results = new List<string>();
                foreach (var hit in hits)
                {
                    if (hit.Metadata != null && hit.Metadata.TryGetValue("content", out var content) && !string.IsNullOrEmpty(content))
                    {
                        results.Add(content);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to search memories", ex);
                return new List<string>();
            }
        }
    }
}
