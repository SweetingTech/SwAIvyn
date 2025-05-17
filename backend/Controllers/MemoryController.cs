using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
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

        public MemoryController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
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

            _dbContext.Memories.Add(memory);
            await _dbContext.SaveChangesAsync();

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

            _dbContext.Memories.Remove(memory);
            await _dbContext.SaveChangesAsync();

            return NoContent();
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
