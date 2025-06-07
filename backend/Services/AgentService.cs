using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwAIvyn.Data; // For SwAIvynDbContext and Agent

namespace SwAIvyn.Services
{
    public class AgentService : IAgentService
    {
        private readonly SwAIvynDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<AgentService> _logger;

        public AgentService(
            SwAIvynDbContext db,
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<AgentService> logger)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public async Task StartAgentAsync(Guid agentId)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
            if (agent == null)
            {
                _logger.LogError("StartAgentAsync: Agent {AgentId} not found", agentId);
                return;
            }

            if (agent.Status == "running")
            {
                _logger.LogInformation("StartAgentAsync: Agent {AgentId} is already running", agentId);
                return;
            }

            agent.Status = "running";
            agent.LastRun = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var workerPayload = new
            {
                agent_id = agent.Id.ToString(),
                payload = new
                {
                    goal = agent.Goal ?? string.Empty,
                    agentName = agent.Name
                }
            };

            var workerUrlBase = _config["WorkerApiBaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrEmpty(workerUrlBase))
            {
                _logger.LogError("StartAgentAsync: WorkerApiBaseUrl is not set in configuration");
                agent.Status = "stopped"; // Or an error status like "error_config"
                await _db.SaveChangesAsync();
                return;
            }

            var client = _httpClientFactory.CreateClient("WorkerApi");
            client.BaseAddress = new Uri(workerUrlBase);

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync("/tasks", workerPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartAgentAsync: Failed to POST to worker for Agent {AgentId}", agentId);
                agent.Status = "stopped"; // Or an error status
                await _db.SaveChangesAsync();
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "StartAgentAsync: Worker returned {StatusCode} for Agent {AgentId}",
                    response.StatusCode, agentId
                );
                agent.Status = "stopped"; // Or an error status
                await _db.SaveChangesAsync();
                return;
            }

            var respObj = await response.Content.ReadFromJsonAsync<WorkerResponse>();
            if (respObj == null || string.IsNullOrEmpty(respObj.TaskId))
            {
                _logger.LogError("StartAgentAsync: Worker returned no task_id for Agent {AgentId}", agentId);
                agent.Status = "stopped"; // Or an error status
                await _db.SaveChangesAsync();
                return;
            }

            string taskId = respObj.TaskId;
            _logger.LogInformation(
                "StartAgentAsync: Dispatched Agent {AgentId} to worker => TaskId {TaskId}",
                agentId, taskId
            );

            // Fire and forget the polling task
            _ = Task.Run(async () =>
            {
                await PollWorkerUntilComplete(taskId, agentId);
            });
        }

        public async Task StopAgentAsync(Guid agentId)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
            if (agent == null) return;

            if (agent.Status != "running")
            {
                _logger.LogInformation("StopAgentAsync: Agent {AgentId} is not running", agentId);
                return;
            }

            agent.Status = "stopped";
            await _db.SaveChangesAsync();
            _logger.LogInformation("StopAgentAsync: Agent {AgentId} forcibly stopped", agentId);
        }

        private class WorkerResponse
        {
            public string TaskId { get; set; } = string.Empty;
        }

        private class WorkerStatusResponse
        {
            public string Status { get; set; } = string.Empty;
            public string? Result { get; set; }
        }

        private async Task PollWorkerUntilComplete(string taskId, Guid agentId)
        {
            var client = _httpClientFactory.CreateClient("WorkerApi");
            var workerUrlBase = _config["WorkerApiBaseUrl"]?.TrimEnd('/');

            // Enhancement: Add null check for workerUrlBase in polling loop
            if (string.IsNullOrEmpty(workerUrlBase))
            {
                _logger.LogError("PollWorkerUntilComplete: WorkerApiBaseUrl is not set. Cannot poll for TaskId {TaskId}", taskId);
                var agentToUpdate = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
                if (agentToUpdate != null && agentToUpdate.Status == "running")
                {
                    agentToUpdate.Status = "error_config"; // Custom status for config error
                    await _db.SaveChangesAsync();
                    _logger.LogWarning("PollWorkerUntilComplete: Agent {AgentId} status set to error_config due to missing WorkerApiBaseUrl", agentId);
                }
                return;
            }
            client.BaseAddress = new Uri(workerUrlBase);

            while (true)
            {
                // Enhancement: Check agent status before polling
                var currentAgentState = await _db.Agents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId);
                if (currentAgentState == null || currentAgentState.Status != "running")
                {
                    _logger.LogInformation("PollWorkerUntilComplete: Agent {AgentId} (Task {TaskId}) is no longer running or deleted. Stopping polling.", agentId, taskId);
                    break;
                }

                try
                {
                    var resp = await client.GetAsync($"/tasks/{taskId}");
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "PollWorkerUntilComplete: Worker returned {StatusCode} for TaskId {TaskId}",
                            resp.StatusCode, taskId
                        );
                        await Task.Delay(2000); // Wait before retrying
                        continue;
                    }

                    var statusObj = await resp.Content.ReadFromJsonAsync<WorkerStatusResponse>();
                    if (statusObj == null)
                    {
                        _logger.LogWarning(
                            "PollWorkerUntilComplete: Empty JSON from worker for TaskId {TaskId}",
                            taskId
                        );
                        await Task.Delay(2000);
                        continue;
                    }

                    if (statusObj.Status == "completed")
                    {
                        _logger.LogInformation(
                            "PollWorkerUntilComplete: Worker completed TaskId {TaskId}, Result: {Result}",
                            taskId, statusObj.Result
                        );

                        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
                        if (agent != null)
                        {
                            // Enhancement: Only update if it was still "running"
                            if(agent.Status == "running")
                            {
                                agent.Status = "stopped";
                                agent.TasksCompleted += 1;
                                agent.LastRun = DateTime.UtcNow; // Update LastRun to completion time
                                await _db.SaveChangesAsync();
                            } else {
                                _logger.LogInformation("PollWorkerUntilComplete: Task {TaskId} completed, but agent {AgentId} was already in status {Status}. Not updating.", taskId, agentId, agent.Status);
                            }
                        }
                        break;
                    }
                    // Enhancement: Handle "failed" or "error" statuses
                    else if (statusObj.Status == "failed" || statusObj.Status == "error")
                    {
                        _logger.LogError("PollWorkerUntilComplete: Worker reported task {TaskId} failed. Result: {Result}", taskId, statusObj.Result);
                        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
                        if (agent != null && agent.Status == "running")
                        {
                            agent.Status = "error_worker"; // Custom status for worker error
                            await _db.SaveChangesAsync();
                        }
                        break;
                    }

                    _logger.LogDebug(
                        "PollWorkerUntilComplete: TaskId {TaskId} is {Status}",
                        taskId, statusObj.Status
                    );
                    await Task.Delay(2000); // Polling interval
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "PollWorkerUntilComplete: Exception while polling TaskId {TaskId}",
                        taskId
                    );
                    // Enhancement: Check if agent still exists
                    var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
                    if (agent == null) {
                        _logger.LogWarning("PollWorkerUntilComplete: Agent {AgentId} (Task {TaskId}) not found during exception handling. Stopping polling.", agentId, taskId);
                        break; // Agent was deleted
                    }
                    // Consider adding a maximum retry count or backoff strategy here
                    await Task.Delay(5000); // Wait longer after an exception
                }
            }
        }
    }
}
