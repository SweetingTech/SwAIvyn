using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents an AI character profile or avatar information.
    /// </summary>
    public class AvatarInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the avatar.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID this avatar belongs to.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the name of the avatar.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the file path to the avatar image.
        /// </summary>
        public string ImagePath { get; set; }

        /// <summary>
        /// Gets or sets the personality description of the avatar.
        /// </summary>
        public string Personality { get; set; }

        /// <summary>
        /// Gets or sets the voice settings for the avatar.
        /// </summary>
        public string VoiceSettings { get; set; }

        // Extended character card fields
        /// <summary>
        /// Gets or sets the detailed description of the character.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the scenario/setting for the character.
        /// </summary>
        public string Scenario { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the first message/greeting from the character.
        /// </summary>
        public string FirstMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets example messages/conversations.
        /// </summary>
        public string MessageExample { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the system prompt for the character.
        /// </summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets post-history instructions.
        /// </summary>
        public string PostHistoryInstructions { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets alternate greetings (JSON array).
        /// </summary>
        public string AlternateGreetings { get; set; } = "[]";

        /// <summary>
        /// Gets or sets character tags (JSON array).
        /// </summary>
        public string Tags { get; set; } = "[]";

        /// <summary>
        /// Gets or sets the creator/author of the character.
        /// </summary>
        public string Creator { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets creator notes/comments.
        /// </summary>
        public string CreatorNotes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the character version.
        /// </summary>
        public string CharacterVersion { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the talkativeness level (0.0 to 1.0).
        /// </summary>
        public float Talkativeness { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets whether this character is favorited.
        /// </summary>
        public bool IsFavorite { get; set; } = false;

        /// <summary>
        /// Gets or sets additional extensions/metadata (JSON).
        /// </summary>
        public string Extensions { get; set; } = "{}";

        /// <summary>
        /// Gets or sets the full YAML profile for the character.
        /// </summary>
        public string YamlProfile { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last modified timestamp.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Navigation property to the user owning this avatar.
        /// </summary>
        public virtual AppUser User { get; set; }
    }
}
