using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data; // For SwAIvynDbContext and Agent
using SwAIvyn.Services; // For IAgentService

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgentsController : ControllerBase
    {
        private readonly IAgentService _agentService;
        private readonly SwAIvynDbContext _db;

        public AgentsController(IAgentService agentService, SwAIvynDbContext db)
        {
            _agentService = agentService;
            _db = db;
        }

        /// <summary>
        /// GET /api/agents
        /// Returns all agents (Id, Name, Status, LastRun, TasksCompleted, etc.)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllAgents()
        {
            // Ensure that we are querying the correct Agent entity from SwAIvyn.Data
            var agents = await _db.Agents.ToListAsync();
            return Ok(agents);
        }

        /// <summary>
        /// POST /api/agents/{id}/start
        /// Kicks off the agent: updates DB and dispatches to worker via AgentService.
        /// </summary>
        [HttpPost("{id}/start")]
        public async Task<IActionResult> Start(Guid id)
        {
            await _agentService.StartAgentAsync(id);
            return Ok(new { message = $"Agent {id} start request processed." });
        }

        /// <summary>
        /// POST /api/agents/{id}/stop
        /// Marks the agent as stopped in the DB (and optionally cancels on the worker).
        /// </summary>
        [HttpPost("{id}/stop")]
        public async Task<IActionResult> Stop(Guid id)
        {
            await _agentService.StopAgentAsync(id);
            return Ok(new { message = $"Agent {id} stop request processed." });
        }
    }
}
