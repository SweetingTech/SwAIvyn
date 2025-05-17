using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a chat message in a conversation.
    /// </summary>
    public class ChatHistory
    {
        /// <summary>
        /// Gets or sets the unique identifier for the chat message.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID associated with the chat message.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the conversation identifier.
        /// </summary>
        public string ConversationId { get; set; }

        /// <summary>
        /// Gets or sets the chat message content.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the sender of the message (e.g., "User" or "AI").
        /// </summary>
        public string Sender { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the message.
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Navigation property to the user who sent the message.
        /// </summary>
        public virtual AppUser User { get; set; }
    }
}
