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

        /// <summary>
        /// Checks if the database can connect
        /// </summary>
        Task<bool> CanConnectAsync();
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

            // Make DataDirectory path more robust
            string configuredDataDir = configuration["AppSettings:DataDirectory"];
            if (string.IsNullOrEmpty(configuredDataDir))
            {
                configuredDataDir = "../data"; // Default if not configured
            }

            if (Path.IsPathRooted(configuredDataDir))
            {
                _dataDirectory = configuredDataDir;
            }
            else
            {
                // Resolve relative paths based on the application's base directory
                _dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredDataDir));
            }
            _logger.LogInfo($"Data directory resolved to: {_dataDirectory}");
        }

        /// <summary>
        /// Initializes the database, creating it if it doesn't exist and enabling WAL mode
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing database...");

                // Ensure data directory exists (using the now absolute path)
                EnsureDirectoryExists(_dataDirectory);
                
                // The connection string will be made absolute in Program.cs,
                // so the database will be created in the correct location.
                // We just need to ensure _dataDirectory itself exists.

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
        /// Checks if the database can connect
        /// </summary>
        public async Task<bool> CanConnectAsync()
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                return await context.Database.CanConnectAsync();
            }
            catch
            {
                return false;
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
