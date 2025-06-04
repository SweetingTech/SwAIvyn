using System;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a user or system setting.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// Gets or sets the unique identifier for the setting.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID associated with the setting.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the key/name of the setting.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value of the setting.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the last modified timestamp.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Navigation property to the user owning this setting.
        /// </summary>
        public virtual AppUser User { get; set; }
    }
}
