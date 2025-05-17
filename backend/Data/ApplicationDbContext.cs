using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data.Entities;

namespace SwAIvyn.Data
{
    /// <summary>
    /// The Entity Framework Core database context for the application.
    /// Manages entity sets and configures relationships.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the ApplicationDbContext class.
        /// </summary>
        /// <param name="options">The options to be used by the DbContext.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the Users DbSet.
        /// </summary>
        public DbSet<AppUser> Users { get; set; }

        /// <summary>
        /// Gets or sets the Memories DbSet.
        /// </summary>
        public DbSet<MemoryItem> Memories { get; set; }

        /// <summary>
        /// Gets or sets the Avatars DbSet.
        /// </summary>
        public DbSet<AvatarInfo> Avatars { get; set; }

        /// <summary>
        /// Gets or sets the ChatHistories DbSet.
        /// </summary>
        public DbSet<ChatHistory> ChatHistories { get; set; }

        /// <summary>
        /// Gets or sets the Settings DbSet.
        /// </summary>
        public DbSet<Settings> Settings { get; set; }

        /// <summary>
        /// Configures the model relationships and constraints.
        /// </summary>
        /// <param name="modelBuilder">The model builder to configure entities.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entity relationships and constraints here
        }
    }
}
