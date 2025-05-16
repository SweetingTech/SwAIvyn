using System;

namespace SwAIvyn.Data.Entities
{
    public class AvatarInfo
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public string Personality { get; set; }
        public string VoiceSettings { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        
        // Navigation property
        public virtual AppUser User { get; set; }
    }
}
