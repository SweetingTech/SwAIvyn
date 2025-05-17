using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a memory item stored by the AI for a user.
    /// </summary>
    public class MemoryItem
    {
        /// <summary>
        /// Gets or sets the unique identifier for the memory item.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID associated with the memory.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the content of the memory.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the category of the memory.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the memory is shared.
        /// </summary>
        public bool IsShared { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last accessed timestamp.
        /// </summary>
        public DateTime LastAccessed { get; set; }
        
        /// <summary>
        /// Navigation property to the user owning this memory.
        /// </summary>
        public virtual AppUser User { get; set; }
    }
}
