// filepath: backend/Models/MemorySearchRequest.cs
namespace SwAIvyn.Models
{
    /// <summary>
    /// Request model for memory search debugging
    /// </summary>
    public class MemorySearchRequest
    {
        /// <summary>
        /// The search query to test
        /// </summary>
        public required string Query { get; set; }
        
        /// <summary>
        /// Maximum number of results to return (optional, defaults to 5)
        /// </summary>
        public int? Limit { get; set; }
    }
}
