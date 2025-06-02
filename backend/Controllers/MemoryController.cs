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
        private readonly ILogger<MemoryController> _logger;

        public MemoryController(IMemoryService memoryService, ILogger<MemoryController> logger)
        {
            _memoryService = memoryService;
            _logger = logger;
        }

        // GET api/memory?category={category}
        [HttpGet]
        public async Task<IActionResult> GetMemories([FromQuery] string? category = null)
        {
            try
            {
                _logger.LogInformation("Getting memories (single-user app), category: {Category}", category);

                var memories = await _memoryService.GetMemoriesAsync(Guid.Empty, category);

                _logger.LogInformation("Retrieved {Count} memories", memories.Count);

                var response = new { memories = memories };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting memories");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
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

        // POST api/memory
        [HttpPost]
        public async Task<IActionResult> CreateMemory([FromBody] CreateMemoryRequest request)
        {
            try
            {
                _logger.LogInformation("Creating global memory (single-user app) with content: {Content}", request.Content);

                var memory = new MemoryItem
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Empty, // Use Guid.Empty for global memories (single-user app like character cards)
                    Content = request.Content,
                    Category = request.Category,
                    IsShared = request.IsShared,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    TargetStore = request.TargetStore ?? VectorTarget.Neo4j
                };

                _logger.LogInformation("Memory object created with ID {MemoryId}, calling CreateMemoryAsync", memory.Id);

                var (success, created) = await _memoryService.CreateMemoryAsync(memory);

                _logger.LogInformation("CreateMemoryAsync returned: success={Success}, created={Created}", success, created != null);

                if (!success || created == null)
                {
                    _logger.LogWarning("Memory creation failed: success={Success}, created={Created}", success, created != null);
                    return BadRequest("Memory creation failed");
                }

                _logger.LogInformation("Memory created successfully with ID {MemoryId}", created.Id);

                return CreatedAtAction(nameof(GetMemories),
                    new { category = (string?)null },
                    created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while creating memory");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
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
