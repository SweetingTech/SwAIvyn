using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// API controller for managing background agents.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AgentsController : ControllerBase
    {
        private readonly IAgentService _agentService;
        private readonly ISimpleLoggerService _logger;

        public AgentsController(IAgentService agentService, ISimpleLoggerService logger)
        {
            _agentService = agentService 
                ?? throw new ArgumentNullException(nameof(agentService));
            _logger = logger 
                ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all agents for a user.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAgents([FromQuery][Required] Guid userId)
        {
            var agents = await _agentService.GetAgentsAsync(userId);
            return Ok(agents);
        }

        /// <summary>
        /// Creates a new agent.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var agent = await _agentService.CreateAgentAsync(
                    request.UserId,
                    request.Name,
                    request.Description,
                    request.Type
                );
                return Ok(agent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating agent");
                return StatusCode(500, "Failed to create agent");
            }
        }

        /// <summary>
        /// Updates an existing agent.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateAgent(Guid id, [FromBody] UpdateAgentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var agent = new Agent
            {
                Id = id,
                UserId = request.UserId,
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                Status = request.Status,
                LastRun = request.LastRun,
                TasksCompleted = request.TasksCompleted,
                Enabled = request.Enabled
            };

            var success = await _agentService.UpdateAgentAsync(agent);
            if (!success)
                return NotFound();

            return Ok(agent);
        }

        /// <summary>
        /// Deletes an agent.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteAgent(Guid id)
        {
            var success = await _agentService.DeleteAgentAsync(id);
            if (!success)
                return NotFound();

            return NoContent();
        }

        /// <summary>
        /// Starts an agent.
        /// </summary>
        [HttpPost("{id:guid}/start")]
        public async Task<IActionResult> StartAgent(Guid id)
        {
            var success = await _agentService.StartAgentAsync(id);
            if (!success)
                return NotFound();

            return Ok();
        }

        /// <summary>
        /// Stops an agent.
        /// </summary>
        [HttpPost("{id:guid}/stop")]
        public async Task<IActionResult> StopAgent(Guid id)
        {
            var success = await _agentService.StopAgentAsync(id);
            if (!success)
                return NotFound();

            return Ok();
        }
    }

    /// <summary>
    /// Request body for creating an agent.
    /// </summary>
    public class CreateAgentRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request body for updating an agent.
    /// </summary>
    public class UpdateAgentRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = "stopped";
        public DateTime? LastRun { get; set; }
        public int TasksCompleted { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
