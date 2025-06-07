using System;

namespace SwAIvyn.Data
{
    public class Agent
    {
        public Guid Id { get; set; }

        // A human‐readable name (e.g., “ResearchBot”)
        public string Name { get; set; } = string.Empty;

        // “stopped”, “running”, or any custom status you use
        public string Status { get; set; } = "stopped";

        // When it was last kicked off
        public DateTime? LastRun { get; set; }

        // How many tasks have been completed so far
        public int TasksCompleted { get; set; } = 0;

        // (Optional) If you store a “goal” or config for the agent, keep it here
        public string? Goal { get; set; }
    }
}
