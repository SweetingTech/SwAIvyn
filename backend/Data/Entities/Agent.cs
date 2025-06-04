using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a background agent that can perform automated tasks for a user.
    /// </summary>
    public class Agent
    {
        /// <summary>
        /// Gets or sets the unique identifier for the agent.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user this agent belongs to.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the agent.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a description of the agent's purpose.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the agent type (e.g., task, monitoring).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status of the agent.
        /// </summary>
        public string Status { get; set; } = "stopped";

        /// <summary>
        /// Gets or sets the timestamp of the last run.
        /// </summary>
        public DateTime? LastRun { get; set; }

        /// <summary>
        /// Gets or sets the number of tasks this agent has completed.
        /// </summary>
        public int TasksCompleted { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this agent is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Navigation property to the owning user.
        /// </summary>
        public virtual AppUser User { get; set; }
    }
}
