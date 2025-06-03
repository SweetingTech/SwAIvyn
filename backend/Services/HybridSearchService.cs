using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Service that calls the external search.py API for hybrid search across all databases
    /// </summary>
    public interface IHybridSearchService
    {
        /// <summary>
        /// Searches across all databases with proper filtering:
        /// - Memories: No userID filtering (global)
        /// - Conversations: With userID filtering (user-specific)
        /// - Documents: With userID filtering (user-specific)
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="userId">User ID for filtering conversations and documents (not memories)</param>
        /// <param name="topK">Maximum number of results</param>
        /// <returns>Combined search results from all sources</returns>
        Task<List<HybridSearchResult>> SearchAsync(string query, Guid userId, int topK = 10);

        /// <summary>
        /// Checks if the search service is available
        /// </summary>
        /// <returns>True if search service is healthy</returns>
        Task<bool> IsHealthyAsync();
    }

    /// <summary>
    /// Search result from the hybrid search service
    /// </summary>
    public class HybridSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
        public string Source { get; set; } = string.Empty; // "neo4j", "weaviate", "sqlite"
        public Dictionary<string, object> Metadata { get; set; } = new();
        public double? NormalizedScore { get; set; }
    }

    /// <summary>
    /// Implementation of hybrid search service that calls search.py API
    /// </summary>
    public class HybridSearchService : IHybridSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly ISimpleLoggerService _logger;
        private readonly string _searchServiceUrl;

        public HybridSearchService(HttpClient httpClient, ISimpleLoggerService logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _searchServiceUrl = "http://localhost:8001"; // search.py service URL
        }

        /// <inheritdoc/>
        public async Task<List<HybridSearchResult>> SearchAsync(string query, Guid userId, int topK = 10)
        {
            try
            {
                _logger.LogInfo($"🔍 HybridSearchService: Searching for '{query}' (userId: {userId}, topK: {topK})");

                var requestBody = new
                {
                    query = query,
                    userId = userId.ToString(),
                    topK = topK
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_searchServiceUrl}/search", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"⚠️ Search service returned {response.StatusCode}: {response.ReasonPhrase}");
                    return new List<HybridSearchResult>();
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<HybridSearchResult>>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInfo($"✅ HybridSearchService: Found {results?.Count ?? 0} results");
                return results ?? new List<HybridSearchResult>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 HybridSearchService error: {ex.Message}", ex);
                return new List<HybridSearchResult>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_searchServiceUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ Search service health check failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Request model for search API
    /// </summary>
    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int TopK { get; set; } = 10;
    }
}
