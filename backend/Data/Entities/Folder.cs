using System;
using System.Collections.Generic;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a folder that can contain conversations, for organizational purposes.
    /// </summary>
    public class Folder
    {
        /// <summary>
        /// Gets or sets the unique identifier for the folder.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID associated with the folder.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the optional parent folder ID for nested folders.
        /// </summary>
        public Guid? ParentId { get; set; }

        /// <summary>
        /// Gets or sets the name of the folder.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the folder.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the date and time the folder was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the date and time the folder was last updated.
        /// </summary>
        public DateTime UpdatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the icon for the folder.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Gets or sets the color for the folder.
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets the order index for display.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Navigation property to the user who owns this folder.
        /// </summary>
        public virtual AppUser User { get; set; }

        /// <summary>
        /// Navigation property to the parent folder.
        /// </summary>
        public virtual Folder Parent { get; set; }

        /// <summary>
        /// Navigation property for child folders contained within this folder.
        /// </summary>
        public virtual ICollection<Folder> Children { get; set; }

        /// <summary>
        /// Navigation property for conversations contained within this folder.
        /// </summary>
        public virtual ICollection<Conversation> Conversations { get; set; }
    }
}
