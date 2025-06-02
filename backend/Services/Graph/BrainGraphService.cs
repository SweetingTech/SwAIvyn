using Microsoft.Extensions.Configuration;
using SwAIvyn.Services.VectorStore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text; // Added for StringBuilder
using System.Linq;  // Added for LINQ extension methods

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
        Task<bool> AddMemoryAsync(Guid id, string text, Dictionary<string, string>? metadata = null);

        /// <summary>
        /// Adds a relationship between two nodes
        /// </summary>
        /// <param name="sourceId">Source node ID</param>
        /// <param name="targetId">Target node ID</param>
        /// <param name="relationshipType">Relationship type</param>
        /// <param name="properties">Optional relationship properties</param>
        /// <returns>True if successful</returns>
        Task<bool> AddRelationshipAsync(Guid sourceId, Guid targetId, string relationshipType, Dictionary<string, object>? properties = null);

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
        Task<bool> AddConversationChunkAsync(Guid id, string text, Dictionary<string, string>? metadata = null);

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
    }

    /// <summary>
    /// Represents a search result from the brain graph
    /// </summary>
    public class BrainSearchResult
    {
        /// <summary>
        /// Gets or sets the search hit
        /// </summary>
        public SearchHit? Hit { get; set; }

        /// <summary>
        /// Gets or sets the related nodes
        /// </summary>
        public List<GraphNode> RelatedNodes { get; set; } = new List<GraphNode>();

        /// <summary>
        /// Gets or sets the relationships
        /// </summary>
        public List<GraphRelationship> Relationships { get; set; } = new List<GraphRelationship>();
    }

    /// <summary>
    /// Represents graph visualization data
    /// </summary>
    public class GraphVisualizationData
    {
        /// <summary>
        /// Gets or sets the nodes
        /// </summary>
        public List<GraphNode> Nodes { get; set; } = new List<GraphNode>();

        /// <summary>
        /// Gets or sets the relationships
        /// </summary>
        public List<GraphRelationship> Relationships { get; set; } = new List<GraphRelationship>();
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
        public async Task<bool> AddMemoryAsync(Guid id, string text, Dictionary<string, string>? metadata = null)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                // Generate embedding for the text
                var embedding = await _embeddingService.EmbedTextAsync(text);

                // Store the embedding in Neo4j vector store
                var vectorStoreSuccess = await _neo4jVectorStore.StoreVectorAsync(id, embedding, metadata);

                // Prepare parameters for the Cypher query
                var cypherParams = new Dictionary<string, object>
                {
                    { "id", id.ToString() }, // Used for MERGE matching
                    { "text", text }
                };

                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        // Attempt to parse timestamp string to DateTimeOffset for Neo4j compatibility
                        if (kvp.Key == "original_timestamp" && DateTimeOffset.TryParse(kvp.Value, out var dtoValue))
                        {
                            cypherParams[kvp.Key] = dtoValue;
                        }
                        else
                        {
                            cypherParams[kvp.Key] = kvp.Value;
                        }
                    }
                }

                var mergeQuery = new StringBuilder();
                mergeQuery.AppendLine("MERGE (m:Memory {id: $id})");

                // ON CREATE: Set all provided properties (text, and everything from metadata)
                var createSetParts = new List<string>();
                foreach(var paramKey in cypherParams.Keys)
                {
                    if (paramKey == "id") continue; // 'id' is in the MERGE pattern, not SET
                    createSetParts.Add($"m.{paramKey} = ${paramKey}");
                }

                if (createSetParts.Any())
                {
                    mergeQuery.AppendLine("ON CREATE SET " + string.Join(", ", createSetParts));
                }

                // ON MATCH: Update mutable properties.
                var matchSetParts = new List<string>();
                foreach(var paramKey in cypherParams.Keys)
                {
                    // Do not update 'id' (used in MERGE) or 'userId' (typically immutable) on MATCH
                    if (paramKey == "id" || paramKey == "userId") continue; 
                    matchSetParts.Add($"m.{paramKey} = ${paramKey}");
                }

                if (matchSetParts.Any())
                {
                    mergeQuery.AppendLine("ON MATCH SET " + string.Join(", ", matchSetParts));
                }

                mergeQuery.AppendLine("RETURN m.id as memoryId");

                try
                {
                    var result = await _neo4jService.ExecuteQueryAsync(mergeQuery.ToString(), cypherParams);
                    if (result != null && result.Any() && result[0].TryGetValue("memoryId", out var mergedIdObj) && mergedIdObj != null) {
                        _logger.LogInfo($"Successfully merged Memory node. ID: {mergedIdObj}");
                    } else {
                         _logger.LogInfo($"Successfully executed MERGE for Memory node ID: {id}. Neo4j query completed; specific ID not in return or result was empty/null.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to MERGE Neo4j node for memory {id}: {ex.Message}", ex);
                    // Maintaining original behavior: log and continue, success depends on vectorStoreSuccess
                }

                return vectorStoreSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to add memory {id}: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> AddRelationshipAsync(Guid sourceId, Guid targetId, string relationshipType, Dictionary<string, object>? properties = null)
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

                // Search Neo4j vector store for memories WITHOUT user filtering
                // Single-user application - no user filtering needed (like character cards)
                var hits = await _neo4jVectorStore.SearchAsync(queryEmbedding, null, limit);

                var results = new List<BrainSearchResult>();
                foreach (var hit in hits)
                {
                    var searchResult = new BrainSearchResult { Hit = hit };

                    try
                    {
                        // Get related nodes and relationships from Neo4j (this will be skipped if Neo4j is not available)
                        var cypher = "MATCH (m:Memory {id: $id})-[r]-(n) RETURN m, r, n, id(m) as m_neoId, labels(m) as m_labels, id(n) as n_neoId, labels(n) as n_labels, id(r) as r_neoId, type(r) as r_type, startNode(r).id as r_startId, endNode(r).id as r_endId";
                        var parameters = new Dictionary<string, object>
                        {
                            { "id", hit.Id.ToString() }
                        };

                        var queryResult = await _neo4jService.ExecuteQueryAsync(cypher, parameters);

                        // Process query results
                        foreach (var row in queryResult)
                        {
                            if (row.TryGetValue("n", out var nodeObj) && nodeObj is Dictionary<string, object> nodeData)
                            {
                                var node = new GraphNode
                                {
                                    Id = row.TryGetValue("n_neoId", out var nId) && nId != null ? nId.ToString()! : string.Empty,
                                    Labels = row.TryGetValue("n_labels", out var nLabels) && nLabels is List<object> nLabelList ? nLabelList.ConvertAll(l => l?.ToString() ?? string.Empty) : new List<string>(),
                                    Properties = nodeData
                                };
                                searchResult.RelatedNodes.Add(node);
                            }

                            if (row.TryGetValue("r", out var relObj) && relObj is Dictionary<string, object> relData)
                            {
                                var rel = new GraphRelationship
                                {
                                    Id = row.TryGetValue("r_neoId", out var rId) && rId != null ? rId.ToString()! : string.Empty,
                                    Type = row.TryGetValue("r_type", out var rType) && rType != null ? rType.ToString()! : string.Empty,
                                    StartNodeId = row.TryGetValue("r_startId", out var rStartId) && rStartId != null ? rStartId.ToString()! : string.Empty,
                                    EndNodeId = row.TryGetValue("r_endId", out var rEndId) && rEndId != null ? rEndId.ToString()! : string.Empty,
                                    Properties = relData
                                };
                                searchResult.Relationships.Add(rel);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Just log the error and continue
                        _logger.LogError($"Failed to get Neo4j data for memory {hit.Id}: {ex.Message}");
                    }
                    results.Add(searchResult);
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
            
            var vizData = new GraphVisualizationData();

            try
            {
                // Get graph visualization data from Neo4j (this will be skipped if Neo4j is not available)
                var cypher = @"
                    MATCH path = (m:Memory {id: $id})-[*1..$depth]-(n)
                    WITH nodes(path) as pathNodes, relationships(path) as pathRels
                    UNWIND pathNodes as node
                    UNWIND pathRels as rel
                    RETURN DISTINCT 
                           node, id(node) as nodeId, labels(node) as nodeLabels, 
                           rel,  id(rel) as relId, type(rel) as relType, startNode(rel).id as relStartNodeId, endNode(rel).id as relEndNodeId";
                var parameters = new Dictionary<string, object>
                {
                    { "id", memoryId.ToString() },
                    { "depth", depth }
                };

                var queryResult = await _neo4jService.ExecuteQueryAsync(cypher, parameters);
                var uniqueNodes = new Dictionary<string, GraphNode>();
                var uniqueRels = new Dictionary<string, GraphRelationship>();

                // Process query results
                foreach (var row in queryResult)
                {
                    if (row.TryGetValue("node", out var nodeObj) && nodeObj is Dictionary<string, object> nodeData &&
                        row.TryGetValue("nodeId", out var nodeIdObj) && nodeIdObj != null)
                    {
                        string nodeId = nodeIdObj.ToString()!;
                        if (!uniqueNodes.ContainsKey(nodeId))
                        {
                            var node = new GraphNode
                            {
                                Id = nodeId,
                                Labels = row.TryGetValue("nodeLabels", out var nodeLabelsObj) && nodeLabelsObj is List<object> labelList ? labelList.ConvertAll(l => l?.ToString() ?? string.Empty) : new List<string>(),
                                Properties = nodeData
                            };
                            uniqueNodes[nodeId] = node;
                        }
                    }

                    if (row.TryGetValue("rel", out var relObj) && relObj is Dictionary<string, object> relData &&
                        row.TryGetValue("relId", out var relIdObj) && relIdObj != null)
                    {
                        string relId = relIdObj.ToString()!;
                        if (!uniqueRels.ContainsKey(relId))
                        {
                            var rel = new GraphRelationship
                            {
                                Id = relId,
                                Type = row.TryGetValue("relType", out var relTypeObj) && relTypeObj != null ? relTypeObj.ToString()! : string.Empty,
                                StartNodeId = row.TryGetValue("relStartNodeId", out var relStartIdObj) && relStartIdObj != null ? relStartIdObj.ToString()! : string.Empty,
                                EndNodeId = row.TryGetValue("relEndNodeId", out var relEndIdObj) && relEndIdObj != null ? relEndIdObj.ToString()! : string.Empty,
                                Properties = relData
                            };
                            uniqueRels[relId] = rel;
                        }
                    }
                }
                vizData.Nodes.AddRange(uniqueNodes.Values);
                vizData.Relationships.AddRange(uniqueRels.Values);
            }
            catch (Exception ex)
            {
                // Just log the error and continue
                _logger.LogError($"Failed to get Neo4j graph visualization for memory {memoryId}: {ex.Message}");
            }
            return vizData;
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

                // Search Neo4j vector store for conversation chunks WITHOUT user filtering
                // Single-user application - no user filtering needed (like character cards)
                var hits = await _neo4jVectorStore.SearchAsync(queryEmbedding, null, limit);

                var results = new List<BrainSearchResult>();
                foreach (var hit in hits)
                {
                    // Filter for conversation chunks only
                    if (hit.Metadata?.GetValueOrDefault("type") == "conversation")
                    {
                        var searchResult = new BrainSearchResult { Hit = hit };

                        try
                        {
                            // Get related nodes and relationships from Neo4j (this will be skipped if Neo4j is not available)
                            var nodeQuery = $"MATCH (n:ConversationChunk {{id: $nodeIdParam}}) RETURN n, id(n) as neoId, labels(n) as nodeLabels";
                            var nodeParams = new Dictionary<string, object> { { "nodeIdParam", hit.Id.ToString() } };
                            var nodeResults = await _neo4jService.ExecuteQueryAsync(nodeQuery, nodeParams);

                            if (nodeResults.Any() && nodeResults[0].TryGetValue("n", out var nodeObj) && nodeObj is Dictionary<string, object> nodeData)
                            {
                                searchResult.RelatedNodes.Add(new GraphNode
                                {
                                    Id = nodeResults[0].TryGetValue("neoId", out var neoIdObj) && neoIdObj != null ? neoIdObj.ToString()! : string.Empty,
                                    Labels = nodeResults[0].TryGetValue("nodeLabels", out var labelsObj) && labelsObj is List<object> labelList ? labelList.ConvertAll(l => l?.ToString() ?? string.Empty) : new List<string> { "ConversationChunk" },
                                    Properties = nodeData
                                });
                            }
                            // Note: This part currently only fetches the ConversationChunk node itself.
                            // If relationships for ConversationChunks are needed, a different/expanded query would be required here.
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to get Neo4j data for conversation chunk {hit.Id}: {ex.Message}", ex);
                        }
                        results.Add(searchResult);
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
        public async Task<bool> AddConversationChunkAsync(Guid id, string text, Dictionary<string, string>? metadata = null)
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
                _logger.LogInfo($"Getting all memory IDs for user {userId}");

                // Query Neo4j for all memory nodes (global memories for single-user app)
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
                        }
                    }
                }

                _logger.LogInfo($"Found {memoryIds.Count} memory IDs in Neo4j for user {userId}");
                return memoryIds;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get memory IDs for user {userId}", ex);
                // Return empty list instead of throwing to allow the sync status to continue
                return new List<Guid>();
            }
        }
    }
}
