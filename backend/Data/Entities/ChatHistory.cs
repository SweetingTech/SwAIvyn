using System;

namespace SwAIvyn.Data.Entities
{
    public class ChatHistory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string ConversationId { get; set; }
        public string Message { get; set; }
        public string Sender { get; set; } // e.g., "User" or "AI"
        public DateTime Timestamp { get; set; }
        
        // Navigation property
        public virtual AppUser User { get; set; }
    }
}
