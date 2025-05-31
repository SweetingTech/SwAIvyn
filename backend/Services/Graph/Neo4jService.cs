// filepath: c:\Users\djay\Desktop\SwAIvyn\backend\Services\Graph\Neo4jService.cs
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;
using System.Linq;

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
        private IDriver? _driver; // Added for Bolt connection
        private string? _databaseName; // Added for Bolt connection

        private string _neo4jUri;
        private string _neo4jUser;
        private string _neo4jPassword;
        private readonly bool _isEmbedded;
        private bool _isInitialized;
        private bool _isInitializing;
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
            _databaseName = configuration["AppSettings:Neo4jDatabase"] ?? "neo4j";

            _logger.LogInfo($"Initial Neo4j configuration -> Uri={_neo4jUri}, User={_neo4jUser}, Embedded={_isEmbedded}, Database={_databaseName}");

            _reconnectTimer = new Timer(async _ => await CheckConnectionAsync(),
                                        null,
                                        TimeSpan.FromSeconds(30),
                                        TimeSpan.FromSeconds(30));
        }

        public void Dispose()
        {
            _reconnectTimer?.Dispose();
            _httpClient?.Dispose();
            _driver?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            lock (_lockObject)
            {
                if (_isInitialized || _isInitializing)
                    return;
                _isInitializing = true;
            }

            try
            {
                _logger.LogInfo("Initializing Neo4j service");

                _neo4jUri = _configurationService.GetNeo4jUri();
                _neo4jUser = _configuration["AppSettings:Neo4jUser"] ?? "neo4j";
                _neo4jPassword = _configuration["AppSettings:Neo4jPassword"] ?? "password";
                _databaseName = _configuration["AppSettings:Neo4jDatabase"] ?? "neo4j";

                // Initialize Bolt Driver
                try
                {
                    _driver = GraphDatabase.Driver(_neo4jUri, AuthTokens.Basic(_neo4jUser, _neo4jPassword));
                    await _driver.VerifyConnectivityAsync();
                    _logger.LogInfo("Neo4j Bolt driver initialized and connected successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to initialize Neo4j Bolt driver. HTTP endpoint will be primary.", ex);
                    _driver = null;
                }

                if (_isEmbedded)
                    _logger.LogInfo("Neo4jService running in EMBEDDED mode. Neo4jRuntimeService will start the server.");
                else
                    _logger.LogInfo($"Neo4jService running in EXTERNAL mode. Connecting to {_neo4jUri} as {_neo4jUser}.");

                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_neo4jUser}:{_neo4jPassword}"));
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

                _logger.LogInfo("Waiting for Neo4j to be available...");
                var maxRetries = 30;
                var retryDelay = TimeSpan.FromSeconds(2);
                var isAvailable = false;

                for (int i = 0; i < maxRetries; i++)
                {
                    if (await PingAsync())
                    {
                        isAvailable = true;
                        break;
                    }
                    
                    _logger.LogInfo($"Neo4j not yet available, retry {i + 1}/{maxRetries}...");
                    await Task.Delay(retryDelay);
                }

                if (!isAvailable)
                {
                    _logger.LogWarning("Neo4j is not available after waiting. Skipping DDL initialization.");
                    lock (_lockObject)
                    {
                        _isInitialized = true;
                        _isInitializing = false;
                    }
                    return;
                }

                await InitializeDatabaseSchemaAsync();

                lock (_lockObject)
                {
                    _isInitialized = true;
                    _isInitializing = false;
                }
                _logger.LogInfo("Neo4j HTTP client configured and schema initialized");
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                {
                    _isInitializing = false;
                }
                _logger.LogError("Failed to initialize Neo4j service", ex);
                throw;
            }
        }

        /// <summary>
        /// Initializes the Neo4j database schema with constraints and vector indexes
        /// </summary>
        public async Task InitializeDatabaseSchemaAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Neo4j database schema...");

                await ExecuteDdlQueryAsync(
                    "CREATE CONSTRAINT memory_id_unique IF NOT EXISTS FOR (m:Memory) REQUIRE m.id IS UNIQUE",
                    "Memory.id uniqueness constraint"
                );

                await ExecuteDdlQueryAsync(
                    "CREATE VECTOR INDEX memory_embedding_vector IF NOT EXISTS FOR (m:Memory) ON (m.embedding) OPTIONS {indexConfig: {`vector.dimensions`: 1536, `vector.similarity_function`: 'cosine'}}",
                    "Memory.embedding vector index"
                );

                await ExecuteDdlQueryAsync(
                    "CREATE INDEX memory_timestamp_index IF NOT EXISTS FOR (m:Memory) ON (m.timestamp)",
                    "Memory.timestamp index"
                );

                await ExecuteDdlQueryAsync(
                    "CREATE INDEX memory_userId_index IF NOT EXISTS FOR (m:Memory) ON (m.userId)",
                    "Memory.userId index"
                );

                _logger.LogInfo("Neo4j database schema initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Neo4j database schema", ex);
                _logger.LogWarning("Database schema initialization failed, but service will continue. Some queries may be slower without proper indexes.");
            }
        }

        /// <summary>
        /// Executes a DDL query with proper error handling and logging
        /// </summary>
        private async Task ExecuteDdlQueryAsync(string query, string description)
        {
            try
            {
                _logger.LogInfo($"Creating {description}...");
                var parameters = new Dictionary<string, object>();
                await ExecuteQueryAsync(query, parameters);
                _logger.LogInfo($"✓ {description} created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠ Failed to create {description}: {ex.Message}");
                
                if (ex.Message.Contains("already exists") || ex.Message.Contains("EquivalentSchemaRuleAlreadyExistsException"))
                {
                    _logger.LogInfo($"✓ {description} already exists, skipping");
                }
                else
                {
                    _logger.LogError($"✗ Unexpected error creating {description}: {ex}");
                }
            }
        }

        /* ------------------------------------------------------------------
           CRUD helpers
        ------------------------------------------------------------------ */

        public async Task<GraphNode?> CreateNodeAsync(List<string> labels, Dictionary<string, object> properties)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var sanitizedLabels = labels.Select(l => l?.Trim())
                                            .Where(l => !string.IsNullOrWhiteSpace(l))
                                            .Select(l => $"`{l}`")
                                            .ToList();

                if (!sanitizedLabels.Any())
                {
                    _logger.LogError("CreateNodeAsync: No valid labels provided.");
                    return null;
                }
                var labelString = string.Join(":", sanitizedLabels);

                var query = $"CREATE (n:{labelString} $props) RETURN n, id(n) AS neo4jId";
                var parameters = new Dictionary<string, object> { ["props"] = properties };

                var queryResultRows = await ExecuteQueryAsync(query, parameters);
                if (queryResultRows.Count > 0)
                {
                    var firstRow = queryResultRows[0];
                    if (firstRow.TryGetValue("n", out var nodePropertiesObj) &&
                        firstRow.TryGetValue("neo4jId", out var nodeIdObj))
                    {
                        var actualNodeProperties = nodePropertiesObj as Dictionary<string, object> ?? new Dictionary<string, object>();
                        _logger.LogInfo($"Node created successfully with ID: {nodeIdObj}, Labels: [{string.Join(", ", labels)}]");
                        return new GraphNode
                        {
                            Id = nodeIdObj?.ToString() ?? string.Empty,
                            Labels = labels,
                            Properties = actualNodeProperties
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create node with labels [{string.Join(", ", labels)}] and properties [{JsonSerializer.Serialize(properties)}]", ex);
            }

            return null;
        }

        public async Task<GraphRelationship?> CreateRelationshipAsync(
            string startNodeId,
            string endNodeId,
            string type,
            Dictionary<string, object>? properties = null)
        {
            if (!_isInitialized) await InitializeAsync();
            if (string.IsNullOrWhiteSpace(type))
            {
                _logger.LogError("CreateRelationshipAsync: Relationship type cannot be null or empty.");
                return null;
            }
            var sanitizedType = $"`{type.Trim()}`";

            try
            {
                var query = $"MATCH (a), (b) WHERE id(a) = toInteger($startId) AND id(b) = toInteger($endId) " +
                            $"CREATE (a)-[r:{sanitizedType} $props]->(b) RETURN r, id(r) as neo4jId";
                
                var parameters = new Dictionary<string, object>
                {
                    ["startId"] = startNodeId,
                    ["endId"] = endNodeId,
                    ["props"] = properties ?? new Dictionary<string, object>()
                };

                var queryResultRows = await ExecuteQueryAsync(query, parameters);
                if (queryResultRows.Count > 0)
                {
                    var firstRow = queryResultRows[0];
                    if (firstRow.TryGetValue("r", out var relPropertiesObj) &&
                        firstRow.TryGetValue("neo4jId", out var relIdObj))
                    {
                        var actualRelProperties = relPropertiesObj as Dictionary<string, object> ?? new Dictionary<string, object>();
                        _logger.LogInfo($"Relationship '{type}' created successfully with ID: {relIdObj} from Node {startNodeId} to Node {endNodeId}");
                        return new GraphRelationship
                        {
                            Id = relIdObj?.ToString() ?? string.Empty,
                            Type = type,
                            StartNodeId = startNodeId,
                            EndNodeId = endNodeId,
                            Properties = actualRelProperties
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create relationship '{type}' from node {startNodeId} to node {endNodeId}", ex);
            }

            return null;
        }

        public async Task<GraphNode?> GetNodeAsync(string id)
        {
            if (!_isInitialized) await InitializeAsync();
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("GetNodeAsync: ID cannot be null or empty.");
                return null;
            }

            try
            {
                var query = "MATCH (n) WHERE id(n) = toInteger($nodeId) RETURN n, labels(n) as nodeLabels, id(n) as internalNodeId";
                var parameters = new Dictionary<string, object> { ["nodeId"] = id };

                var queryResultRows = await ExecuteQueryAsync(query, parameters);
                if (queryResultRows.Count > 0)
                {
                    var firstRow = queryResultRows[0];
                    if (firstRow.TryGetValue("n", out var nodePropertiesObj) &&
                        firstRow.TryGetValue("nodeLabels", out var labelsObj) &&
                        firstRow.TryGetValue("internalNodeId", out var internalIdObj))
                    {
                        var actualNodeProperties = nodePropertiesObj as Dictionary<string, object> ?? new Dictionary<string, object>();
                        
                        List<string> labelsList = new List<string>();
                        if (labelsObj is JsonElement labelsJsonElement && labelsJsonElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement labelElement in labelsJsonElement.EnumerateArray())
                            {
                                var labelString = labelElement.GetString();
                                if (!string.IsNullOrEmpty(labelString))
                                {
                                    labelsList.Add(labelString);
                                }
                            }
                        }
                        
                        _logger.LogInfo($"Node retrieved successfully with ID: {internalIdObj}");
                        return new GraphNode
                        {
                            Id = internalIdObj?.ToString() ?? string.Empty,
                            Labels = labelsList,
                            Properties = actualNodeProperties
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get node with id {id}", ex);
            }

            return null;
        }

        public async Task<List<GraphNode>> GetNodesByLabelAsync(string label, int limit = 100)
        {
            if (!_isInitialized) await InitializeAsync();
            var nodes = new List<GraphNode>();

            try
            {
                var query = $"MATCH (n:`{label}`) RETURN n, id(n) as id, labels(n) as labels LIMIT $limit";
                var parameters = new Dictionary<string, object> { ["limit"] = limit };

                var result = await ExecuteQueryAsync(query, parameters);
                foreach (var row in result)
                {
                    if (row.TryGetValue("n", out var nodeData) &&
                        row.TryGetValue("id", out var nodeId) &&
                        row.TryGetValue("labels", out var nodeLabels))
                    {
                        var properties = nodeData as Dictionary<string, object> ?? new Dictionary<string, object>();
                        var labels = ExtractLabelsFromJsonElement(nodeLabels);
                        
                        nodes.Add(new GraphNode
                        {
                            Id = nodeId?.ToString() ?? string.Empty,
                            Labels = labels,
                            Properties = properties
                        });
                    }
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
                var query = $"MATCH (n) WHERE n.`{propertyName}` = $propValue " +
                            "RETURN n, id(n) as id, labels(n) as labels LIMIT $limit";

                var parameters = new Dictionary<string, object>
                {
                    ["propValue"] = propertyValue,
                    ["limit"] = limit
                };

                var result = await ExecuteQueryAsync(query, parameters);
                foreach (var row in result)
                {
                    if (row.TryGetValue("n", out var nodeData) &&
                        row.TryGetValue("id", out var nodeId) &&
                        row.TryGetValue("labels", out var nodeLabels))
                    {
                        var properties = nodeData as Dictionary<string, object> ?? new Dictionary<string, object>();
                        var labels = ExtractLabelsFromJsonElement(nodeLabels);
                        
                        nodes.Add(new GraphNode
                        {
                            Id = nodeId?.ToString() ?? string.Empty,
                            Labels = labels,
                            Properties = properties
                        });
                    }
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
                var query = $"MATCH ()-[r:`{type}`]->() " +
                            "RETURN r, id(r) as id, id(startNode(r)) as startId, id(endNode(r)) as endId " +
                            "LIMIT $limit";
                var parameters = new Dictionary<string, object> { ["limit"] = limit };

                var result = await ExecuteQueryAsync(query, parameters);
                foreach (var row in result)
                {
                    if (row.TryGetValue("r", out var relData) &&
                        row.TryGetValue("id", out var relId) &&
                        row.TryGetValue("startId", out var startId) &&
                        row.TryGetValue("endId", out var endId))
                    {
                        var properties = relData as Dictionary<string, object> ?? new Dictionary<string, object>();
                        
                        relationships.Add(new GraphRelationship
                        {
                            Id = relId?.ToString() ?? string.Empty,
                            Type = type,
                            StartNodeId = startId?.ToString() ?? string.Empty,
                            EndNodeId = endId?.ToString() ?? string.Empty,
                            Properties = properties
                        });
                    }
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
            Dictionary<string, object>? parameters = null)
        {
            bool shouldInitialize;
            lock (_lockObject)
            {
                shouldInitialize = !_isInitialized && !_isInitializing && 
                                 !query.Contains("dbms.cluster.overview") && 
                                 !query.Contains("RETURN 1");
            }
            
            if (shouldInitialize)
                await InitializeAsync();

            var requestStartTime = DateTime.UtcNow;
            var commitEndpoint = $"{_neo4jUri}/db/neo4j/tx/commit";
            
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

                var requestJson = JsonSerializer.Serialize(requestData);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                _logger.LogInfo($"[NEO4J REQUEST] Query: {query}");

                var response = await _httpClient.PostAsync(commitEndpoint, content);
                var responseTime = DateTime.UtcNow.Subtract(requestStartTime).TotalMilliseconds;
                var responseJson = await response.Content.ReadAsStringAsync();

                _logger.LogInfo($"[NEO4J RESPONSE] Status: {(int)response.StatusCode} {response.StatusCode}, Time: {responseTime:F2}ms");

                response.EnsureSuccessStatusCode();

                if (string.IsNullOrEmpty(responseJson))
                {
                    _logger.LogError("[NEO4J ERROR] Empty response received from Neo4j");
                    return new List<Dictionary<string, object>>();
                }

                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);
                var results = new List<Dictionary<string, object>>();

                if (responseObj.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array)
                {
                    var resultArray = resultsElement.EnumerateArray().ToArray();
                    if (resultArray.Length > 0)
                    {
                        var firstResult = resultArray[0];
                        if (firstResult.TryGetProperty("data", out var dataElement) &&
                            firstResult.TryGetProperty("columns", out var columnsElement))
                        {
                            var columns = columnsElement.EnumerateArray().Select(c => c.GetString()).ToArray();

                            foreach (var row in dataElement.EnumerateArray())
                            {
                                if (row.TryGetProperty("row", out var rowElement))
                                {
                                    var rowData = new Dictionary<string, object>();
                                    var values = rowElement.EnumerateArray().ToArray();
                                    for (int i = 0; i < columns.Length && i < values.Length; i++)
                                    {
                                        var columnName = columns[i];
                                        if (!string.IsNullOrEmpty(columnName))
                                        {
                                            rowData[columnName] = JsonElementToObject(values[i]);
                                        }
                                    }
                                    results.Add(rowData);
                                }
                            }
                        }
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[NEO4J ERROR] Failed to execute query: {ex.Message}");
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecuteWriteQueryAsync(string query, Dictionary<string, object> parameters)
        {
            if (_driver == null)
            {
                _logger.LogWarning("ExecuteWriteQueryAsync: Neo4j Bolt driver is not initialized. Falling back to HTTP.");
                return await ExecuteQueryAsync(query, parameters);
            }

            if (!_isInitialized && !_isInitializing) await InitializeAsync();

            var results = new List<Dictionary<string, object>>();
            IAsyncSession? session = null;
            
            try
            {
                session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
                await session.ExecuteWriteAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(query, parameters);
                    var records = await cursor.ToListAsync();
                    foreach (var record in records)
                    {
                        results.Add(record.Keys.ToDictionary(key => key, key => record[key].As<object>()));
                    }
                });
                _logger.LogInfo($"ExecuteWriteQueryAsync successful. Query: {query}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing write query: {query}", ex);
                throw;
            }
            finally
            {
                if (session != null)
                {
                    await session.CloseAsync();
                }
            }
            return results;
        }

        public async Task<bool> DeleteNodeAsync(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                _logger.LogWarning("DeleteNodeAsync: Node ID cannot be null or empty.");
                return false;
            }
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var query = "MATCH (n) WHERE id(n) = toInteger($nodeId) DETACH DELETE n";
                var parameters = new Dictionary<string, object> { ["nodeId"] = nodeId };
                
                await ExecuteWriteQueryAsync(query, parameters);
                _logger.LogInfo($"Node with ID {nodeId} deleted successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete node with ID {nodeId}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteRelationshipAsync(string relationshipId)
        {
            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                _logger.LogWarning("DeleteRelationshipAsync: Relationship ID cannot be null or empty.");
                return false;
            }
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var query = "MATCH ()-[r]-() WHERE id(r) = toInteger($relationshipId) DELETE r";
                var parameters = new Dictionary<string, object> { ["relationshipId"] = relationshipId };

                await ExecuteWriteQueryAsync(query, parameters);
                _logger.LogInfo($"Relationship with ID {relationshipId} deleted successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete relationship with ID {relationshipId}", ex);
                return false;
            }
        }

        /* ------------------------------------------------------------------
           Health and status
        ------------------------------------------------------------------ */

        public async Task<bool> PingAsync()
        {
            if (_driver != null)
            {
                try
                {
                    IAsyncSession? session = null;
                    try
                    {
                        session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
                        await session.RunAsync("RETURN 1");
                        _logger.LogInfo("Neo4j Bolt Ping successful.");
                        _online = true;
                        return true;
                    }
                    finally
                    {
                        if (session != null)
                        {
                            await session.CloseAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Neo4j Bolt Ping failed: {ex.Message}");
                    _online = false;
                }
            }

            if (string.IsNullOrEmpty(_neo4jUri))
            {
                _logger.LogWarning("Neo4j URI is not configured. Cannot ping.");
                _online = false;
                return false;
            }

            try
            {
                var response = await _httpClient.GetAsync($"{_neo4jUri.TrimEnd('/')}/");
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInfo("Neo4j HTTP Ping successful.");
                    _online = true;
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Neo4j HTTP Ping failed with status code: {response.StatusCode}");
                    _online = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Neo4j HTTP Ping failed: {ex.Message}");
                _online = false;
                return false;
            }
        }

        public async Task<bool> CheckConnectionAsync()
        {
            _logger.LogInfo("Checking Neo4j connection status...");
            var isConnected = await PingAsync();
            _online = isConnected;
            if (isConnected)
            {
                _logger.LogInfo("Neo4j connection is active.");
            }
            else
            {
                _logger.LogWarning("Neo4j connection is inactive.");
            }
            return isConnected;
        }        public Dictionary<string, object> GetStatus()
        {
            return new Dictionary<string, object>
            {
                ["Mode"] = _isEmbedded ? "Embedded" : "Remote",
                ["Uri"] = _neo4jUri,
                ["Connected"] = _online,
                ["Status"] = _online ? "Online" : "Offline"
            };
        }        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            // Perform a connection check to ensure status is current
            await CheckConnectionAsync();
            
            return new Dictionary<string, object>
            {
                ["Mode"] = _isEmbedded ? "Embedded" : "Remote",
                ["Uri"] = _neo4jUri,
                ["Connected"] = _online,
                ["Status"] = _online ? "Online" : "Offline",
                ["Database"] = _databaseName ?? "neo4j"
            };
        }

        /* ------------------------------------------------------------------
           Helper methods
        ------------------------------------------------------------------ */

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
                _ => element.ToString()
            };
        }

        private static List<string> ExtractLabelsFromJsonElement(object labelsObj)
        {
            var labelsList = new List<string>();
            if (labelsObj is JsonElement labelsJsonElement && labelsJsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement labelElement in labelsJsonElement.EnumerateArray())
                {
                    var labelString = labelElement.GetString();
                    if (!string.IsNullOrEmpty(labelString))
                    {
                        labelsList.Add(labelString);
                    }
                }
            }
            return labelsList;
        }
    }
}
