using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data.Entities;

namespace SwAIvyn.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppUser> Users { get; set; }
        public DbSet<MemoryItem> Memories { get; set; }
        public DbSet<AvatarInfo> Avatars { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entity relationships and constraints here
        }
    }
}
