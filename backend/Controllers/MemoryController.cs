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
        /// Gets all memory items for a user.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of memory items</returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetMemories(Guid userId)
        {
            var memories = await _dbContext.Memories
                .Where(m => m.UserId == userId)
                .ToListAsync();

            return Ok(memories);
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
