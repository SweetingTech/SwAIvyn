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
        /// Gets the graph visualization data for a memory
        /// </summary>
        /// <param name="memoryId">Memory ID</param>
        /// <param name="depth">Relationship depth</param>
        /// <returns>Graph visualization data</returns>
        Task<GraphVisualizationData> GetGraphVisualizationAsync(Guid memoryId, int depth = 2);

        /// <summary>
        /// Gets the status of the brain graph service
        /// </summary>
        /// <returns>Status information</returns>
        Task<Dictionary<string, object>> GetStatusAsync();
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
        private readonly IVectorStore _vectorStore;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISimpleLoggerService _logger;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the BrainGraphService
        /// </summary>
        /// <param name="neo4jService">Neo4j service</param>
        /// <param name="vectorStore">Vector store</param>
        /// <param name="embeddingService">Embedding service</param>
        /// <param name="logger">Logger service</param>
        public BrainGraphService(
            INeo4jService neo4jService,
            IVectorStore vectorStore,
            IEmbeddingService embeddingService,
            ISimpleLoggerService logger)
        {
            _neo4jService = neo4jService;
            _vectorStore = vectorStore;
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

                // Initialize vector store
                await _vectorStore.InitializeAsync();

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

                // Store the embedding in the vector store
                var vectorStoreSuccess = await _vectorStore.StoreVectorAsync(id, embedding, metadata);

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

                // Search the vector store
                var hits = await _vectorStore.SearchAsync(queryEmbedding, limit);

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
                    // Get vector store status
                    var vectorStoreStatus = await _vectorStore.GetStatusAsync();
                    status["VectorStore"] = vectorStoreStatus;
                }
                catch (Exception ex)
                {
                    // Just log the error and continue
                    _logger.LogError($"Failed to get vector store status: {ex.Message}");
                    status["VectorStore"] = new Dictionary<string, object>
                    {
                        ["Error"] = ex.Message
                    };
                }

                status["Initialized"] = _isInitialized;

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get brain graph status", ex);
                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message
                };
            }
        }
    }
}
