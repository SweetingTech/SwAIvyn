using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents an AI character profile or avatar information.
    /// </summary>
    public class AvatarInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the avatar.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID this avatar belongs to.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the name of the avatar.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the file path to the avatar image.
        /// </summary>
        public string ImagePath { get; set; }

        /// <summary>
        /// Gets or sets the personality description of the avatar.
        /// </summary>
        public string Personality { get; set; }

        /// <summary>
        /// Gets or sets the voice settings for the avatar.
        /// </summary>
        public string VoiceSettings { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last modified timestamp.
        /// </summary>
        public DateTime LastModified { get; set; }
        
        /// <summary>
        /// Navigation property to the user owning this avatar.
        /// </summary>
        public virtual AppUser User { get; set; }
    }
}
