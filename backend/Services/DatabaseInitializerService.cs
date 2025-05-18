using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for database initialization service
    /// </summary>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// Initializes the database, creating it if it doesn't exist and enabling WAL mode
        /// </summary>
        Task InitializeAsync();
    }

    /// <summary>
    /// Service that handles database initialization, including creating the database,
    /// enabling WAL mode, and ensuring directories exist
    /// </summary>
    public class DatabaseInitializerService : IDatabaseInitializer
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly ISimpleLoggerService _logger;
        private readonly string _connectionString;
        private readonly string _dataDirectory;

        /// <summary>
        /// Initializes a new instance of the DatabaseInitializerService
        /// </summary>
        /// <param name="dbContextFactory">Factory for creating database contexts</param>
        /// <param name="logger">Logger service</param>
        /// <param name="configuration">Application configuration</param>
        public DatabaseInitializerService(
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            ISimpleLoggerService logger,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _dataDirectory = configuration["AppSettings:DataDirectory"] ?? "../data";
        }

        /// <summary>
        /// Initializes the database, creating it if it doesn't exist and enabling WAL mode
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing database...");

                // Ensure data directory exists
                EnsureDirectoryExists(_dataDirectory);
                
                // Extract database path from connection string
                var dbPath = ExtractDatabasePath(_connectionString);
                if (!string.IsNullOrEmpty(dbPath))
                {
                    var dbDirectory = Path.GetDirectoryName(dbPath);
                    if (!string.IsNullOrEmpty(dbDirectory))
                    {
                        EnsureDirectoryExists(dbDirectory);
                    }
                }

                // Create database if it doesn't exist
                using (var context = await _dbContextFactory.CreateDbContextAsync())
                {
                    _logger.LogInfo("Ensuring database is created...");
                    await context.Database.EnsureCreatedAsync();
                    
                    // Enable WAL mode for better concurrency
                    _logger.LogInfo("Enabling WAL mode...");
                    await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                    await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
                    
                    _logger.LogInfo("Database initialization completed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Failed to initialize database", ex);
                throw;
            }
        }

        /// <summary>
        /// Ensures that the specified directory exists, creating it if necessary
        /// </summary>
        /// <param name="directory">Directory path to check/create</param>
        private void EnsureDirectoryExists(string directory)
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogInfo($"Creating directory: {directory}");
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Extracts the database file path from a SQLite connection string
        /// </summary>
        /// <param name="connectionString">Connection string to parse</param>
        /// <returns>Database file path or null if not found</returns>
        private string ExtractDatabasePath(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return null;

            var builder = new SqliteConnectionStringBuilder(connectionString);
            return builder.DataSource;
        }
    }
}
