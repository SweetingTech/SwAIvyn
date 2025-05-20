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
    /// Service for interacting with a Neo4j graph database over the HTTP transactional endpoint.
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

        private bool _isInitialized;
        private bool _online;
        private readonly Timer _reconnectTimer;
        private readonly object _lockObject = new();

        public Neo4jService(
            IConfiguration configuration,
            IConfigurationService configurationService,
            ISimpleLoggerService logger)
        {
            _httpClient = new HttpClient();
            _configurationService = configurationService;
            _logger = logger;
            _configuration = configuration;

            _neo4jUri = _configurationService.GetNeo4jUri();
            _neo4jUser = configuration["AppSettings:Neo4jUser"] ?? "neo4j";
            _neo4jPassword = configuration["AppSettings:Neo4jPassword"] ?? "password";
            _isEmbedded = configuration.GetValue<bool>("AppSettings:Neo4jEmbedded", true);

            _logger.LogInfo($"Initial Neo4j configuration -> Uri={_neo4jUri}, User={_neo4jUser}, Embedded={_isEmbedded}");

            // Ping every 30 s to keep status fresh
            _reconnectTimer = new Timer(async _ => await CheckConnectionAsync(),
                                        null,
                                        TimeSpan.FromSeconds(30),
                                        TimeSpan.FromSeconds(30));
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Neo4j service");

                // Refresh settings
                _neo4jUri      = _configurationService.GetNeo4jUri();
                _neo4jUser     = _configuration["AppSettings:Neo4jUser"]     ?? "neo4j";
                _neo4jPassword = _configuration["AppSettings:Neo4jPassword"] ?? "password";

                if (_isEmbedded)
                    _logger.LogInfo("Neo4jService running in EMBEDDED mode. Neo4jRuntimeService will start the server.");
                else
                    _logger.LogInfo($"Neo4jService running in EXTERNAL mode. Connecting to {_neo4jUri} as {_neo4jUser}.");

                // Basic‑auth header
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_neo4jUser}:{_neo4jPassword}"));
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

                _isInitialized = true;
                _logger.LogInfo("Neo4j HTTP client configured");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Neo4j service", ex);
                throw;
            }
        }

        /* ------------------------------------------------------------------
           CRUD helpers
        ------------------------------------------------------------------ */

        public async Task<GraphNode> CreateNodeAsync(List<string> labels, Dictionary<string, object> properties)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var labelString = string.Join(":", labels);
                var query = $"CREATE (n:{labelString} $props) RETURN n";
                var parameters = new Dictionary<string, object> { ["props"] = properties };

                var result = await ExecuteQueryAsync(query, parameters);
                if (result.Count > 0 && result[0].TryGetValue("n", out var nObj))
                {
                    var nodeData = (Dictionary<string, object>)nObj;
                    return new GraphNode
                    {
                        Id         = nodeData["id"].ToString(),
                        Labels     = labels,
                        Properties = properties
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create node", ex);
            }

            return null;
        }

        public async Task<GraphRelationship> CreateRelationshipAsync(
            string startNodeId,
            string endNodeId,
            string type,
            Dictionary<string, object> properties = null)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var query = "MATCH (a), (b) WHERE id(a) = $startId AND id(b) = $endId " +
                            $"CREATE (a)-[r:{type} $props]->(b) RETURN r";
                var parameters = new Dictionary<string, object>
                {
                    ["startId"] = startNodeId,
                    ["endId"]   = endNodeId,
                    ["props"]   = properties ?? new Dictionary<string, object>()
                };

                var result = await ExecuteQueryAsync(query, parameters);
                if (result.Count > 0 && result[0].TryGetValue("r", out var rObj))
                {
                    var relData = (Dictionary<string, object>)rObj;
                    return new GraphRelationship
                    {
                        Id          = relData["id"].ToString(),
                        Type        = type,
                        StartNodeId = startNodeId,
                        EndNodeId   = endNodeId,
                        Properties  = properties ?? new Dictionary<string, object>()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create relationship", ex);
            }

            return null;
        }

        public async Task<GraphNode> GetNodeAsync(string id)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var query = "MATCH (n) WHERE id(n) = $id RETURN n, labels(n) as labels";
                var parameters = new Dictionary<string, object> { ["id"] = id };

                var result = await ExecuteQueryAsync(query, parameters);
                if (result.Count > 0 && result[0].TryGetValue("n", out var nObj))
                {
                    var nodeData = (Dictionary<string, object>)nObj;
                    var labels   = (List<string>)result[0]["labels"];
                    return new GraphNode
                    {
                        Id         = id,
                        Labels     = labels,
                        Properties = nodeData
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get node {id}", ex);
            }

            return null;
        }

        public async Task<List<GraphNode>> GetNodesByLabelAsync(string label, int limit = 100)
        {
            if (!_isInitialized) await InitializeAsync();
            var nodes = new List<GraphNode>();

            try
            {
                var query = $"MATCH (n:{label}) RETURN n, id(n) as id, labels(n) as labels LIMIT $limit";
                var parameters = new Dictionary<string, object> { ["limit"] = limit };

                var result = await ExecuteQueryAsync(query, parameters);
                foreach (var row in result)
                {
                    var nodeData = (Dictionary<string, object>)row["n"];
                    nodes.Add(new GraphNode
                    {
                        Id         = row["id"].ToString(),
                        Labels     = (List<string>)row["labels"],
                        Properties = nodeData
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get nodes by label {label}", ex);
            }

            return nodes;
        }

        public async Task<List<GraphNode>> GetNodesByPropertyAsync(
            string propertyName,
            object propertyValue,
            int limit = 100)
        {
            if (!_isInitialized) await InitializeAsync();
            var nodes = new List<GraphNode>();

            try
            {
                // Neo4j cannot parameterize the property key itself, so interpolate the key carefully.
                var query = $"MATCH (n) WHERE n.{propertyName} = $propValue " +
                            "RETURN n, id(n) as id, labels(n) as labels LIMIT $limit";

                var parameters = new Dictionary<string, object>
                {
                    ["propValue"] = propertyValue,
                    ["limit"]     = limit
                };

                var result = await ExecuteQueryAsync(query, parameters);
                foreach (var row in result)
                {
                    var nodeData = (Dictionary<string, object>)row["n"];
                    nodes.Add(new GraphNode
                    {
                        Id         = row["id"].ToString(),
                        Labels     = (List<string>)row["labels"],
                        Properties = nodeData
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get nodes by property {propertyName}", ex);
            }

            return nodes;
        }

        public async Task<List<GraphRelationship>> GetRelationshipsByTypeAsync(string type, int limit = 100)
        {
            if (!_isInitialized) await InitializeAsync();
            var relationships = new List<GraphRelationship>();

            try
            {
                var query = $"MATCH ()-[r:{type}]->() " +
                            "RETURN r, id(r) as id, id(startNode(r)) as startId, id(endNode(r)) as endId " +
                            "LIMIT $limit";
                var parameters = new Dictionary<string, object> { ["limit"] = limit };

                var result = await ExecuteQueryAsync(query, parameters);
                foreach (var row in result)
                {
                    var relData = (Dictionary<string, object>)row["r"];
                    relationships.Add(new GraphRelationship
                    {
                        Id          = row["id"].ToString(),
                        Type        = type,
                        StartNodeId = row["startId"].ToString(),
                        EndNodeId   = row["endId"].ToString(),
                        Properties  = relData
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get relationships by type {type}", ex);
            }

            return relationships;
        }

        /* ------------------------------------------------------------------
           Low‑level query pipeline
        ------------------------------------------------------------------ */

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(
            string query,
            Dictionary<string, object> parameters = null)
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
                            statement    = query,
                            parameters   = parameters ?? new Dictionary<string, object>(),
                            includeStats = true
                        }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestData),
                                                Encoding.UTF8,
                                                "application/json");

                // Neo4j 4+ HTTP transactional endpoint
                // TODO: if you support multiple databases, inject the name instead of hard‑coding "neo4j".
                var commitEndpoint = $"{_neo4jUri}/db/neo4j/tx/commit";
                var response       = await _httpClient.PostAsync(commitEndpoint, content);

                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseObj  = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);

                var results = new List<Dictionary<string, object>>();

                var resultList = (List<object>)responseObj["results"];
                if (resultList.Count > 0)
                {
                    var firstResult = (Dictionary<string, object>)resultList[0];
                    var data        = (List<object>)firstResult["data"];
                    var columns     = (List<string>)firstResult["columns"];

                    foreach (var row in data)
                    {
                        var rowDict = (Dictionary<string, object>)row;
                        var rowData = new Dictionary<string, object>();
                        var values  = (List<object>)rowDict["row"];
                        for (int i = 0; i < columns.Count; i++)
                            rowData[columns[i]] = values[i];

                        results.Add(rowData);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute query -> {query}", ex);
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<bool> DeleteNodeAsync(string id)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var query = "MATCH (n) WHERE id(n) = $id DETACH DELETE n";
                await ExecuteQueryAsync(query, new Dictionary<string, object> { ["id"] = id });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete node {id}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteRelationshipAsync(string id)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var query = "MATCH ()-[r]->() WHERE id(r) = $id DELETE r";
                await ExecuteQueryAsync(query, new Dictionary<string, object> { ["id"] = id });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete relationship {id}", ex);
                return false;
            }
        }

        /* ------------------------------------------------------------------
           Health and status
        ------------------------------------------------------------------ */

        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            var status = new Dictionary<string, object>
            {
                ["Mode"]      = _isEmbedded ? "Embedded" : "Remote",
                ["Uri"]       = _neo4jUri,
                ["Connected"] = false
            };

            try
            {
                var result = await ExecuteQueryAsync("RETURN 1 AS connection_check");
                if (result.Count > 0 && result[0].ContainsKey("connection_check"))
                {
                    status["Connected"] = true;
                    _online             = true;
                    _logger.LogInfo($"Successfully connected to Neo4j ({status["Mode"]}) at {_neo4jUri}");
                }
                else
                {
                    _online = false;
                }
            }
            catch (Exception ex)
            {
                status["Error"] = ex.Message;
                _online = false;
                _logger.LogError("Neo4j status check failed", ex);
            }

            return status;
        }

        public async Task<bool> HealthCheckAsync() => await PingAsync();

        public async Task<bool> PingAsync()
        {
            var previousUri = _neo4jUri;
            try
            {
                _neo4jUri = _configurationService.GetNeo4jUri();
                if (previousUri != _neo4jUri)
                {
                    _logger.LogInfo($"Neo4j URI changed -> {previousUri} => {_neo4jUri}");
                    var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_neo4jUser}:{_neo4jPassword}"));
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                }

                var result = await ExecuteQueryAsync("RETURN 1 AS ok");
                _online    = result.Count > 0 && result[0].ContainsKey("ok");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Ping failed for {_neo4jUri} -> {ex.Message}");
                _online = false;
            }

            return _online;
        }

        public bool IsOnline => _online;

        private async Task CheckConnectionAsync()
        {
            _logger.LogInfo($"Timer ping: {_neo4jUri} (Embedded={_isEmbedded})");
            var current = await PingAsync();

            if (current && !_online)
                _logger.LogInfo($"Neo4j connection to {_neo4jUri} restored");
            if (!current && _online)
                _logger.LogWarning($"Neo4j connection to {_neo4jUri} lost");

            _online = current;
        }

        public void Dispose()
        {
            _reconnectTimer?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
