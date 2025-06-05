using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services.VectorStore
{
    /// <summary>
    /// Interface for routing vector operations between different stores
    /// Neo4j vectors = long-term "brain" memories
    /// Weaviate vectors = file/document-level uploads
    /// </summary>
    public interface IVectorRouter
    {
        /// <summary>
        /// Searches for memories in Neo4j vector store
        /// </summary>
        /// <param name="userId">User ID to filter by</param>
        /// <param name="query">Search query text</param>
        /// <param name="k">Number of results to return</param>
        /// <returns>List of memory search hits</returns>
        Task<IReadOnlyList<SearchHit>> SearchMemoryAsync(Guid userId, string query, int k = 8);

        /// <summary>
        /// Searches for uploaded documents in Weaviate vector store
        /// </summary>
        /// <param name="userId">User ID to filter by</param>
        /// <param name="query">Search query text</param>
        /// <param name="k">Number of results to return</param>
        /// <returns>List of upload search hits</returns>
        Task<IReadOnlyList<SearchHit>> SearchUploadsAsync(Guid userId, string query, int k = 8);

        /// <summary>
        /// Searches for general knowledge documents in Weaviate vector store (no user filtering)
        /// </summary>
        /// <param name="query">Search query text</param>
        /// <param name="k">Number of results to return</param>
        /// <returns>List of knowledge document search hits</returns>
        Task<IReadOnlyList<SearchHit>> SearchKnowledgeAsync(string query, int k = 8);

        /// <summary>
        /// Stores a memory vector in Neo4j
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <param name="vector">Embedding vector</param>
        /// <param name="metadata">Metadata including userId, category, content, etc.</param>
        /// <returns>True if successful</returns>
        Task<bool> StoreMemoryAsync(Guid id, float[] vector, IDictionary<string, string> metadata);

        /// <summary>
        /// Stores an upload chunk vector in Weaviate
        /// </summary>
        /// <param name="id">Chunk ID</param>
        /// <param name="vector">Embedding vector</param>
        /// <param name="metadata">Metadata including userId, fileId, page, etc.</param>
        /// <returns>True if successful</returns>
        Task<bool> StoreUploadChunkAsync(Guid id, float[] vector, IDictionary<string, string> metadata);

        /// <summary>
        /// Deletes a memory from Neo4j
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteMemoryAsync(Guid id);

        /// <summary>
        /// Deletes an upload chunk from Weaviate
        /// </summary>
        /// <param name="id">Chunk ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteUploadChunkAsync(Guid id);

        /// <summary>
        /// Gets the status of both vector stores
        /// </summary>
        /// <returns>Combined status information</returns>
        Task<Dictionary<string, object>> GetStatusAsync();

        /// <summary>
        /// Checks the health of both vector stores
        /// </summary>
        /// <returns>Health status</returns>
        Task<bool> HealthCheckAsync();
    }
}
