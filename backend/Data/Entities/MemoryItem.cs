using System;

namespace SwAIvyn.Data.Entities
{
    public class MemoryItem
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
        public string Category { get; set; }
        public bool IsShared { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        
        // Navigation property
        public virtual AppUser User { get; set; }
    }
}
