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
        private readonly bool _isEmbedded;        private bool _isInitialized;
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

            _logger.LogInfo($"Initial Neo4j configuration -> Uri={_neo4jUri}, User={_neo4jUser}, Embedded={_isEmbedded}");

            // Ping every 30 s to keep status fresh
            _reconnectTimer = new Timer(async _ => await CheckConnectionAsync(),
                                        null,
                                        TimeSpan.FromSeconds(30),
                                        TimeSpan.FromSeconds(30));
        }        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            // Prevent recursive initialization
            lock (_lockObject)
            {
                if (_isInitialized || _isInitializing)
                    return;
                _isInitializing = true;
            }

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

                // Wait for Neo4j to be available before running DDL
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

                // Initialize database schema (constraints and indexes)
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
        }/// <summary>
        /// Initializes the Neo4j database schema with constraints and vector indexes
        /// </summary>
        public async Task InitializeDatabaseSchemaAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Neo4j database schema...");

                // 1. Create uniqueness constraint for Memory.id
                await ExecuteDdlQueryAsync(
                    "CREATE CONSTRAINT memory_id_unique IF NOT EXISTS FOR (m:Memory) REQUIRE m.id IS UNIQUE",
                    "Memory.id uniqueness constraint"
                );

                // 2. Create vector index for Memory.embedding (1536 dimensions, cosine similarity)  
                await ExecuteDdlQueryAsync(
                    "CREATE VECTOR INDEX memory_embedding_vector IF NOT EXISTS FOR (m:Memory) ON (m.embedding) OPTIONS {indexConfig: {`vector.dimensions`: 1536, `vector.similarity_function`: 'cosine'}}",
                    "Memory.embedding vector index"
                );

                // 3. Create timestamp index for Memory.timestamp (optional but helpful for queries)
                await ExecuteDdlQueryAsync(
                    "CREATE INDEX memory_timestamp_index IF NOT EXISTS FOR (m:Memory) ON (m.timestamp)",
                    "Memory.timestamp index"
                );

                // 4. Create index for Memory.userId for efficient user-based queries
                await ExecuteDdlQueryAsync(
                    "CREATE INDEX memory_userId_index IF NOT EXISTS FOR (m:Memory) ON (m.userId)",
                    "Memory.userId index"
                );

                _logger.LogInfo("Neo4j database schema initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Neo4j database schema", ex);
                // Don't throw here - let the service continue with warnings
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
                // Log error but don't fail initialization - constraints/indexes might already exist
                _logger.LogWarning($"⚠ Failed to create {description}: {ex.Message}");
                
                // Check if it's just because it already exists
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
                // Sanitize labels and ensure they are not empty or whitespace
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
                    {                        var actualNodeProperties = nodePropertiesObj as Dictionary<string, object> ?? new Dictionary<string, object>();
                        _logger.LogInfo($"Node created successfully with ID: {nodeIdObj}, Labels: [{string.Join(", ", labels)}]");
                        return new GraphNode
                        {
                            Id         = nodeIdObj?.ToString() ?? string.Empty,
                            Labels     = labels, // Use original labels for consistency, Neo4j stores them as provided
                            Properties = actualNodeProperties
                        };
                    }
                    else
                    {
                        _logger.LogWarning($"CreateNodeAsync: Query executed but 'n' or 'neo4jId' not found in the result. Query: {query}, Result: {JsonSerializer.Serialize(firstRow)}");
                    }
                }
                else
                {
                    _logger.LogWarning($"CreateNodeAsync: No results returned from query. Query: {query}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create node with labels [{string.Join(", ", labels)}] and properties [{JsonSerializer.Serialize(properties)}]", ex);
            }

            return null;
        }        public async Task<GraphRelationship?> CreateRelationshipAsync(
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
                // Use toInteger for ID matching and sanitize relationship type with backticks
                var query = $"MATCH (a), (b) WHERE id(a) = toInteger($startId) AND id(b) = toInteger($endId) " +
                            $"CREATE (a)-[r:{sanitizedType} $props]->(b) RETURN r, id(r) as neo4jId";
                
                var parameters = new Dictionary<string, object>
                {
                    // Ensure IDs are passed as strings if that's how they are stored/retrieved, toInteger handles conversion in Cypher
                    ["startId"] = startNodeId,
                    ["endId"]   = endNodeId,
                    ["props"]   = properties ?? new Dictionary<string, object>()
                };

                var queryResultRows = await ExecuteQueryAsync(query, parameters);
                if (queryResultRows.Count > 0)
                {
                    var firstRow = queryResultRows[0];
                    if (firstRow.TryGetValue("r", out var relPropertiesObj) &&
                        firstRow.TryGetValue("neo4jId", out var relIdObj))
                    {                        var actualRelProperties = relPropertiesObj as Dictionary<string, object> ?? new Dictionary<string, object>();
                        _logger.LogInfo($"Relationship '{type}' created successfully with ID: {relIdObj} from Node {startNodeId} to Node {endNodeId}");
                        return new GraphRelationship
                        {
                            Id          = relIdObj?.ToString() ?? string.Empty,
                            Type        = type,
                            StartNodeId = startNodeId,
                            EndNodeId   = endNodeId,
                            Properties  = actualRelProperties
                        };
                    }
                    else
                    {
                        _logger.LogWarning($"CreateRelationshipAsync: Query executed but 'r' or 'neo4jId' not found in the result. Query: {query}, Result: {JsonSerializer.Serialize(firstRow)}");
                    }
                }
                else
                {
                     _logger.LogWarning($"CreateRelationshipAsync: No results returned from query. Query: {query}");
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
                          // Safely convert labelsObj to List<string>
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
                        else if (labelsObj is IEnumerable<object> labelsEnumerable) // Fallback for other list types if JsonElementToObject changes
                        {
                            labelsList = labelsEnumerable.Select(l => l?.ToString())
                                                        .Where(s => !string.IsNullOrEmpty(s))
                                                        .Cast<string>()
                                                        .ToList();
                        }
                        
                        _logger.LogInfo($"Node retrieved successfully with ID: {internalIdObj}");
                        return new GraphNode
                        {
                            Id         = internalIdObj?.ToString() ?? string.Empty,
                            Labels     = labelsList,
                            Properties = actualNodeProperties
                        };
                    }
                    else
                    {
                         _logger.LogWarning($"GetNodeAsync: Query executed but 'n', 'nodeLabels', or 'internalNodeId' not found in result for ID {id}. Result: {JsonSerializer.Serialize(firstRow)}");
                    }
                }
                else
                {
                    _logger.LogInfo($"GetNodeAsync: No node found with ID {id}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get node with id {id}", ex);
            }

            return null;
        }        public async Task<List<GraphNode>> GetNodesByLabelAsync(string label, int limit = 100)
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
                        Id         = row["id"]?.ToString() ?? string.Empty,
                        Labels     = (List<string>)row["labels"] ?? new List<string>(),
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
                };                var result = await ExecuteQueryAsync(query, parameters);
                foreach (var row in result)
                {
                    var nodeData = (Dictionary<string, object>)row["n"];
                    nodes.Add(new GraphNode
                    {
                        Id         = row["id"]?.ToString() ?? string.Empty,
                        Labels     = (List<string>)row["labels"] ?? new List<string>(),
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
                var parameters = new Dictionary<string, object> { ["limit"] = limit };                var result = await ExecuteQueryAsync(query, parameters);
                foreach (var row in result)
                {
                    var relData = (Dictionary<string, object>)row["r"];
                    relationships.Add(new GraphRelationship
                    {
                        Id          = row["id"]?.ToString() ?? string.Empty,
                        Type        = type,
                        StartNodeId = row["startId"]?.ToString() ?? string.Empty,
                        EndNodeId   = row["endId"]?.ToString() ?? string.Empty,
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
        ------------------------------------------------------------------ */        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(
            string query,
            Dictionary<string, object>? parameters = null)
        {
            // Don't auto-initialize during ping/health checks or basic queries to avoid recursion
            // Also check if we're already initializing to prevent infinite loops
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
                            statement    = query,
                            parameters   = parameters ?? new Dictionary<string, object>(),
                            includeStats = true
                        }
                    }
                };

                var requestJson = JsonSerializer.Serialize(requestData);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                // Log comprehensive request details
                _logger.LogInfo($"[NEO4J REQUEST] Endpoint: {commitEndpoint}");
                _logger.LogInfo($"[NEO4J REQUEST] Method: POST");
                _logger.LogInfo($"[NEO4J REQUEST] Query: {query}");
                _logger.LogInfo($"[NEO4J REQUEST] Parameters: {JsonSerializer.Serialize(parameters ?? new Dictionary<string, object>())}");
                _logger.LogInfo($"[NEO4J REQUEST] Full JSON Body: {requestJson}");
                _logger.LogInfo($"[NEO4J REQUEST] Headers: Authorization=Basic [REDACTED], Content-Type=application/json");

                // Neo4j 4+ HTTP transactional endpoint
                // TODO: if you support multiple databases, inject the name instead of hard‑coding "neo4j".
                var response = await _httpClient.PostAsync(commitEndpoint, content);

                var responseTime = DateTime.UtcNow.Subtract(requestStartTime).TotalMilliseconds;
                var responseJson = await response.Content.ReadAsStringAsync();

                // Log comprehensive response details
                _logger.LogInfo($"[NEO4J RESPONSE] Status: {(int)response.StatusCode} {response.StatusCode}");
                _logger.LogInfo($"[NEO4J RESPONSE] Time: {responseTime:F2}ms");
                _logger.LogInfo($"[NEO4J RESPONSE] Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");
                _logger.LogInfo($"[NEO4J RESPONSE] Content Length: {responseJson?.Length ?? 0} characters");
                _logger.LogInfo($"[NEO4J RESPONSE] Full JSON Body: {responseJson}");

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
                            var columns = columnsElement.EnumerateArray().Select(c => c.GetString()).ToArray();                            foreach (var row in dataElement.EnumerateArray())
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
                }                return results;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError($"[NEO4J ERROR] HTTP request failed to {commitEndpoint}: {httpEx.Message}");
                _logger.LogError($"[NEO4J ERROR] Query was: {query}");
                return new List<Dictionary<string, object>>();
            }
            catch (TaskCanceledException tcEx)
            {
                _logger.LogError($"[NEO4J ERROR] Request timeout to {commitEndpoint}: {tcEx.Message}");
                _logger.LogError($"[NEO4J ERROR] Query was: {query}");
                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[NEO4J ERROR] Failed to execute query to {commitEndpoint}: {ex.Message}");
                _logger.LogError($"[NEO4J ERROR] Query was: {query}");
                _logger.LogError($"[NEO4J ERROR] Exception details: {ex}");
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<bool> DeleteNodeAsync(string id)
        {
            if (!_isInitialized) await InitializeAsync();
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("DeleteNodeAsync: ID cannot be null or empty.");
                return false;
            }

            try
            {
                var query = "MATCH (n) WHERE id(n) = toInteger($nodeId) DETACH DELETE n";
                var parameters = new Dictionary<string, object> { ["nodeId"] = id };
                // ExecuteQueryAsync for DELETE might return empty results or stats, not actual node data.
                // We rely on it not throwing an exception for success.
                await ExecuteQueryAsync(query, parameters); 
                _logger.LogInfo($"Node with ID {id} (and its relationships) deleted successfully.");
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
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("DeleteRelationshipAsync: ID cannot be null or empty.");
                return false;
            }

            try
            {
                // Assuming 'id' here is the Neo4j internal relationship ID
                var query = "MATCH ()-[r]->() WHERE id(r) = toInteger($relId) DELETE r";
                var parameters = new Dictionary<string, object> { ["relId"] = id };
                await ExecuteQueryAsync(query, parameters);
                _logger.LogInfo($"Relationship with ID {id} deleted successfully.");
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

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
                _ => element.ToString()
            };
        }

        public void Dispose()
        {
            _reconnectTimer?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
