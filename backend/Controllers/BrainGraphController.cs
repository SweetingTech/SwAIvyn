using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using SwAIvyn.Services.Graph;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for managing the brain graph
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BrainGraphController : ControllerBase
    {
        private readonly IBrainGraphService _brainGraphService;
        private readonly ISimpleLoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the BrainGraphController
        /// </summary>
        /// <param name="brainGraphService">Brain graph service</param>
        /// <param name="logger">Logger service</param>
        public BrainGraphController(
            IBrainGraphService brainGraphService,
            ISimpleLoggerService logger)
        {
            _brainGraphService = brainGraphService;
            _logger = logger;
        }

        /// <summary>
        /// Adds a memory to the brain graph
        /// </summary>
        /// <param name="request">Add memory request</param>
        /// <returns>Success status</returns>
        [HttpPost("memory")]
        public async Task<IActionResult> AddMemory([FromBody] AddBrainMemoryRequest request)
        {
            try
            {
                var success = await _brainGraphService.AddMemoryAsync(
                    request.Id,
                    request.Text,
                    request.Metadata);

                if (success)
                    return Ok();
                else
                    return BadRequest("Failed to add memory to brain graph");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding memory {request.Id} to brain graph", ex);
                return StatusCode(500, "An error occurred while adding the memory to the brain graph");
            }
        }

        /// <summary>
        /// Adds a relationship between two nodes
        /// </summary>
        /// <param name="request">Add relationship request</param>
        /// <returns>Success status</returns>
        [HttpPost("relationship")]
        public async Task<IActionResult> AddRelationship([FromBody] AddRelationshipRequest request)
        {
            try
            {
                var success = await _brainGraphService.AddRelationshipAsync(
                    request.SourceId,
                    request.TargetId,
                    request.RelationshipType,
                    request.Properties);

                if (success)
                    return Ok();
                else
                    return BadRequest("Failed to add relationship to brain graph");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding relationship between {request.SourceId} and {request.TargetId}", ex);
                return StatusCode(500, "An error occurred while adding the relationship to the brain graph");
            }
        }

        /// <summary>
        /// Searches the brain graph
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="limit">Maximum number of results (default: 10)</param>
        /// <returns>List of search results</returns>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 10)
        {
            try
            {
                var results = await _brainGraphService.SearchAsync(query, limit);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching brain graph", ex);
                return StatusCode(500, "An error occurred while searching the brain graph");
            }
        }

        /// <summary>
        /// Gets the graph visualization data for a memory
        /// </summary>
        /// <param name="id">Memory ID</param>
        /// <param name="depth">Relationship depth (default: 2)</param>
        /// <returns>Graph visualization data</returns>
        [HttpGet("visualization/{id}")]
        public async Task<IActionResult> GetVisualization(Guid id, [FromQuery] int depth = 2)
        {
            try
            {
                var data = await _brainGraphService.GetGraphVisualizationAsync(id, depth);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting graph visualization for memory {id}", ex);
                return StatusCode(500, "An error occurred while getting the graph visualization");
            }
        }

        /// <summary>
        /// Gets the status of the brain graph service
        /// </summary>
        /// <returns>Status information</returns>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _brainGraphService.GetStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting brain graph status", ex);
                return StatusCode(500, "An error occurred while getting the brain graph status");
            }
        }
    }

    /// <summary>
    /// Request to add a memory to the brain graph
    /// </summary>
    public class AddBrainMemoryRequest
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
    }

    /// <summary>
    /// Request to add a relationship to the brain graph
    /// </summary>
    public class AddRelationshipRequest
    {
        /// <summary>
        /// Gets or sets the source node ID
        /// </summary>
        [Required]
        public Guid SourceId { get; set; }

        /// <summary>
        /// Gets or sets the target node ID
        /// </summary>
        [Required]
        public Guid TargetId { get; set; }

        /// <summary>
        /// Gets or sets the relationship type
        /// </summary>
        [Required]
        public string RelationshipType { get; set; }

        /// <summary>
        /// Gets or sets the relationship properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }
    }
}
