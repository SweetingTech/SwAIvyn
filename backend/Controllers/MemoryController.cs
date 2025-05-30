// SPDX-License-Identifier: MIT
using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Data.Entities;
using SwAIvyn.Enums;
using SwAIvyn.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MemoryController : ControllerBase
    {
        private readonly IMemoryService _memoryService;

        public MemoryController(IMemoryService memoryService)
        {
            _memoryService = memoryService;
        }

        // GET api/memory?userId={userId}&category={category}
        [HttpGet]
        public async Task<IActionResult> GetMemories([FromQuery] Guid userId, [FromQuery] string? category = null)
        {
            var memories = await _memoryService.GetMemoriesAsync(userId, category);
            return Ok(memories);
        }

        // GET api/memory/graph?userId={userId}&category={category}&maxResults={maxResults}
        [HttpGet("graph")]
        public async Task<IActionResult> GetGraphMemories(
            [FromQuery] Guid userId,
            [FromQuery] string? category = null,
            [FromQuery] int maxResults = 50)
        {
            var memories = await _memoryService.GetGraphMemoriesAsync(userId, category, maxResults);
            return Ok(new { source = "neo4j", memories });
        }

        // GET api/memory/documents?userId={userId}&maxResults={maxResults}
        [HttpGet("documents")]
        public async Task<IActionResult> GetDocumentMemories(
            [FromQuery] Guid userId,
            [FromQuery] int maxResults = 50)
        {
            var memories = await _memoryService.GetDocumentMemoriesAsync(userId, maxResults);
            return Ok(new { source = "weaviate", memories });
        }

        // GET api/memory/search?userId={userId}&query={query}&maxResults={maxResults}&targetStore={targetStore}
        [HttpGet("search")]
        public async Task<IActionResult> SearchMemories(
            [FromQuery] Guid userId,
            [FromQuery] string query,
            [FromQuery] int maxResults = 10,
            [FromQuery] VectorTarget? targetStore = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query parameter is required");

            var results = await _memoryService.SearchMemoriesAsync(userId, query, maxResults, targetStore);
            var response = results.Select(r => new
            {
                Memory = r.Memory,
                Similarity = r.Similarity
            });
            return Ok(response);
        }

        // POST api/memory?userId={userId}
        [HttpPost]
        public async Task<IActionResult> CreateMemory(
            [FromQuery] Guid userId,
            [FromBody] CreateMemoryRequest request)
        {
            var memory = new MemoryItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Content = request.Content,
                Category = request.Category,
                IsShared = request.IsShared,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                TargetStore = request.TargetStore ?? VectorTarget.Neo4j
            };

            var (success, created) = await _memoryService.CreateMemoryAsync(memory);
            if (!success || created == null)
                return BadRequest("Memory creation failed");

            return CreatedAtAction(nameof(GetMemories),
                new { userId, category = (string?)null },
                created);
        }

        // PUT api/memory/{id}?userId={userId}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMemory(
            Guid id,
            [FromQuery] Guid userId,
            [FromBody] UpdateMemoryRequest request)
        {
            var success = await _memoryService.UpdateMemoryAsync(
                id,
                request.Content,
                request.Category,
                request.IsShared,
                request.TargetStore);

            if (!success) return NotFound();
            return NoContent();
        }

        // DELETE api/memory/{id}?userId={userId}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMemory(Guid id)
        {
            var success = await _memoryService.DeleteMemoryAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // POST api/memory/reconcile?userId={userId}
        [HttpPost("reconcile")]
        public async Task<IActionResult> ReconcileMemories([FromQuery] Guid? userId = null)
        {
            var report = await _memoryService.ReconcileMemoriesAsync(userId);
            return Ok(report);
        }
    }

    public class CreateMemoryRequest
    {
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = "general";
        public bool IsShared { get; set; } = false;
        public VectorTarget? TargetStore { get; set; }
    }

    public class UpdateMemoryRequest
    {
        public string? Content { get; set; }
        public string? Category { get; set; }
        public bool? IsShared { get; set; }
        public VectorTarget? TargetStore { get; set; }
    }
}
