using Microsoft.EntityFrameworkCore;

namespace SwAIvyn.Data
{
    public class SwAIvynDbContext : DbContext
    {
        public SwAIvynDbContext(DbContextOptions<SwAIvynDbContext> options)
            : base(options)
        {
        }

        // “Agents” table
        public DbSet<Agent> Agents { get; set; }
    }
}
