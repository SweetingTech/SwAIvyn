using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for managing agents.
    /// </summary>
    public interface IAgentService
    {
        /// <summary>
        /// Gets all agents for a user.
        /// </summary>
        Task<List<Agent>> GetAgentsAsync(Guid? userId = null);

        /// <summary>
        /// Gets an agent by ID.
        /// </summary>
        Task<Agent?> GetAgentAsync(Guid id);

        /// <summary>
        /// Creates a new agent.
        /// </summary>
        Task<Agent> CreateAgentAsync(Agent agent);

        /// <summary>
        /// Updates an existing agent.
        /// </summary>
        Task<Agent?> UpdateAgentAsync(Agent agent);

        /// <summary>
        /// Deletes an agent by ID.
        /// </summary>
        Task<bool> DeleteAgentAsync(Guid id);

        /// <summary>
        /// Starts an agent (stub).
        /// </summary>
        Task<bool> StartAgentAsync(Guid id);

        /// <summary>
        /// Stops an agent (stub).
        /// </summary>
        Task<bool> StopAgentAsync(Guid id);
    }

    /// <summary>
    /// Implementation of agent management service.
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
        public async Task<List<Agent>> GetAgentsAsync(Guid? userId = null)
        {
            var query = _dbContext.Agents.AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(a => a.UserId == userId.Value);
            }
            return await query.ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<Agent?> GetAgentAsync(Guid id)
        {
            return await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == id);
        }

        /// <inheritdoc/>
        public async Task<Agent> CreateAgentAsync(Agent agent)
        {
            agent.Id = Guid.NewGuid();
            _dbContext.Agents.Add(agent);
            await _dbContext.SaveChangesAsync();
            _logger.LogInfo($"Created agent {agent.Name} ({agent.Id})");
            return agent;
        }

        /// <inheritdoc/>
        public async Task<Agent?> UpdateAgentAsync(Agent agent)
        {
            var existing = await _dbContext.Agents.FindAsync(agent.Id);
            if (existing == null)
            {
                return null;
            }

            existing.Name = agent.Name;
            existing.Description = agent.Description;
            existing.Type = agent.Type;
            existing.Status = agent.Status;
            existing.LastRun = agent.LastRun;
            existing.TasksCompleted = agent.TasksCompleted;
            existing.Enabled = agent.Enabled;
            await _dbContext.SaveChangesAsync();
            return existing;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAgentAsync(Guid id)
        {
            var existing = await _dbContext.Agents.FindAsync(id);
            if (existing == null)
            {
                return false;
            }

            _dbContext.Agents.Remove(existing);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public Task<bool> StartAgentAsync(Guid id)
        {
            // Placeholder for execution logic
            _logger.LogInfo($"Starting agent {id}");
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<bool> StopAgentAsync(Guid id)
        {
            // Placeholder for stop logic
            _logger.LogInfo($"Stopping agent {id}");
            return Task.FromResult(true);
        }
    }
}
