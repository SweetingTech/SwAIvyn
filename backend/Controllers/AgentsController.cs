using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;
using System;
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

        private static Guid DefaultUserId => Guid.Parse("00000000-0000-0000-0000-000000000001");

        /// <summary>
        /// Gets all agents for the default user.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAgents()
        {
            var agents = await _agentService.GetAgentsAsync(DefaultUserId);
            return Ok(agents);
        }

        /// <summary>
        /// Creates a new agent.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateAgent([FromBody] Agent agent)
        {
            agent.UserId = DefaultUserId;
            var created = await _agentService.CreateAgentAsync(agent);
            return Ok(created);
        }

        /// <summary>
        /// Updates an existing agent.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAgent(Guid id, [FromBody] Agent agent)
        {
            agent.Id = id;
            var updated = await _agentService.UpdateAgentAsync(agent);
            if (updated == null)
            {
                return NotFound();
            }
            return Ok(updated);
        }

        /// <summary>
        /// Deletes an agent.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAgent(Guid id)
        {
            var success = await _agentService.DeleteAgentAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }

        /// <summary>
        /// Starts the specified agent.
        /// </summary>
        [HttpPost("{id}/start")]
        public async Task<IActionResult> StartAgent(Guid id)
        {
            var success = await _agentService.StartAgentAsync(id);
            return success ? Ok() : StatusCode(500);
        }

        /// <summary>
        /// Stops the specified agent.
        /// </summary>
        [HttpPost("{id}/stop")]
        public async Task<IActionResult> StopAgent(Guid id)
        {
            var success = await _agentService.StopAgentAsync(id);
            return success ? Ok() : StatusCode(500);
        }
    }
}
