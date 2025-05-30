using SwAIvyn.Data.Entities;
using SwAIvyn.Enums;

namespace SwAIvyn.Services.Interfaces
{
    /// <summary>
    /// Unified facade interface for memory operations across all three databases.
    /// Provides clean CRUD operations and routing between SQLite (source of truth),
    /// Neo4j (brain/graph), and Weaviate (document/knowledge).
    /// </summary>
    public interface IMemoryService
    {
        /// <summary>
        /// Creates a new memory item with intelligent routing to appropriate vector store.
        /// </summary>
        /// <param name="memory">Memory item to create</param>
        /// <param name="metadata">Additional metadata for the memory</param>
        /// <returns>Success status and created memory item</returns>
        Task<(bool Success, MemoryItem? CreatedMemory)> CreateMemoryAsync(MemoryItem memory, Dictionary<string, string>? metadata = null);

        /// <summary>
        /// Gets all memory items for a user from SQLite (source of truth).
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="category">Optional category filter</param>
        /// <returns>List of memory items</returns>
        Task<List<MemoryItem>> GetMemoriesAsync(Guid userId, string? category = null);

        /// <summary>
        /// Updates an existing memory item across all stores.
        /// </summary>
        /// <param name="memoryId">Memory ID</param>
        /// <param name="content">New content</param>
        /// <param name="category">New category</param>
        /// <param name="isShared">New shared status</param>
        /// <param name="targetStore">Optional target store change</param>
        /// <returns>Success status</returns>
        Task<bool> UpdateMemoryAsync(Guid memoryId, string? content = null, string? category = null, bool? isShared = null, VectorTarget? targetStore = null);

        /// <summary>
        /// Deletes a memory item from all stores.
        /// </summary>
        /// <param name="memoryId">Memory ID</param>
        /// <returns>Success status</returns>
        Task<bool> DeleteMemoryAsync(Guid memoryId);

        /// <summary>
        /// Searches memories using semantic similarity across appropriate vector stores.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="query">Search query</param>
        /// <param name="maxResults">Maximum results to return</param>
        /// <param name="targetStore">Optional specific store to search</param>
        /// <returns>List of relevant memories with similarity scores</returns>
        Task<List<(MemoryItem Memory, float Similarity)>> SearchMemoriesAsync(Guid userId, string query, int maxResults = 10, VectorTarget? targetStore = null);

        /// <summary>
        /// Gets Neo4j graph-related memories for a user.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="category">Category filter (personal, facts, events, shared)</param>
        /// <param name="maxResults">Maximum results</param>
        /// <returns>Graph memories with relationships</returns>
        Task<List<MemoryItem>> GetGraphMemoriesAsync(Guid userId, string? category = null, int maxResults = 50);

        /// <summary>
        /// Gets document/knowledge memories from Weaviate.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="maxResults">Maximum results</param>
        /// <returns>Document memories</returns>
        Task<List<MemoryItem>> GetDocumentMemoriesAsync(Guid userId, int maxResults = 50);

        /// <summary>
        /// Reconciles inconsistencies between the three databases.
        /// Called by background service for healing.
        /// </summary>
        /// <param name="userId">Optional user ID to reconcile, null for all users</param>
        /// <returns>Reconciliation report</returns>
        Task<ReconciliationReport> ReconcileMemoriesAsync(Guid? userId = null);
    }

    /// <summary>
    /// Report of reconciliation operations between databases.
    /// </summary>
    public class ReconciliationReport
    {
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public int TotalMemoriesChecked { get; set; }
        public int InconsistenciesFound { get; set; }
        public int InconsistenciesFixed { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }
}
