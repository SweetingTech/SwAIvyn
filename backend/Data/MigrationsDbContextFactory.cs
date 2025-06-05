using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace SwAIvyn.Data
{
    public class MigrationsDbContextFactory : IDesignTimeDbContextFactory<MigrationsDbContext>
    {
        public MigrationsDbContext CreateDbContext(string[] args)
        {
            // Build configuration
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            // Configure DbContextOptions
            var builder = new DbContextOptionsBuilder<MigrationsDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Resolve data directory for SQLite connection string
            var appSettingsDataDir = configuration["AppSettings:DataDirectory"] ?? "../data";
            string resolvedDataDirectory;

            if (Path.IsPathRooted(appSettingsDataDir))
            {
                resolvedDataDirectory = appSettingsDataDir;
            }
            else
            {
                // For design-time, the base directory is usually the project directory
                resolvedDataDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), appSettingsDataDir));
            }

            var csBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(csBuilder.DataSource) && !Path.IsPathRooted(csBuilder.DataSource))
            {
                string dbFileName = Path.GetFileName(csBuilder.DataSource);
                csBuilder.DataSource = Path.GetFullPath(Path.Combine(resolvedDataDirectory, dbFileName));
            }
            connectionString = csBuilder.ToString();

            builder.UseSqlite(connectionString);

            return new MigrationsDbContext(builder.Options);
        }
    }
}
