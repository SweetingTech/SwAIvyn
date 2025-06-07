using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data.Entities;

namespace SwAIvyn.Data
{
    public class MigrationsDbContext : DbContext
    {
        public MigrationsDbContext(DbContextOptions<MigrationsDbContext> options)
            : base(options)
        {
        }

        // Exclude AppUser from this DbContext for migrations
        // public DbSet<AppUser> Users { get; set; }

        public DbSet<Folder> Folders { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatHistory> ChatHistories { get; set; }
        public DbSet<ChatIndex> ChatIndices { get; set; }
        public DbSet<MemoryItem> Memories { get; set; }
        // public DbSet<AvatarInfo> Avatars { get; set; }
        public DbSet<PromptInfo> Prompts { get; set; }
        public DbSet<Settings> Settings { get; set; }
        public DbSet<UploadDocument> UploadDocuments { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<SwAIvyn.Data.Entities.Agent> Agents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Exclude AppUser configuration from this DbContext for migrations
            // modelBuilder.Entity<AppUser>()
            //     .HasKey(u => u.Id);
            // modelBuilder.Entity<AppUser>()
            //     .HasIndex(u => u.Username)
            //     .IsUnique();

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

            // Conversation - No foreign key constraints to avoid database issues
            modelBuilder.Entity<Conversation>()
                .HasKey(c => c.Id);
            // Removed foreign key constraints to match DirectDatabaseService schema
            // modelBuilder.Entity<Conversation>()
            //     .HasOne(c => c.User)
            //     .WithMany(u => u.Conversations)
            //     .HasForeignKey(c => c.UserId)
            //     .OnDelete(DeleteBehavior.Cascade);
            // modelBuilder.Entity<Conversation>()
            //     .HasOne(c => c.Folder)
            //     .WithMany(f => f.Conversations)
            //     .HasForeignKey(c => c.FolderId)
            //     .OnDelete(DeleteBehavior.SetNull)
            //     .IsRequired(false);
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
            // // AvatarInfo
            // modelBuilder.Entity<AvatarInfo>()
            //     .HasKey(a => a.Id);
            // modelBuilder.Entity<AvatarInfo>()
            //     .HasOne(a => a.User)
            //     .WithMany(u => u.Avatars)
            //     .HasForeignKey(a => a.UserId)
            //     .OnDelete(DeleteBehavior.Cascade);

            // PromptInfo
            modelBuilder.Entity<PromptInfo>()
                .HasKey(p => p.Id);
            modelBuilder.Entity<PromptInfo>()
                .HasOne(p => p.Avatar)
                .WithMany()
                .HasForeignKey(p => p.AvatarId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PromptInfo>()
                .HasIndex(p => new { p.AvatarId, p.IsActive });

            // Agent
            modelBuilder.Entity<SwAIvyn.Data.Entities.Agent>()
                .HasKey(a => a.Id);
            modelBuilder.Entity<SwAIvyn.Data.Entities.Agent>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SwAIvyn.Data.Entities.Agent>()
                .HasIndex(a => new { a.UserId, a.Name });

            // Add SQL trigger for folder cascade delete
            modelBuilder.Entity<Folder>()
                .ToTable(tb => tb.HasTrigger("DeleteFolderCascade"));
        }
    }
}
