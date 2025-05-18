using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services.VectorStore
{
    /// <summary>
    /// Represents a search hit from the vector store
    /// </summary>
    public class SearchHit
    {
        /// <summary>
        /// Gets or sets the ID of the item
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the similarity score (higher is better)
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// Gets or sets optional metadata associated with the hit
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Defines the scope of a vector search
    /// </summary>
    public enum VectorScope
    {
        /// <summary>
        /// Core vectors stored locally
        /// </summary>
        Core,

        /// <summary>
        /// Vectors stored on a remote server
        /// </summary>
        Remote,

        /// <summary>
        /// Both core and remote vectors
        /// </summary>
        All
    }

    /// <summary>
    /// Interface for vector storage and search
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Initializes the vector store
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Stores a vector with the given ID
        /// </summary>
        /// <param name="id">Item ID</param>
        /// <param name="embedding">Vector embedding</param>
        /// <param name="metadata">Optional metadata</param>
        /// <param name="scope">Vector scope</param>
        /// <returns>True if successful</returns>
        Task<bool> StoreVectorAsync(Guid id, float[] embedding, Dictionary<string, string>? metadata = null, VectorScope scope = VectorScope.Core);

        /// <summary>
        /// Searches for similar vectors
        /// </summary>
        /// <param name="queryVector">Query vector</param>
        /// <param name="limit">Maximum number of results</param>
        /// <param name="scope">Vector scope</param>
        /// <returns>List of search hits</returns>
        Task<List<SearchHit>> SearchAsync(float[] queryVector, int limit = 10, VectorScope scope = VectorScope.All);

        /// <summary>
        /// Deletes a vector with the given ID
        /// </summary>
        /// <param name="id">Item ID</param>
        /// <param name="scope">Vector scope</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteVectorAsync(Guid id, VectorScope scope = VectorScope.Core);

        /// <summary>
        /// Gets the status of the vector store
        /// </summary>
        /// <returns>Status information</returns>
        Task<Dictionary<string, object>> GetStatusAsync();

        /// <summary>
        /// Checks the health of the vector store
        /// </summary>
        /// <returns>True if healthy</returns>
        Task<bool> HealthCheckAsync();
    }
}
