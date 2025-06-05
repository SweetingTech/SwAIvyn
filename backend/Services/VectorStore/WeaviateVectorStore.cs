using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SwAIvyn.Services.VectorStore
{
    /// <summary>
    /// Weaviate-based vector store implementation
    /// </summary>
    public class WeaviateVectorStore : IVectorStore
    {
        private readonly HttpClient _httpClient;
        private readonly ISimpleLoggerService _logger;
        private readonly string _baseUrl;
        private readonly string _memoryClassName = "SwAIvynMemory";
        private readonly string _documentClassName = "Document";
        private bool _isInitialized = false;

        public WeaviateVectorStore(
            HttpClient httpClient,
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration.GetValue<string>("AppSettings:WeaviateUrl", "http://stabled:8080");

            _logger?.LogInfo($"WeaviateVectorStore initialized with base URL: {_baseUrl}");
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                _logger?.LogInfo("Initializing Weaviate vector store...");
                _logger?.LogInfo($"[WEAVIATE CONFIG] Base URL: {_baseUrl}");
                _logger?.LogInfo($"[WEAVIATE CONFIG] Memory Class: {_memoryClassName}");
                _logger?.LogInfo($"[WEAVIATE CONFIG] Document Class: {_documentClassName}");

                // Check if Weaviate is accessible
                var healthEndpoint = $"{_baseUrl}/v1/meta";
                _logger?.LogInfo($"[WEAVIATE REQUEST] Health Check - GET {healthEndpoint}");
                
                var requestStartTime = DateTime.UtcNow;
                var healthResponse = await _httpClient.GetAsync(healthEndpoint);
                var healthResponseTime = DateTime.UtcNow.Subtract(requestStartTime).TotalMilliseconds;
                
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Health Check - Status: {(int)healthResponse.StatusCode} {healthResponse.StatusCode}");
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Health Check - Time: {healthResponseTime:F2}ms");
                
                if (!healthResponse.IsSuccessStatusCode)
                {
                    var healthErrorContent = await healthResponse.Content.ReadAsStringAsync();
                    _logger?.LogError($"[WEAVIATE ERROR] Health check failed: {healthResponse.StatusCode}");
                    _logger?.LogError($"[WEAVIATE ERROR] Health check response: {healthErrorContent}");
                    return;
                }

                var healthContent = await healthResponse.Content.ReadAsStringAsync();
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Health Check - Content Length: {healthContent?.Length ?? 0} characters");

                // Check if our classes already exist
                var schemaEndpoint = $"{_baseUrl}/v1/schema";
                _logger?.LogInfo($"[WEAVIATE REQUEST] Schema Check - GET {schemaEndpoint}");
                
                requestStartTime = DateTime.UtcNow;
                var schemaResponse = await _httpClient.GetAsync(schemaEndpoint);
                var schemaResponseTime = DateTime.UtcNow.Subtract(requestStartTime).TotalMilliseconds;
                var schemaContent = await schemaResponse.Content.ReadAsStringAsync();
                
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Schema Check - Status: {(int)schemaResponse.StatusCode} {schemaResponse.StatusCode}");
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Schema Check - Time: {schemaResponseTime:F2}ms");
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Schema Check - Content Length: {schemaContent?.Length ?? 0} characters");

                var memoryClassExists = !string.IsNullOrEmpty(schemaContent) && schemaContent.Contains(_memoryClassName);
                var documentClassExists = !string.IsNullOrEmpty(schemaContent) && schemaContent.Contains(_documentClassName);

                // Create Memory class schema if needed
                if (!memoryClassExists)
                {
                    await CreateMemoryClassSchema();
                }
                else
                {
                    _logger?.LogInfo($"Weaviate class '{_memoryClassName}' already exists");
                }

                // Create Document class schema if needed  
                if (!documentClassExists)
                {
                    await CreateDocumentClassSchema();
                }
                else
                {
                    _logger?.LogInfo($"Weaviate class '{_documentClassName}' already exists");
                }

                _isInitialized = true;
                _logger?.LogInfo("Weaviate vector store initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[WEAVIATE ERROR] Failed to initialize Weaviate vector store: {ex.Message}");
                _logger?.LogError($"[WEAVIATE ERROR] Exception details: {ex}");
            }
        }

        /// <summary>
        /// Creates the Memory class schema for SwAIvyn memories
        /// </summary>
        private async Task CreateMemoryClassSchema()
        {
            var classSchema = new
            {
                @class = _memoryClassName,
                description = "SwAIvyn memory storage for vector search",
                vectorizer = "none",  // We provide our own embeddings
                vectorIndexConfig = new
                {
                    distance = "cosine"
                },
                properties = new[]
                {
                    new
                    {
                        name = "content",
                        dataType = new[] { "text" },
                        description = "The memory content"
                    },
                    new
                    {
                        name = "category",
                        dataType = new[] { "string" },
                        description = "Memory category"
                    },
                    new
                    {
                        name = "userId",
                        dataType = new[] { "string" },
                        description = "User ID who owns this memory"
                    },
                    new
                    {
                        name = "createdAt",
                        dataType = new[] { "date" },
                        description = "Creation timestamp"
                    },
                    new
                    {
                        name = "metadata",
                        dataType = new[] { "text" },
                        description = "Additional metadata as JSON"
                    }
                }
            };

            await CreateClassSchemaInternal(_memoryClassName, classSchema);
        }

        /// <summary>
        /// Creates the Document class schema for upload document chunks
        /// </summary>
        private async Task CreateDocumentClassSchema()
        {
            var classSchema = new
            {
                @class = _documentClassName,
                description = "Document chunks from uploaded files for semantic search",
                vectorizer = "none",  // We provide our own embeddings  
                vectorIndexConfig = new
                {
                    distance = "cosine"
                },
                properties = new[]
                {
                    new
                    {
                        name = "title",
                        dataType = new[] { "text" },
                        description = "Document title or filename"
                    },
                    new
                    {
                        name = "content",
                        dataType = new[] { "text" },
                        description = "Document chunk content"
                    },
                    new
                    {
                        name = "source",
                        dataType = new[] { "string" },
                        description = "Source filename or URL"
                    },
                    new
                    {
                        name = "tags",
                        dataType = new[] { "text[]" },
                        description = "Document tags for categorization"
                    },
                    new
                    {
                        name = "userId",
                        dataType = new[] { "string" },
                        description = "User ID who owns this document"
                    },
                    new
                    {
                        name = "uploadedAt",
                        dataType = new[] { "date" },
                        description = "Upload timestamp"
                    },
                    new
                    {
                        name = "chunkIndex",
                        dataType = new[] { "int" },
                        description = "Index of this chunk within the document"
                    },
                    new
                    {
                        name = "metadata",
                        dataType = new[] { "text" },
                        description = "Additional metadata as JSON"
                    }
                }
            };

            await CreateClassSchemaInternal(_documentClassName, classSchema);
        }

        /// <summary>
        /// Internal method to create a class schema
        /// </summary>
        private async Task CreateClassSchemaInternal(string className, object classSchema)
        {
            var json = JsonSerializer.Serialize(classSchema, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var schemaEndpoint = $"{_baseUrl}/v1/schema";
            _logger?.LogInfo($"[WEAVIATE REQUEST] Create Schema - POST {schemaEndpoint}");
            _logger?.LogInfo($"[WEAVIATE REQUEST] Create Schema - Class: {className}");
            _logger?.LogInfo($"[WEAVIATE REQUEST] Create Schema - Headers: Content-Type=application/json");
            _logger?.LogInfo($"[WEAVIATE REQUEST] Create Schema - Body: {json}");

            var requestStartTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsync(schemaEndpoint, content);
            var responseTime = DateTime.UtcNow.Subtract(requestStartTime).TotalMilliseconds;

            _logger?.LogInfo($"[WEAVIATE RESPONSE] Create Schema - Status: {(int)response.StatusCode} {response.StatusCode}");
            _logger?.LogInfo($"[WEAVIATE RESPONSE] Create Schema - Time: {responseTime:F2}ms");
            _logger?.LogInfo($"[WEAVIATE RESPONSE] Create Schema - Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Create Schema - Content Length: {responseContent?.Length ?? 0} characters");
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Create Schema - Body: {responseContent}");
                _logger?.LogInfo($"Created Weaviate class '{className}' successfully");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Class already exists - this is fine for idempotent initialization
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogInfo($"[WEAVIATE INFO] Class '{className}' already exists (409 Conflict)");
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Body: {responseContent}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError($"[WEAVIATE ERROR] Failed to create Weaviate class '{className}': {response.StatusCode}");
                _logger?.LogError($"[WEAVIATE ERROR] Error response: {errorContent}");
                throw new Exception($"Failed to create Weaviate class '{className}': {response.StatusCode}");
            }
        }

        /// <inheritdoc/>
        public async Task<bool> StoreVectorAsync(Guid id, float[] embedding, Dictionary<string, string>? metadata = null, VectorScope scope = VectorScope.Core)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (!_isInitialized)
            {
                _logger?.LogWarning("[WEAVIATE WARNING] Attempted to store vector, but WeaviateVectorStore is not initialized");
                return false;
            }

            try
            {
                // Determine the class based on scope or metadata
                var className = _memoryClassName; // Default to Memory class for backward compatibility
                
                // Check if this is a document upload (VectorRouter should set this)
                if (metadata?.ContainsKey("_type") == true && metadata["_type"] == "document")
                {
                    className = _documentClassName;
                }

                var weaviateObject = new
                {
                    @class = className,
                    id = id.ToString(),
                    properties = BuildProperties(className, metadata),
                    vector = embedding
                };

                var json = JsonSerializer.Serialize(weaviateObject, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var objectsEndpoint = $"{_baseUrl}/v1/objects";
                _logger?.LogInfo($"[WEAVIATE REQUEST] Store Vector - POST {objectsEndpoint}");
                _logger?.LogInfo($"[WEAVIATE REQUEST] Store Vector - Class: {className}");
                _logger?.LogInfo($"[WEAVIATE REQUEST] Store Vector - Object ID: {id}");
                _logger?.LogInfo($"[WEAVIATE REQUEST] Store Vector - Embedding Length: {embedding?.Length ?? 0}");
                _logger?.LogInfo($"[WEAVIATE REQUEST] Store Vector - Metadata: {JsonSerializer.Serialize(metadata ?? new Dictionary<string, string>())}");

                var requestStartTime = DateTime.UtcNow;
                var response = await _httpClient.PostAsync(objectsEndpoint, content);
                var responseTime = DateTime.UtcNow.Subtract(requestStartTime).TotalMilliseconds;

                _logger?.LogInfo($"[WEAVIATE RESPONSE] Store Vector - Status: {(int)response.StatusCode} {response.StatusCode}");
                _logger?.LogInfo($"[WEAVIATE RESPONSE] Store Vector - Time: {responseTime:F2}ms");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogInfo($"[WEAVIATE RESPONSE] Store Vector - Content Length: {responseContent?.Length ?? 0} characters");
                    _logger?.LogInfo($"Successfully stored vector for ID {id} in class {className}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogWarning($"[WEAVIATE ERROR] Failed to store vector for ID {id}: {response.StatusCode}");
                    _logger?.LogWarning($"[WEAVIATE ERROR] Error response: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[WEAVIATE ERROR] Exception storing vector for ID {id}: {ex.Message}");
                _logger?.LogError($"[WEAVIATE ERROR] Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Builds properties object based on class name and metadata
        /// </summary>
        private object BuildProperties(string className, Dictionary<string, string>? metadata)
        {
            if (className == _documentClassName)
            {
                return new
                {
                    title = metadata?.GetValueOrDefault("title", ""),
                    content = metadata?.GetValueOrDefault("content", ""),
                    source = metadata?.GetValueOrDefault("source", ""),
                    tags = metadata?.GetValueOrDefault("tags", "").Split(',', StringSplitOptions.RemoveEmptyEntries),
                    userId = metadata?.GetValueOrDefault("userId", ""),
                    uploadedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    chunkIndex = int.TryParse(metadata?.GetValueOrDefault("chunkIndex", "0"), out var idx) ? idx : 0,
                    metadata = metadata != null ? JsonSerializer.Serialize(metadata) : "{}"
                };
            }
            else // Memory class
            {
                return new
                {
                    content = metadata?.GetValueOrDefault("content", ""),
                    category = metadata?.GetValueOrDefault("category", "general"),
                    userId = metadata?.GetValueOrDefault("userId", ""),
                    createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    metadata = metadata != null ? JsonSerializer.Serialize(metadata) : "{}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<List<SearchHit>> SearchAsync(float[] queryVector, int limit = 10, VectorScope scope = VectorScope.All)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (!_isInitialized)
            {
                _logger?.LogWarning("Attempted to search vectors, but WeaviateVectorStore is not initialized");
                return new List<SearchHit>();
            }

            try
            {
                var allResults = new List<SearchHit>();

                // Search in Memory class
                var memoryResults = await SearchInClass(_memoryClassName, queryVector, limit);
                allResults.AddRange(memoryResults);

                // Search in Document class  
                var documentResults = await SearchInClass(_documentClassName, queryVector, limit);
                allResults.AddRange(documentResults);                // Sort by relevance and limit
                return allResults
                    .OrderBy(hit => hit.Score)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Exception during vector search: {ex.Message}");
                return new List<SearchHit>();
            }
        }

        /// <summary>
        /// Searches vectors in a specific Weaviate class
        /// </summary>
        private async Task<List<SearchHit>> SearchInClass(string className, float[] queryVector, int limit)
        {
            try
            {
                var searchQuery = new
                {
                    query = $@"
                    {{
                        Get {{
                            {className}(
                                nearVector: {{
                                    vector: [{string.Join(",", queryVector)}]
                                }}
                                limit: {limit}
                            ) {{
                                content
                                {(className == _documentClassName ? "title source tags" : "category")}
                                userId
                                metadata
                                _additional {{
                                    id
                                    distance
                                }}
                            }}
                        }}
                    }}"
                };

                var json = JsonSerializer.Serialize(searchQuery);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var graphQLEndpoint = $"{_baseUrl}/v1/graphql";
                var response = await _httpClient.PostAsync(graphQLEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return ParseSearchResponse(responseContent, className);
                }
                else
                {
                    _logger?.LogWarning($"Search failed for class {className}: {response.StatusCode}");
                    return new List<SearchHit>();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Exception during search in class {className}: {ex.Message}");
                return new List<SearchHit>();
            }
        }

        /// <summary>
        /// Parses Weaviate GraphQL search response
        /// </summary>
        private List<SearchHit> ParseSearchResponse(string responseContent, string className)
        {
            var results = new List<SearchHit>();

            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                var data = doc.RootElement.GetProperty("data");
                var get = data.GetProperty("Get");
                
                if (get.TryGetProperty(className, out var classResults))
                {
                    foreach (var item in classResults.EnumerateArray())
                    {
                        var additional = item.GetProperty("_additional");
                        var id = additional.GetProperty("id").GetString();
                        var distance = additional.GetProperty("distance").GetSingle();

                        var metadata = new Dictionary<string, string>();

                        // Common properties
                        if (item.TryGetProperty("content", out var contentProp))
                            metadata["content"] = contentProp.GetString() ?? "";
                        if (item.TryGetProperty("userId", out var userIdProp))
                            metadata["userId"] = userIdProp.GetString() ?? "";
                        if (item.TryGetProperty("metadata", out var metadataProp))
                            metadata["metadata"] = metadataProp.GetString() ?? "";

                        // Class-specific properties
                        if (className == _documentClassName)
                        {
                            if (item.TryGetProperty("title", out var titleProp))
                                metadata["title"] = titleProp.GetString() ?? "";
                            if (item.TryGetProperty("source", out var sourceProp))
                                metadata["source"] = sourceProp.GetString() ?? "";
                            if (item.TryGetProperty("tags", out var tagsProp))
                            {
                                var tags = new List<string>();
                                foreach (var tag in tagsProp.EnumerateArray())
                                {
                                    tags.Add(tag.GetString() ?? "");
                                }
                                metadata["tags"] = string.Join(",", tags);
                            }
                        }
                        else
                        {
                            if (item.TryGetProperty("category", out var categoryProp))
                                metadata["category"] = categoryProp.GetString() ?? "";
                        }                        if (Guid.TryParse(id, out var guidId))
                        {
                            results.Add(new SearchHit
                            {
                                Id = guidId,
                                Score = distance,
                                Metadata = metadata
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to parse search response for class {className}: {ex.Message}");
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteVectorAsync(Guid id, VectorScope scope = VectorScope.Core)
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/v1/objects/{id}");

                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInfo($"Successfully deleted vector for ID {id}");
                    return true;
                }
                else
                {
                    _logger?.LogWarning($"Failed to delete vector for ID {id}: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Exception deleting vector for ID {id}: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            var status = new Dictionary<string, object>
            {
                ["type"] = "Weaviate",
                ["baseUrl"] = _baseUrl,
                ["initialized"] = _isInitialized,
                ["memoryClass"] = _memoryClassName,
                ["documentClass"] = _documentClassName
            };

            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/meta");
                status["connected"] = response.IsSuccessStatusCode;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    status["meta"] = content;
                }
            }
            catch (Exception ex)
            {
                status["connected"] = false;
                status["error"] = ex.Message;
            }

            return status;
        }

        /// <inheritdoc/>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/meta");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
