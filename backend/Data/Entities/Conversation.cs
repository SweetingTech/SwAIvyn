using System;
using System.Collections.Generic;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a conversation session between a user and the AI.
    /// </summary>
    public class Conversation
    {
        /// <summary>
        /// Gets or sets the unique identifier for the conversation.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID associated with the conversation.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the optional folder ID this conversation belongs to.
        /// </summary>
        public Guid? FolderId { get; set; }

        /// <summary>
        /// Gets or sets the title of the conversation.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the date and time the conversation was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the date and time the conversation was last updated.
        /// </summary>
        public DateTime UpdatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the date and time the conversation was last opened.
        /// </summary>
        public DateTime LastOpenUtc { get; set; }

        /// <summary>
        /// Gets or sets a summary of the conversation.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Gets or sets the status of the conversation.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets any tags associated with this conversation.
        /// </summary>
        public string Tags { get; set; }

        /// <summary>
        /// Gets or sets the character ID associated with this conversation (optional).
        /// </summary>
        public Guid? CharacterId { get; set; }

        /// <summary>
        /// Gets or sets the character system prompt for this conversation (optional).
        /// </summary>
        public string CharacterSystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property to the user who owns this conversation.
        /// </summary>
        public virtual AppUser User { get; set; }

        /// <summary>
        /// Navigation property to the folder containing this conversation.
        /// </summary>
        public virtual Folder Folder { get; set; }

        /// <summary>
        /// Navigation property for the messages in this conversation.
        /// </summary>
        public virtual ICollection<ChatHistory> Messages { get; set; }
    }
}
