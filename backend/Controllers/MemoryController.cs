using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;
using SwAIvyn.Services.Graph;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for managing AI memory items.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MemoryController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IBrainGraphService _brainGraphService;

        public MemoryController(ApplicationDbContext dbContext, IBrainGraphService brainGraphService)
        {
            _dbContext = dbContext;
            _brainGraphService = brainGraphService;
        }

        /// <summary>
        /// Gets all memory items from both SQL database and Neo4j.
        /// Since this is a single-user application, we don't filter by userId.
        /// </summary>
        /// <param name="userId">User ID (ignored for single-user app)</param>
        /// <returns>List of memory items</returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetMemories(Guid userId)
        {
            try
            {
                // Get ALL memories from SQL database (single-user app)
                var sqlMemories = await _dbContext.Memories.ToListAsync();

                // Get ALL memory IDs from Neo4j (single-user app)
                // Use the default user ID for Neo4j operations
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var neo4jMemoryIds = await _brainGraphService.GetAllMemoryIdsAsync(defaultUserId);

                // Find memories that exist in Neo4j but not in SQL
                var sqlMemoryIds = sqlMemories.Select(m => m.Id).ToHashSet();
                var missingMemoryIds = neo4jMemoryIds.Where(id => !sqlMemoryIds.Contains(id)).ToList();

                // If there are memories in Neo4j that aren't in SQL, we need to create placeholder entries
                // This handles memories created through the chat system
                var allMemories = new List<MemoryItem>(sqlMemories);

                if (missingMemoryIds.Any())
                {
                    Console.WriteLine($"Found {missingMemoryIds.Count} memories in Neo4j that aren't in SQL database");

                    // For each missing memory, try to get its content from Neo4j and create a SQL entry
                    foreach (var memoryId in missingMemoryIds)
                    {
                        try
                        {
                            // Search for this specific memory in Neo4j to get its content
                            var searchResults = await _brainGraphService.SearchAsync($"memory_id:{memoryId}", limit: 1);

                            if (searchResults.Any())
                            {
                                var result = searchResults.First();
                                var hit = result.Hit;

                                // Get content from metadata (Neo4j vector store stores content in metadata)
                                var content = hit.Metadata?.TryGetValue("content", out var contentValue) == true ? contentValue : "Memory content not available";

                                // Extract metadata if available
                                var category = hit.Metadata?.TryGetValue("category", out var cat) == true ? cat : "Chat Memory";
                                var createdAtStr = hit.Metadata?.TryGetValue("createdAt", out var created) == true ? created : DateTime.UtcNow.ToString("O");
                                var isSharedStr = hit.Metadata?.TryGetValue("isShared", out var shared) == true ? shared : "false";

                                DateTime.TryParse(createdAtStr, out var createdAt);
                                bool.TryParse(isSharedStr, out var isShared);

                                // Create a memory item for display (but don't save to SQL yet to avoid duplicates)
                                var memoryItem = new MemoryItem
                                {
                                    Id = memoryId,
                                    UserId = defaultUserId, // Use default user ID for single-user app
                                    Content = content,
                                    Category = category,
                                    IsShared = isShared,
                                    CreatedAt = createdAt == default ? DateTime.UtcNow : createdAt,
                                    LastAccessed = DateTime.UtcNow
                                };

                                allMemories.Add(memoryItem);
                                Console.WriteLine($"Added Neo4j memory to display: {memoryId} - {content.Substring(0, Math.Min(50, content.Length))}...");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to retrieve memory {memoryId} from Neo4j: {ex.Message}");
                        }
                    }
                }

                // Sort by creation date (newest first)
                allMemories = allMemories.OrderByDescending(m => m.CreatedAt).ToList();

                Console.WriteLine($"Returning {allMemories.Count} total memories ({sqlMemories.Count} from SQL, {missingMemoryIds.Count} from Neo4j)");
                return Ok(allMemories);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting memories: {ex.Message}");
                // Fallback to just SQL memories if there's an error (single-user app)
                var fallbackMemories = await _dbContext.Memories
                    .OrderByDescending(m => m.CreatedAt)
                    .ToListAsync();
                return Ok(fallbackMemories);
            }
        }

        /// <summary>
        /// Creates a new memory item.
        /// </summary>
        /// <param name="request">Memory creation request</param>
        /// <returns>Created memory item</returns>
        [HttpPost]
        public async Task<IActionResult> CreateMemory([FromBody] CreateMemoryRequest request)
        {
            var memory = new MemoryItem
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Content = request.Content,
                Category = request.Category,
                IsShared = request.IsShared,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow
            };

            // Save to database
            _dbContext.Memories.Add(memory);
            await _dbContext.SaveChangesAsync();

            // Add to Neo4j graph database for persistent memory and relationships
            try
            {
                var metadata = new Dictionary<string, string>
                {
                    { "category", memory.Category },
                    { "userId", memory.UserId.ToString() },
                    { "isShared", memory.IsShared.ToString() },
                    { "createdAt", memory.CreatedAt.ToString("O") }
                };

                await _brainGraphService.AddMemoryAsync(memory.Id, memory.Content, metadata);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request - memory is still saved to database
                Console.WriteLine($"Warning: Failed to add memory to graph database: {ex.Message}");
            }

            return Ok(memory);
        }

        /// <summary>
        /// Updates an existing memory item.
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <param name="request">Memory update request</param>
        /// <returns>Updated memory item</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMemory(Guid id, [FromBody] UpdateMemoryRequest request)
        {
            var memory = await _dbContext.Memories.FindAsync(id);
            if (memory == null)
            {
                return NotFound();
            }

            memory.Content = request.Content;
            memory.Category = request.Category;
            memory.IsShared = request.IsShared;
            memory.LastAccessed = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(memory);
        }

        /// <summary>
        /// Deletes a memory item.
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <returns>Action result</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMemory(Guid id)
        {
            var memory = await _dbContext.Memories.FindAsync(id);
            if (memory == null)
            {
                return NotFound();
            }

            // Remove from database
            _dbContext.Memories.Remove(memory);
            await _dbContext.SaveChangesAsync();

            // Remove from graph database
            try
            {
                await _brainGraphService.DeleteMemoryAsync(id);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request - memory is already deleted from database
                Console.WriteLine($"Warning: Failed to delete memory from graph database: {ex.Message}");
            }

            return NoContent();
        }

        /// <summary>
        /// Rebuilds the graph database by adding all existing memories to it.
        /// This is useful for migrating existing memories to the graph database.
        /// </summary>
        /// <param name="userId">User ID to rebuild memories for</param>
        /// <returns>Number of memories processed</returns>
        [HttpPost("rebuild-graph/{userId}")]
        public async Task<IActionResult> RebuildGraph(Guid userId)
        {
            try
            {
                var memories = await _dbContext.Memories
                    .Where(m => m.UserId == userId)
                    .ToListAsync();

                int processed = 0;
                int errors = 0;

                foreach (var memory in memories)
                {
                    try
                    {
                        var metadata = new Dictionary<string, string>
                        {
                            { "category", memory.Category },
                            { "userId", memory.UserId.ToString() },
                            { "isShared", memory.IsShared.ToString() },
                            { "createdAt", memory.CreatedAt.ToString("O") }
                        };

                        await _brainGraphService.AddMemoryAsync(memory.Id, memory.Content, metadata);
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to add memory {memory.Id} to graph database: {ex.Message}");
                        errors++;
                    }
                }

                return Ok(new { processed, errors, total = memories.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error rebuilding graph: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Request model for creating a memory item.
    /// </summary>
    public class CreateMemoryRequest
    {
        public Guid UserId { get; set; }
        public string Content { get; set; }
        public string Category { get; set; }
        public bool IsShared { get; set; }
    }

    /// <summary>
    /// Request model for updating a memory item.
    /// </summary>
    public class UpdateMemoryRequest
    {
        public string Content { get; set; }
        public string Category { get; set; }
        public bool IsShared { get; set; }
    }
}
