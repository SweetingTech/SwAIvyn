using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using SwAIvyn.Services.VectorStore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for managing the brain
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BrainController : ControllerBase
    {
        private readonly IBrainService _brainService;
        private readonly ISimpleLoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the BrainController
        /// </summary>
        /// <param name="brainService">Brain service</param>
        /// <param name="logger">Logger service</param>
        public BrainController(
            IBrainService brainService,
            ISimpleLoggerService logger)
        {
            _brainService = brainService;
            _logger = logger;
        }

        /// <summary>
        /// Adds a memory to the brain
        /// </summary>
        /// <param name="request">Add memory request</param>
        /// <returns>Success status</returns>
        [HttpPost("memory")]
        public async Task<IActionResult> AddMemory([FromBody] AddMemoryRequest request)
        {
            try
            {
                var scope = ParseScope(request.Scope);
                var success = await _brainService.AddMemoryAsync(request.Id, request.Text, request.Metadata, scope);
                
                if (success)
                    return Ok();
                else
                    return BadRequest("Failed to add memory");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding memory {request.Id}", ex);
                return StatusCode(500, "An error occurred while adding the memory");
            }
        }

        /// <summary>
        /// Searches the brain for similar memories
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="limit">Maximum number of results (default: 10)</param>
        /// <param name="scope">Search scope (default: all)</param>
        /// <returns>List of search hits</returns>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 10, [FromQuery] string scope = "all")
        {
            try
            {
                var vectorScope = ParseScope(scope);
                var results = await _brainService.SearchAsync(query, limit, vectorScope);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching brain", ex);
                return StatusCode(500, "An error occurred while searching the brain");
            }
        }

        /// <summary>
        /// Deletes a memory from the brain
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <param name="scope">Delete scope (default: core)</param>
        /// <returns>Success status</returns>
        [HttpDelete("memory/{id}")]
        public async Task<IActionResult> DeleteMemory(Guid id, [FromQuery] string scope = "core")
        {
            try
            {
                var vectorScope = ParseScope(scope);
                var success = await _brainService.DeleteMemoryAsync(id, vectorScope);
                
                if (success)
                    return Ok();
                else
                    return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting memory {id}", ex);
                return StatusCode(500, "An error occurred while deleting the memory");
            }
        }

        /// <summary>
        /// Gets the status of the brain
        /// </summary>
        /// <returns>Status information</returns>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _brainService.GetStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting brain status", ex);
                return StatusCode(500, "An error occurred while getting the brain status");
            }
        }

        /// <summary>
        /// Parses a scope string to a VectorScope enum
        /// </summary>
        /// <param name="scope">Scope string</param>
        /// <returns>VectorScope enum</returns>
        private VectorScope ParseScope(string scope)
        {
            return scope?.ToLower() switch
            {
                "core" => VectorScope.Core,
                "remote" => VectorScope.Remote,
                "all" => VectorScope.All,
                _ => VectorScope.Core
            };
        }
    }

    /// <summary>
    /// Request to add a memory to the brain
    /// </summary>
    public class AddMemoryRequest
    {
        /// <summary>
        /// Gets or sets the memory ID
        /// </summary>
        [Required]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the memory text
        /// </summary>
        [Required]
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the memory metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// Gets or sets the memory scope (core, remote, all)
        /// </summary>
        public string Scope { get; set; } = "core";
    }
}
