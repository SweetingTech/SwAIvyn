using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a background agent that performs automated tasks for a user.
    /// </summary>
    public class Agent
    {
        /// <summary>
        /// Unique identifier for the agent.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The user to whom this agent belongs.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Display name of the agent.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the agent's purpose.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The agent's type (e.g., "task", "monitoring").
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the agent (e.g., "running", "stopped").
        /// </summary>
        public string Status { get; set; } = "stopped";

        /// <summary>
        /// Timestamp of the last time this agent ran.
        /// </summary>
        public DateTime? LastRun { get; set; }

        /// <summary>
        /// Number of tasks this agent has completed.
        /// </summary>
        public int TasksCompleted { get; set; }

        /// <summary>
        /// Indicates whether this agent is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Navigation property to the owning user.
        /// </summary>
        public virtual AppUser User { get; set; }
    }
}
