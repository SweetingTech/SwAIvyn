using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SwAIvyn.Services.Graph
{
    /// <summary>
    /// Service for interacting with Neo4j graph database
    /// </summary>
    public class Neo4jService : INeo4jService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ISimpleLoggerService _logger;
        private readonly string _neo4jUri;
        private readonly string _neo4jUser;
        private readonly string _neo4jPassword;
        private readonly bool _isEmbedded;
        private bool _isInitialized = false;
        private bool _online = false;
        private Timer _reconnectTimer;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the Neo4jService
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger service</param>
        public Neo4jService(
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _httpClient = new HttpClient();
            _logger = logger;

            _neo4jUri = configuration["AppSettings:Neo4jUri"] ?? "http://localhost:7474";
            _neo4jUser = configuration["AppSettings:Neo4jUser"] ?? "neo4j";
            _neo4jPassword = configuration["AppSettings:Neo4jPassword"] ?? "password";
            _isEmbedded = configuration.GetValue<bool>("AppSettings:Neo4jEmbedded", true);

            // Log the configuration
            _logger.LogInfo($"Neo4j configuration: Uri={_neo4jUri}, User={_neo4jUser}, Embedded={_isEmbedded}");

            // Start the reconnect timer (check every 30 seconds)
            _reconnectTimer = new Timer(async _ => await CheckConnectionAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Neo4j service...");

                // If Neo4j is not embedded, we don't need to initialize it
                if (!_isEmbedded)
                {
                    _logger.LogInfo("Neo4j embedded mode is disabled. Skipping Neo4j initialization.");
                    _isInitialized = true;
                    return;
                }

                if (_isEmbedded)
                {
                    // For embedded Neo4j, we would start the embedded server here
                    // This is a placeholder for now
                    _logger.LogInfo("Starting embedded Neo4j server...");
                    // await StartEmbeddedServerAsync();
                }

                // Set up authentication for Neo4j HTTP API
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_neo4jUser}:{_neo4jPassword}"));
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

                // Test connection
                var status = await GetStatusAsync();
                if (status.ContainsKey("Error"))
                {
                    throw new Exception($"Failed to connect to Neo4j: {status["Error"]}");
                }

                _isInitialized = true;
                _logger.LogInfo("Neo4j service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Neo4j service", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<GraphNode> CreateNodeAsync(List<string> labels, Dictionary<string, object> properties)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var labelString = string.Join(":", labels);
                var query = $"CREATE (n:{labelString} $props) RETURN n";
                var parameters = new Dictionary<string, object>
                {
                    { "props", properties }
                };

                var result = await ExecuteQueryAsync(query, parameters);
                if (result.Count > 0 && result[0].ContainsKey("n"))
                {
                    var nodeData = result[0]["n"] as Dictionary<string, object>;
                    return new GraphNode
                    {
                        Id = nodeData["id"].ToString(),
                        Labels = labels,
                        Properties = properties
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create node", ex);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<GraphRelationship> CreateRelationshipAsync(string startNodeId, string endNodeId, string type, Dictionary<string, object> properties = null)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var query = "MATCH (a), (b) WHERE id(a) = $startId AND id(b) = $endId " +
                           $"CREATE (a)-[r:{type} $props]->(b) RETURN r";
                var parameters = new Dictionary<string, object>
                {
                    { "startId", startNodeId },
                    { "endId", endNodeId },
                    { "props", properties ?? new Dictionary<string, object>() }
                };

                var result = await ExecuteQueryAsync(query, parameters);
                if (result.Count > 0 && result[0].ContainsKey("r"))
                {
                    var relData = result[0]["r"] as Dictionary<string, object>;
                    return new GraphRelationship
                    {
                        Id = relData["id"].ToString(),
                        Type = type,
                        StartNodeId = startNodeId,
                        EndNodeId = endNodeId,
                        Properties = properties ?? new Dictionary<string, object>()
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create relationship", ex);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<GraphNode> GetNodeAsync(string id)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var query = "MATCH (n) WHERE id(n) = $id RETURN n, labels(n) as labels";
                var parameters = new Dictionary<string, object>
                {
                    { "id", id }
                };

                var result = await ExecuteQueryAsync(query, parameters);
                if (result.Count > 0 && result[0].ContainsKey("n"))
                {
                    var nodeData = result[0]["n"] as Dictionary<string, object>;
                    var labels = result[0]["labels"] as List<string>;
                    return new GraphNode
                    {
                        Id = id,
                        Labels = labels,
                        Properties = nodeData
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get node {id}", ex);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<List<GraphNode>> GetNodesByLabelAsync(string label, int limit = 100)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var query = $"MATCH (n:{label}) RETURN n, id(n) as id, labels(n) as labels LIMIT $limit";
                var parameters = new Dictionary<string, object>
                {
                    { "limit", limit }
                };

                var result = await ExecuteQueryAsync(query, parameters);
                var nodes = new List<GraphNode>();

                foreach (var row in result)
                {
                    var nodeData = row["n"] as Dictionary<string, object>;
                    var nodeId = row["id"].ToString();
                    var labels = row["labels"] as List<string>;
                    nodes.Add(new GraphNode
                    {
                        Id = nodeId,
                        Labels = labels,
                        Properties = nodeData
                    });
                }

                return nodes;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get nodes by label {label}", ex);
                return new List<GraphNode>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<GraphNode>> GetNodesByPropertyAsync(string propertyName, object propertyValue, int limit = 100)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var query = "MATCH (n) WHERE n.$propName = $propValue " +
                           "RETURN n, id(n) as id, labels(n) as labels LIMIT $limit";
                var parameters = new Dictionary<string, object>
                {
                    { "propName", propertyName },
                    { "propValue", propertyValue },
                    { "limit", limit }
                };

                var result = await ExecuteQueryAsync(query, parameters);
                var nodes = new List<GraphNode>();

                foreach (var row in result)
                {
                    var nodeData = row["n"] as Dictionary<string, object>;
                    var nodeId = row["id"].ToString();
                    var labels = row["labels"] as List<string>;
                    nodes.Add(new GraphNode
                    {
                        Id = nodeId,
                        Labels = labels,
                        Properties = nodeData
                    });
                }

                return nodes;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get nodes by property {propertyName}", ex);
                return new List<GraphNode>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<GraphRelationship>> GetRelationshipsByTypeAsync(string type, int limit = 100)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var query = $"MATCH ()-[r:{type}]->() RETURN r, id(r) as id, " +
                           "id(startNode(r)) as startId, id(endNode(r)) as endId LIMIT $limit";
                var parameters = new Dictionary<string, object>
                {
                    { "limit", limit }
                };

                var result = await ExecuteQueryAsync(query, parameters);
                var relationships = new List<GraphRelationship>();

                foreach (var row in result)
                {
                    var relData = row["r"] as Dictionary<string, object>;
                    var relId = row["id"].ToString();
                    var startId = row["startId"].ToString();
                    var endId = row["endId"].ToString();
                    relationships.Add(new GraphRelationship
                    {
                        Id = relId,
                        Type = type,
                        StartNodeId = startId,
                        EndNodeId = endId,
                        Properties = relData
                    });
                }

                return relationships;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get relationships by type {type}", ex);
                return new List<GraphRelationship>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query, Dictionary<string, object> parameters = null)
        {
            if (!_isInitialized && !query.Contains("dbms.cluster.overview"))
                await InitializeAsync();

            // If Neo4j is not embedded, return an empty result
            if (!_isEmbedded)
            {
                return new List<Dictionary<string, object>>();
            }

            try
            {
                var requestData = new
                {
                    statements = new[]
                    {
                        new
                        {
                            statement = query,
                            parameters = parameters ?? new Dictionary<string, object>(),
                            includeStats = true
                        }
                    }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{_neo4jUri}/db/data/transaction/commit", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);

                var results = new List<Dictionary<string, object>>();
                var resultList = responseObj["results"] as List<object>;
                if (resultList.Count > 0)
                {
                    var firstResult = resultList[0] as Dictionary<string, object>;
                    var data = firstResult["data"] as List<object>;
                    foreach (var row in data)
                    {
                        var rowDict = row as Dictionary<string, object>;
                        var rowData = new Dictionary<string, object>();
                        var rowList = rowDict["row"] as List<object>;
                        var columns = firstResult["columns"] as List<string>;
                        for (int i = 0; i < columns.Count; i++)
                        {
                            rowData[columns[i]] = rowList[i];
                        }
                        results.Add(rowData);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute query: {query}", ex);
                return new List<Dictionary<string, object>>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteNodeAsync(string id)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var query = "MATCH (n) WHERE id(n) = $id DETACH DELETE n";
                var parameters = new Dictionary<string, object>
                {
                    { "id", id }
                };

                await ExecuteQueryAsync(query, parameters);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete node {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteRelationshipAsync(string id)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var query = "MATCH ()-[r]->() WHERE id(r) = $id DELETE r";
                var parameters = new Dictionary<string, object>
                {
                    { "id", id }
                };

                await ExecuteQueryAsync(query, parameters);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete relationship {id}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            try
            {
                var status = new Dictionary<string, object>();

                // If Neo4j is not embedded, return a default status
                if (!_isEmbedded)
                {
                    status["Connected"] = false;
                    status["Mode"] = "Remote";
                    status["Uri"] = _neo4jUri;
                    status["Message"] = "Neo4j embedded mode is disabled";
                    return status;
                }

                try
                {
                    var query = "CALL dbms.cluster.overview()";
                    var result = await ExecuteQueryAsync(query);
                    status["Connected"] = true;
                    status["Mode"] = "Embedded";
                    status["Uri"] = _neo4jUri;
                }
                catch (Exception ex)
                {
                    status["Connected"] = false;
                    status["Error"] = ex.Message;
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get Neo4j status", ex);
                return new Dictionary<string, object>
                {
                    ["Connected"] = false,
                    ["Error"] = ex.Message
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> HealthCheckAsync()
        {
            // If Neo4j is not embedded, we don't need to check its health
            if (!_isEmbedded)
            {
                return true;
            }

            return await PingAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> PingAsync()
        {
            try
            {
                // If Neo4j is not embedded, we don't need to check its health
                if (!_isEmbedded)
                {
                    return true;
                }

                // Try a simple query to check Neo4j connection
                await ExecuteQueryAsync("RETURN 1 AS ok");
                _online = true;
            }
            catch
            {
                _online = false;
            }
            return _online;
        }

        /// <inheritdoc/>
        public bool IsOnline => _online;

        /// <summary>
        /// Checks the connection to Neo4j and updates the online status
        /// </summary>
        private async Task CheckConnectionAsync()
        {
            try
            {
                // Only check if Neo4j is embedded
                if (!_isEmbedded)
                {
                    return;
                }

                // Try to ping Neo4j
                await PingAsync();

                // If we get here, Neo4j is online
                if (_online)
                {
                    _logger.LogInfo("Neo4j connection restored");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to check Neo4j connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the Neo4j service
        /// </summary>
        public void Dispose()
        {
            _reconnectTimer?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
