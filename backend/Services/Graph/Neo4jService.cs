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
        private readonly IConfigurationService _configurationService;
        private readonly ISimpleLoggerService _logger;
        private readonly IConfiguration _configuration;
        private string _neo4jUri;
        private string _neo4jUser;
        private string _neo4jPassword;
        private readonly bool _isEmbedded;
        private bool _isInitialized = false;
        private bool _online = false;
        private Timer _reconnectTimer;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the Neo4jService
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="configurationService">Configuration service</param>
        /// <param name="logger">Logger service</param>
        public Neo4jService(
            IConfiguration configuration,
            IConfigurationService configurationService,
            ISimpleLoggerService logger)
        {
            _httpClient = new HttpClient();
            _configurationService = configurationService;
            _logger = logger;
            _configuration = configuration;

            // These will be updated in InitializeAsync with values from configuration
            _neo4jUri = _configurationService.GetNeo4jUri();
            _neo4jUser = configuration["AppSettings:Neo4jUser"] ?? "neo4j";
            _neo4jPassword = configuration["AppSettings:Neo4jPassword"] ?? "password";
            _isEmbedded = configuration.GetValue<bool>("AppSettings:Neo4jEmbedded", true);

            // Log the initial configuration
            _logger.LogInfo($"Initial Neo4j configuration: Uri={_neo4jUri}, User={_neo4jUser}, Embedded={_isEmbedded}");

            // Start the reconnect timer (check every 30 seconds)
            _reconnectTimer = new Timer(async _ => await CheckConnectionAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Neo4j service...");

                // Get the latest Neo4j settings from configuration
                _neo4jUri = _configurationService.GetNeo4jUri();

                _logger.LogInfo($"Using Neo4j settings from configuration: Uri={_neo4jUri}");

                // Set up authentication for Neo4j HTTP API
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_neo4jUser}:{_neo4jPassword}"));
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

                // Initialization is complete once headers are set, regardless of embedded status.
                // Actual connectivity is checked by PingAsync or GetStatusAsync.
                _isInitialized = true; 
                _logger.LogInfo("Neo4j service basic initialization completed (HTTP client configured).");

                if (_isEmbedded)
                {
                    // Logic related to embedded server (if any beyond Neo4jRuntimeService) would go here.
                    // For now, Neo4jRuntimeService handles startup.
                    _logger.LogInfo("Embedded mode is true. Neo4jRuntimeService is expected to handle server startup.");
                }
                else
                {
                    _logger.LogInfo("Embedded mode is false. Will connect to external Neo4j instance.");
                }
                
                // Initial connectivity test is removed from here to prevent recursion.
                // It should be done externally after InitializeAsync completes, e.g., in Program.cs or by first query.
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
                var status = new Dictionary<string, object>
                {
                    ["Mode"] = _isEmbedded ? "Embedded" : "Remote",
                    ["Uri"] = _neo4jUri,
                    ["Connected"] = false // Assume not connected until proven
                };

                try
                {
                    // A simple query to check connectivity.
                    // Using "CALL dbms.components()" as it's generally available and gives basic info.
                    // Using "RETURN 1" is even simpler for just a ping.
                    var query = "RETURN 1 AS connection_check"; 
                    var result = await ExecuteQueryAsync(query); // ExecuteQueryAsync calls InitializeAsync if not initialized.

                    if (result != null && result.Count > 0 && result[0].ContainsKey("connection_check"))
                    {
                        status["Connected"] = true;
                        _online = true; // Update internal online status
                        _logger.LogInfo($"Successfully connected to Neo4j ({status["Mode"]}) at {_neo4jUri}.");
                    }
                    else
                    {
                        status["Error"] = "Connection check query returned no result or unexpected result.";
                        _online = false;
                         _logger.LogWarning($"Neo4j ({status["Mode"]}) at {_neo4jUri} connection check failed or returned unexpected result.");
                    }
                }
                catch (Exception ex)
                {
                    status["Error"] = ex.Message;
                    _online = false;
                    _logger.LogError($"Failed to connect to Neo4j ({status["Mode"]}) at {_neo4jUri}: {ex.Message}", ex);
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
            // Health check should always try to ping the configured Neo4j instance.
            return await PingAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> PingAsync()
        {
            string originalNeo4jUri = _neo4jUri; // Store original URI
            try
            {
                // Ensure _neo4jUri is up-to-date from configuration for each ping
                // This allows dynamic changes to appsettings to be picked up by new pings,
                // though _configurationService might cache. More robust would be re-fetching if ISettingsProvider used.
                _neo4jUri = _configurationService.GetNeo4jUri();
                if(originalNeo4jUri != _neo4jUri) {
                     _logger.LogInfo($"Neo4j URI changed from {originalNeo4jUri} to {_neo4jUri}. Re-authenticating HTTP client.");
                     var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_neo4jUser}:{_neo4jPassword}"));
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                }


                // Try a simple query to check Neo4j connection
                var query = "RETURN 1 AS ok";
                // Pass a special flag or use a different method for ping to avoid InitializeAsync loop if it's part of health check
                var result = await ExecuteQueryAsync(query, null); 
                _online = (result != null && result.Count > 0 && result[0].ContainsKey("ok"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Ping failed for Neo4j at {_neo4jUri}: {ex.Message}");
                _online = false;
            }
            return _online;
        }

        /// <inheritdoc/>
        public bool IsOnline => _online;

        /// <summary>
        /// Checks the connection to Neo4j and updates the online status. Called by a timer.
        /// </summary>
        private async Task CheckConnectionAsync()
        {
            _logger.LogInfo($"Timer: Pinging Neo4j at {_neo4jUri} (IsEmbedded: {_isEmbedded})");
            bool currentStatus = await PingAsync();
            if (currentStatus)
            {
                if (!_online) // If it was previously offline
                {
                    _logger.LogInfo($"Neo4j connection to {_neo4jUri} RESTORED.");
                }
                _online = true;
            }
            else
            {
                if (_online) // If it was previously online
                {
                    _logger.LogWarning($"Neo4j connection to {_neo4jUri} LOST.");
                }
                _online = false;
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
