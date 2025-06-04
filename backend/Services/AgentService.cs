using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for managing background agents.
    /// </summary>
    public interface IAgentService
    {
        /// <summary>
        /// Retrieves all agents for the given user.
        /// </summary>
        Task<List<Agent>> GetAgentsAsync(Guid userId);

        /// <summary>
        /// Retrieves a single agent by its ID.
        /// </summary>
        Task<Agent?> GetAgentAsync(Guid agentId);

        /// <summary>
        /// Creates a new agent for the specified user.
        /// </summary>
        Task<Agent> CreateAgentAsync(Guid userId, string name, string description, string type);

        /// <summary>
        /// Updates an existing agent. Returns false if not found.
        /// </summary>
        Task<bool> UpdateAgentAsync(Agent agent);

        /// <summary>
        /// Deletes an agent by its ID. Returns false if not found.
        /// </summary>
        Task<bool> DeleteAgentAsync(Guid agentId);

        /// <summary>
        /// Starts an agent (marks it enabled and sets status to "running").
        /// </summary>
        Task<bool> StartAgentAsync(Guid agentId);

        /// <summary>
        /// Stops an agent (marks it disabled and sets status to "stopped").
        /// </summary>
        Task<bool> StopAgentAsync(Guid agentId);
    }

    /// <summary>
    /// Service implementation for agent management.
    /// </summary>
    public class AgentService : IAgentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ISimpleLoggerService _logger;

        public AgentService(ApplicationDbContext dbContext, ISimpleLoggerService logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<Agent>> GetAgentsAsync(Guid userId)
        {
            return await _dbContext.Agents
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<Agent?> GetAgentAsync(Guid agentId)
        {
            return await _dbContext.Agents.FindAsync(agentId);
        }

        /// <inheritdoc/>
        public async Task<Agent> CreateAgentAsync(Guid userId, string name, string description, string type)
        {
            var agent = new Agent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = name,
                Description = description,
                Type = type,
                Status = "stopped",
                Enabled = false,
                TasksCompleted = 0,
                LastRun = null
            };

            _dbContext.Agents.Add(agent);
            await _dbContext.SaveChangesAsync();

            _logger.LogInfo($"Created agent {agent.Id} for user {userId}");
            return agent;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAgentAsync(Agent agent)
        {
            var existing = await _dbContext.Agents.FindAsync(agent.Id);
            if (existing == null)
                return false;

            existing.Name = agent.Name;
            existing.Description = agent.Description;
            existing.Type = agent.Type;
            existing.Enabled = agent.Enabled;
            existing.Status = agent.Status;
            existing.TasksCompleted = agent.TasksCompleted;
            existing.LastRun = agent.LastRun;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAgentAsync(Guid agentId)
        {
            var agent = await _dbContext.Agents.FindAsync(agentId);
            if (agent == null)
                return false;

            _dbContext.Agents.Remove(agent);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> StartAgentAsync(Guid agentId)
        {
            var agent = await _dbContext.Agents.FindAsync(agentId);
            if (agent == null)
                return false;

            agent.Enabled = true;
            agent.Status = "running";
            agent.LastRun = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> StopAgentAsync(Guid agentId)
        {
            var agent = await _dbContext.Agents.FindAsync(agentId);
            if (agent == null)
                return false;

            agent.Enabled = false;
            agent.Status = "stopped";
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
