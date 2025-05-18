using System;
using System.Collections.Generic;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a user of the SwAIvyn system.
    /// </summary>
    public class AppUser
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the username for login.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the hashed password.
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// Gets or sets the PIN code for alternative login.
        /// </summary>
        public string PINCode { get; set; }

        /// <summary>
        /// Gets or sets the recovery phrase for account recovery.
        /// </summary>
        public string RecoveryPhrase { get; set; }

        /// <summary>
        /// Gets or sets the date and time the user was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the last login.
        /// </summary>
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// Navigation property for the user's memories.
        /// </summary>
        public virtual ICollection<MemoryItem> Memories { get; set; }

        /// <summary>
        /// Navigation property for the user's avatars.
        /// </summary>
        public virtual ICollection<AvatarInfo> Avatars { get; set; }

        /// <summary>
        /// Navigation property for the user's conversations.
        /// </summary>
        public virtual ICollection<Conversation> Conversations { get; set; }
    }
}
