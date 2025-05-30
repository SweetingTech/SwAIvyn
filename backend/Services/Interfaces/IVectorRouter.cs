using SwAIvyn.Data.Entities;
using SwAIvyn.Enums;

namespace SwAIvyn.Services.Interfaces
{
    /// <summary>
    /// Interface for routing vector operations to appropriate vector store.
    /// Handles intelligent routing between Neo4j and Weaviate based on content type.
    /// </summary>
    public interface IVectorRouter
    {
        /// <summary>
        /// Determines the optimal vector store for a memory item based on content and metadata.
        /// </summary>
        /// <param name="memory">Memory item to analyze</param>
        /// <param name="metadata">Additional metadata</param>
        /// <returns>Recommended vector target</returns>
        VectorTarget DetermineOptimalStore(MemoryItem memory, Dictionary<string, string>? metadata = null);

        /// <summary>
        /// Adds a memory to the specified vector store.
        /// </summary>
        /// <param name="memory">Memory to add</param>
        /// <param name="targetStore">Target vector store</param>
        /// <param name="metadata">Additional metadata</param>
        /// <returns>Success status</returns>
        Task<bool> AddToVectorStoreAsync(MemoryItem memory, VectorTarget targetStore, Dictionary<string, string>? metadata = null);

        /// <summary>
        /// Updates a memory in the specified vector store.
        /// </summary>
        /// <param name="memoryId">Memory ID</param>
        /// <param name="content">New content</param>
        /// <param name="targetStore">Target vector store</param>
        /// <param name="metadata">Updated metadata</param>
        /// <returns>Success status</returns>
        Task<bool> UpdateInVectorStoreAsync(Guid memoryId, string content, VectorTarget targetStore, Dictionary<string, string>? metadata = null);

        /// <summary>
        /// Removes a memory from the specified vector store.
        /// </summary>
        /// <param name="memoryId">Memory ID</param>
        /// <param name="targetStore">Target vector store</param>
        /// <returns>Success status</returns>
        Task<bool> RemoveFromVectorStoreAsync(Guid memoryId, VectorTarget targetStore);

        /// <summary>
        /// Searches for similar memories in the specified vector store.
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="targetStore">Target vector store</param>
        /// <param name="userId">User ID for filtering</param>
        /// <param name="maxResults">Maximum results</param>
        /// <returns>Similar memories with scores</returns>
        Task<List<(Guid MemoryId, string Content, float Similarity)>> SearchVectorStoreAsync(string query, VectorTarget targetStore, Guid userId, int maxResults = 10);

        /// <summary>
        /// Fan-out search across multiple vector stores and merge results.
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="userId">User ID for filtering</param>
        /// <param name="maxResults">Maximum results per store</param>
        /// <returns>Merged and ranked results from all stores</returns>
        Task<List<(Guid MemoryId, string Content, float Similarity, VectorTarget Source)>> FanOutSearchAsync(string query, Guid userId, int maxResults = 10);
    }
}
