using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a processed prompt derived from an avatar for LLM consumption.
    /// This table stores optimized, ready-to-use prompts that are built from avatar data.
    /// </summary>
    public class PromptInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the prompt.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the avatar ID this prompt is derived from.
        /// </summary>
        public Guid AvatarId { get; set; }

        /// <summary>
        /// Gets or sets the name/title of this prompt variant.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of prompt (e.g., "chat", "roleplay", "instruction").
        /// </summary>
        public string PromptType { get; set; } = "chat";

        /// <summary>
        /// Gets or sets the optimized system prompt for the LLM.
        /// This is the main prompt content ready for LLM consumption.
        /// </summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the initial/greeting message for conversations.
        /// </summary>
        public string InitialMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets example conversation snippets for context.
        /// </summary>
        public string ExampleConversation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional context or instructions.
        /// </summary>
        public string AdditionalContext { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the temperature setting for this prompt (0.0 to 2.0).
        /// </summary>
        public float Temperature { get; set; } = 0.7f;

        /// <summary>
        /// Gets or sets the maximum tokens for responses.
        /// </summary>
        public int MaxTokens { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether this is the active/default prompt for the avatar.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the version of this prompt.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets optimization metadata (JSON).
        /// </summary>
        public string Metadata { get; set; } = "{}";

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last modified timestamp.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Navigation property to the avatar this prompt is derived from.
        /// </summary>
        public virtual AvatarInfo Avatar { get; set; }
    }
}
