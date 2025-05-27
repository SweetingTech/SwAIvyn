using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;
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
        private readonly IBrainService _brainService;

        public MemoryController(ApplicationDbContext dbContext, IBrainService brainService)
        {
            _dbContext = dbContext;
            _brainService = brainService;
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

            // Add to vector store for semantic search
            try
            {
                var metadata = new Dictionary<string, string>
                {
                    { "category", memory.Category },
                    { "userId", memory.UserId.ToString() },
                    { "isShared", memory.IsShared.ToString() },
                    { "createdAt", memory.CreatedAt.ToString("O") }
                };

                await _brainService.AddMemoryAsync(memory.Id, memory.Content, metadata);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request - memory is still saved to database
                Console.WriteLine($"Warning: Failed to add memory to vector store: {ex.Message}");
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

            // Remove from vector store
            try
            {
                await _brainService.DeleteMemoryAsync(id);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request - memory is already deleted from database
                Console.WriteLine($"Warning: Failed to delete memory from vector store: {ex.Message}");
            }

            return NoContent();
        }

        /// <summary>
        /// Rebuilds the vector store by adding all existing memories to it.
        /// This is useful for migrating existing memories to the vector store.
        /// </summary>
        /// <param name="userId">User ID to rebuild memories for</param>
        /// <returns>Number of memories processed</returns>
        [HttpPost("rebuild-vectors/{userId}")]
        public async Task<IActionResult> RebuildVectors(Guid userId)
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

                        await _brainService.AddMemoryAsync(memory.Id, memory.Content, metadata);
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to add memory {memory.Id} to vector store: {ex.Message}");
                        errors++;
                    }
                }

                return Ok(new { processed, errors, total = memories.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error rebuilding vectors: {ex.Message}");
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
