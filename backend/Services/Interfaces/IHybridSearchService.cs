using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services.Interfaces
{
    /// <summary>
    /// Interface for hybrid search service that combines SQL, Weaviate, and Neo4j databases
    /// </summary>
    public interface IHybridSearchService
    {
        /// <summary>
        /// Performs a hybrid search across all databases (SQLite, Neo4j, Weaviate)
        /// </summary>
        /// <param name="userId">User ID to filter results</param>
        /// <param name="query">Search query</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <param name="hybridAlpha">Weight for text vs semantic search (0.0 = only text, 1.0 = only semantic)</param>
        /// <returns>Ranked hybrid search results</returns>
        Task<List<HybridSearchResult>> SearchAsync(Guid userId, string query, int maxResults = 10, double hybridAlpha = 0.5);

        /// <summary>
        /// Gets the status of the Python hybrid search service
        /// </summary>
        /// <returns>Service status information</returns>
        Task<object> GetServiceStatusAsync();
    }

    /// <summary>
    /// Represents a single result from the hybrid search
    /// </summary>
    public class HybridSearchResult
    {
        /// <summary>
        /// Unique identifier for the search result
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Content/text of the search result
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Source database (SQLite, Neo4j, Weaviate)
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Type of content (memory, chat, document, etc.)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Combined similarity/relevance score
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Category or classification of the content
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Timestamp when the content was created
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Additional metadata about the search result
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
