using System;
using System.Collections.Generic;

namespace SwAIvyn.Data.Entities
{
    public class AppUser
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string RecoveryPhrase { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLogin { get; set; }
        
        // Navigation properties
        public virtual ICollection<MemoryItem> Memories { get; set; }
        public virtual ICollection<AvatarInfo> Avatars { get; set; }
    }
}
