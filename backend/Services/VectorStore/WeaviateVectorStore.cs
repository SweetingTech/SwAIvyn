using System;
using System.Collections.Generic;
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
        private readonly string _className = "SwAIvynMemory";
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

                // Check if Weaviate is accessible
                var healthResponse = await _httpClient.GetAsync($"{_baseUrl}/v1/meta");
                if (!healthResponse.IsSuccessStatusCode)
                {
                    _logger?.LogError($"Weaviate health check failed: {healthResponse.StatusCode}");
                    return;
                }

                // Check if our class already exists
                var schemaResponse = await _httpClient.GetAsync($"{_baseUrl}/v1/schema");
                var schemaContent = await schemaResponse.Content.ReadAsStringAsync();
                
                if (!string.IsNullOrEmpty(schemaContent) && schemaContent.Contains(_className))
                {
                    _logger?.LogInfo($"Weaviate class '{_className}' already exists");
                    _isInitialized = true;
                    return;
                }

                // Create the class schema
                await CreateClassSchema();
                _isInitialized = true;
                _logger?.LogInfo("Weaviate vector store initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to initialize Weaviate vector store: {ex.Message}");
            }
        }

        private async Task CreateClassSchema()
        {
            var classSchema = new
            {
                @class = _className,
                description = "SwAIvyn memory storage for vector search",
                vectorizer = "multi2vec-clip",
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

            var json = JsonSerializer.Serialize(classSchema, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/v1/schema", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger?.LogInfo($"Created Weaviate class '{_className}' successfully");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError($"Failed to create Weaviate class: {response.StatusCode} - {errorContent}");
                throw new Exception($"Failed to create Weaviate class: {response.StatusCode}");
            }
        }

        /// <inheritdoc/>
        public async Task<bool> StoreVectorAsync(Guid id, float[] embedding, Dictionary<string, string>? metadata = null, VectorScope scope = VectorScope.Core)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (!_isInitialized)
            {
                _logger?.LogWarning("Attempted to store vector, but WeaviateVectorStore is not initialized");
                return false;
            }

            try
            {
                var weaviateObject = new
                {
                    @class = _className,
                    id = id.ToString(),
                    properties = new
                    {
                        content = metadata?.GetValueOrDefault("content", ""),
                        category = metadata?.GetValueOrDefault("category", "general"),
                        userId = metadata?.GetValueOrDefault("userId", ""),
                        createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        metadata = metadata != null ? JsonSerializer.Serialize(metadata) : "{}"
                    },
                    vector = embedding
                };

                var json = JsonSerializer.Serialize(weaviateObject, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/v1/objects", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInfo($"Successfully stored vector for ID {id}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogWarning($"Failed to store vector for ID {id}: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Exception storing vector for ID {id}: {ex.Message}");
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
                _logger?.LogWarning("Attempted to search vectors, but WeaviateVectorStore is not initialized");
                return new List<SearchHit>();
            }

            try
            {
                var searchQuery = new
                {
                    query = $@"
                    {{
                        Get {{
                            {_className}(
                                nearVector: {{
                                    vector: [{string.Join(",", queryVector)}]
                                }}
                                limit: {limit}
                            ) {{
                                content
                                category
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

                var response = await _httpClient.PostAsync($"{_baseUrl}/v1/graphql", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogWarning($"Weaviate search failed: {response.StatusCode} - {errorContent}");
                    return new List<SearchHit>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return ParseSearchResponse(responseContent);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Exception during vector search: {ex.Message}");
                return new List<SearchHit>();
            }
        }

        private List<SearchHit> ParseSearchResponse(string responseContent)
        {
            var hits = new List<SearchHit>();
            
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                var data = document.RootElement.GetProperty("data");
                var get = data.GetProperty("Get");
                var results = get.GetProperty(_className);

                foreach (var result in results.EnumerateArray())
                {
                    var additional = result.GetProperty("_additional");
                    var id = Guid.Parse(additional.GetProperty("id").GetString() ?? Guid.Empty.ToString());
                    var distance = additional.GetProperty("distance").GetSingle();
                    
                    // Convert distance to similarity score (lower distance = higher similarity)
                    var score = 1.0f - distance;

                    var metadata = new Dictionary<string, string>();
                    if (result.TryGetProperty("content", out var content))
                        metadata["content"] = content.GetString() ?? "";
                    if (result.TryGetProperty("category", out var category))
                        metadata["category"] = category.GetString() ?? "";
                    if (result.TryGetProperty("userId", out var userId))
                        metadata["userId"] = userId.GetString() ?? "";

                    hits.Add(new SearchHit
                    {
                        Id = id,
                        Score = score,
                        Metadata = metadata
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to parse Weaviate search response: {ex.Message}");
            }

            return hits;
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
                ["className"] = _className
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
