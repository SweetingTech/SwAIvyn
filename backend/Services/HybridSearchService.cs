using Microsoft.Extensions.Configuration;
using SwAIvyn.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Service for hybrid search functionality that communicates with the Python search service
    /// </summary>
    public class HybridSearchService : IHybridSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ISimpleLoggerService _logger;
        private readonly string _searchServiceUrl;

        public HybridSearchService(
            HttpClient httpClient,
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;            // Get the Python search service URL from configuration
            _searchServiceUrl = _configuration.GetValue<string>("AppSettings:HybridSearchServiceUrl") ?? "http://localhost:8000";
            
            // Configure HttpClient
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <inheritdoc/>
        public async Task<List<HybridSearchResult>> SearchAsync(Guid userId, string query, int maxResults = 10, double hybridAlpha = 0.5)
        {
            try
            {
                _logger.LogInfo($"[HYBRID_SEARCH_SERVICE] Starting search for user {userId}, query: '{query}'");

                // Prepare the request payload
                var requestPayload = new
                {
                    query = query,
                    user_id = userId.ToString(),
                    max_results = maxResults,
                    hybrid_alpha = hybridAlpha
                };

                var json = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Make the HTTP request to the Python service
                var url = $"{_searchServiceUrl}/search";
                _logger.LogInfo($"[HYBRID_SEARCH_SERVICE] Calling Python service at: {url}");

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[HYBRID_SEARCH_SERVICE] Python service returned error: {response.StatusCode} - {errorContent}");
                    throw new Exception($"Search service returned {response.StatusCode}: {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInfo($"[HYBRID_SEARCH_SERVICE] Received response from Python service - Length: {responseContent.Length}");

                // Parse the response
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var searchResponse = JsonSerializer.Deserialize<HybridSearchResponse>(responseContent, options);

                if (searchResponse?.Results == null)
                {
                    _logger.LogWarning("[HYBRID_SEARCH_SERVICE] No results received from Python service");
                    return new List<HybridSearchResult>();
                }

                // Convert Python response to C# objects
                var results = new List<HybridSearchResult>();
                foreach (var result in searchResponse.Results)
                {
                    var hybridResult = new HybridSearchResult
                    {
                        Id = result.Id ?? Guid.NewGuid().ToString(),
                        Content = result.Content ?? "",
                        Source = result.Source ?? "unknown",
                        Type = result.Type ?? "unknown",
                        Score = result.Score,
                        Category = result.Category,
                        CreatedAt = ParseDateTime(result.CreatedAt),
                        Metadata = result.Metadata
                    };
                    results.Add(hybridResult);
                }

                _logger.LogInfo($"[HYBRID_SEARCH_SERVICE] Successfully processed {results.Count} search results");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[HYBRID_SEARCH_SERVICE] Error during hybrid search", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<object> GetServiceStatusAsync()
        {
            try
            {
                _logger.LogInfo("[HYBRID_SEARCH_SERVICE] Checking service status");

                var url = $"{_searchServiceUrl}/health";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Service health check failed: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var statusData = JsonSerializer.Deserialize<object>(content);

                return new
                {
                    pythonServiceStatus = statusData,
                    serviceUrl = _searchServiceUrl,
                    lastChecked = DateTime.UtcNow,
                    isHealthy = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("[HYBRID_SEARCH_SERVICE] Service health check failed", ex);
                return new
                {
                    pythonServiceStatus = "unavailable",
                    serviceUrl = _searchServiceUrl,
                    lastChecked = DateTime.UtcNow,
                    isHealthy = false,
                    error = ex.Message
                };
            }
        }

        /// <summary>
        /// Parses a datetime string from various formats
        /// </summary>
        private static DateTime? ParseDateTime(string? dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString))
                return null;

            if (DateTime.TryParse(dateTimeString, out var result))
                return result;

            return null;
        }
    }

    /// <summary>
    /// Response model from the Python hybrid search service
    /// </summary>
    internal class HybridSearchResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<HybridSearchResultDto>? Results { get; set; }
        public int? TotalResults { get; set; }
        public string? Query { get; set; }
    }

    /// <summary>
    /// Individual search result from the Python service
    /// </summary>
    internal class HybridSearchResultDto
    {
        public string? Id { get; set; }
        public string? Content { get; set; }
        public string? Source { get; set; }
        public string? Type { get; set; }
        public double Score { get; set; }
        public string? Category { get; set; }
        public string? CreatedAt { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
