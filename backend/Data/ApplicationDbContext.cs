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
        /// Gets or sets the Folders DbSet.
        /// </summary>
        public DbSet<Folder> Folders { get; set; }

        /// <summary>
        /// Gets or sets the Conversations DbSet.
        /// </summary>
        public DbSet<Conversation> Conversations { get; set; }

        /// <summary>
        /// Gets or sets the ChatHistories DbSet.
        /// </summary>
        public DbSet<ChatHistory> ChatHistories { get; set; }

        /// <summary>
        /// Gets or sets the ChatIndex DbSet.
        /// </summary>
        public DbSet<ChatIndex> ChatIndices { get; set; }

        /// <summary>
        /// Gets or sets the Memories DbSet.
        /// </summary>
        public DbSet<MemoryItem> Memories { get; set; }

        /// <summary>
        /// Gets or sets the Avatars DbSet.
        /// </summary>
        public DbSet<AvatarInfo> Avatars { get; set; }

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

            // AppUser
            modelBuilder.Entity<AppUser>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Folder
            modelBuilder.Entity<Folder>()
                .HasKey(f => f.Id);

            modelBuilder.Entity<Folder>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Folder>()
                .HasOne(f => f.Parent)
                .WithMany(f => f.Children)
                .HasForeignKey(f => f.ParentId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Conversation
            modelBuilder.Entity<Conversation>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User)
                .WithMany(u => u.Conversations)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.Folder)
                .WithMany(f => f.Conversations)
                .HasForeignKey(c => c.FolderId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            modelBuilder.Entity<Conversation>()
                .HasIndex(c => new { c.UserId, c.CreatedUtc });

            // ChatHistory
            modelBuilder.Entity<ChatHistory>()
                .HasKey(ch => ch.Id);

            modelBuilder.Entity<ChatHistory>()
                .HasOne(ch => ch.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(ch => ch.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatHistory>()
                .HasOne(ch => ch.User)
                .WithMany()
                .HasForeignKey(ch => ch.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // ChatIndex
            modelBuilder.Entity<ChatIndex>()
                .HasKey(ci => ci.Id);

            modelBuilder.Entity<ChatIndex>()
                .HasOne(ci => ci.Conversation)
                .WithMany()
                .HasForeignKey(ci => ci.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatIndex>()
                .HasIndex(ci => new { ci.ConversationId, ci.CreatedUtc });

            // MemoryItem
            modelBuilder.Entity<MemoryItem>()
                .HasKey(m => m.Id);

            modelBuilder.Entity<MemoryItem>()
                .HasOne(m => m.User)
                .WithMany(u => u.Memories)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MemoryItem>()
                .HasIndex(m => new { m.UserId, m.Category });

            // Settings
            modelBuilder.Entity<Settings>()
                .HasKey(s => s.Id);

            modelBuilder.Entity<Settings>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            modelBuilder.Entity<Settings>()
                .HasIndex(s => new { s.UserId, s.Key })
                .IsUnique();

            // AvatarInfo
            modelBuilder.Entity<AvatarInfo>()
                .HasKey(a => a.Id);

            modelBuilder.Entity<AvatarInfo>()
                .HasOne(a => a.User)
                .WithMany(u => u.Avatars)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Add SQL trigger for folder cascade delete
            modelBuilder.Entity<Folder>()
                .ToTable(tb => tb.HasTrigger("DeleteFolderCascade"));
        }
    }
}
