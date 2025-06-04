using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

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
            _agentService = agentService;
            _logger = logger;
        }

        /// <summary>
        /// Gets all agents for a user.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of agents</returns>
        [HttpGet]
        public async Task<IActionResult> GetAgents([FromQuery] Guid userId)
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
            var agent = await _agentService.CreateAgentAsync(request.UserId, request.Name, request.Description, request.Type);
            return Ok(agent);
        }

        /// <summary>
        /// Updates an existing agent.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAgent(Guid id, [FromBody] UpdateAgentRequest request)
        {
            var agent = new Agent
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                Status = request.Status,
                TasksCompleted = request.TasksCompleted,
                Enabled = request.Enabled,
                LastRun = request.LastRun,
                UserId = request.UserId
            };

            var success = await _agentService.UpdateAgentAsync(agent);
            if (!success)
                return NotFound();

            return Ok(agent);
        }

        /// <summary>
        /// Deletes an agent.
        /// </summary>
        [HttpDelete("{id}")]
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
        [HttpPost("{id}/start")]
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
        [HttpPost("{id}/stop")]
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
        public bool Enabled { get; set; }
    }
}
