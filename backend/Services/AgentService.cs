using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for managing background agents.
    /// </summary>
    public interface IAgentService
    {
        Task<List<Agent>> GetAgentsAsync(Guid userId);
        Task<Agent?> GetAgentAsync(Guid agentId);
        Task<Agent> CreateAgentAsync(Guid userId, string name, string description, string type);
        Task<bool> UpdateAgentAsync(Agent agent);
        Task<bool> DeleteAgentAsync(Guid agentId);
        Task<bool> StartAgentAsync(Guid agentId);
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
            // Placeholder for actual execution logic
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
