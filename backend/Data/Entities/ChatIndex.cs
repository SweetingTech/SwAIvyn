using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents an indexed entry for chat messages for vector search capabilities.
    /// </summary>
    public class ChatIndex
    {
        /// <summary>
        /// Gets or sets the unique identifier for the chat index entry.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the conversation ID this index belongs to.
        /// </summary>
        public Guid ConversationId { get; set; }

        /// <summary>
        /// Gets or sets the original message ID this index refers to.
        /// </summary>
        public Guid MessageId { get; set; }

        /// <summary>
        /// Gets or sets the textual content being indexed.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the embedding vector as a serialized string.
        /// </summary>
        public string Embedding { get; set; }

        /// <summary>
        /// Gets or sets the date and time this index was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the type of content (e.g., "user", "assistant").
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the role of the message sender (e.g., "user", "assistant", "system").
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Gets or sets the optional file path if this index refers to a file content.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets any metadata associated with this index.
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// Navigation property to the conversation this index belongs to.
        /// </summary>
        public virtual Conversation Conversation { get; set; }
    }
}
