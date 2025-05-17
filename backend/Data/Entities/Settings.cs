using System;

namespace SwAIvyn.Data.Entities
{
    public class Settings
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime LastModified { get; set; }
        
        // Navigation property
        public virtual AppUser User { get; set; }
    }
}
