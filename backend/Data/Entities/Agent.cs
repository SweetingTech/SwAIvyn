using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a background agent that can execute automated tasks.
    /// </summary>
    public class Agent
    {
        /// <summary>
        /// Gets or sets the unique identifier for the agent.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the agent name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of what the agent does.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the agent type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status of the agent.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last time the agent was executed.
        /// </summary>
        public DateTime? LastRun { get; set; }

        /// <summary>
        /// Gets or sets the number of tasks completed by the agent.
        /// </summary>
        public int TasksCompleted { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the agent is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the user ID that owns this agent.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Navigation property to the agent's owner.
        /// </summary>
        public virtual AppUser User { get; set; }
    }
}
