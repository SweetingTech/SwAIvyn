using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Service that handles database backups
    /// </summary>
    public class BackupService : BackgroundService
    {
        private readonly ISimpleLoggerService _logger;
        private readonly string _connectionString;
        private readonly string _dataDirectory;
        private readonly string _backupDirectory;
        private readonly TimeSpan _backupInterval;
        private readonly int _maxBackups;

        /// <summary>
        /// Initializes a new instance of the BackupService
        /// </summary>
        /// <param name="logger">Logger service</param>
        /// <param name="configuration">Application configuration</param>
        public BackupService(
            ISimpleLoggerService logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _dataDirectory = configuration["AppSettings:DataDirectory"] ?? "../data";
            _backupDirectory = configuration["AppSettings:BackupsDirectory"] ?? "../data/backups";
            
            // Default to daily backups
            var backupIntervalHours = configuration.GetValue<int>("AppSettings:BackupIntervalHours", 24);
            _backupInterval = TimeSpan.FromHours(backupIntervalHours);
            
            // Default to keeping 7 backups
            _maxBackups = configuration.GetValue<int>("AppSettings:MaxBackups", 7);
        }

        /// <summary>
        /// Executes the backup service
        /// </summary>
        /// <param name="stoppingToken">Cancellation token</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInfo("Backup service started");
                
                // Ensure backup directory exists
                if (!Directory.Exists(_backupDirectory))
                {
                    Directory.CreateDirectory(_backupDirectory);
                    _logger.LogInfo($"Created backup directory: {_backupDirectory}");
                }
                
                // Wait for initial delay to allow application to fully start
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await CreateBackupAsync();
                        await CleanupOldBackupsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error during backup operation", ex);
                    }
                    
                    // Wait for next backup interval
                    await Task.Delay(_backupInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                _logger.LogInfo("Backup service stopping");
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Backup service encountered an error", ex);
            }
        }

        /// <summary>
        /// Creates a backup of the database
        /// </summary>
        private async Task CreateBackupAsync()
        {
            _logger.LogInfo("Starting database backup");
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"swai_{timestamp}.bak";
            var backupPath = Path.Combine(_backupDirectory, backupFileName);
            var tempDbPath = Path.Combine(_backupDirectory, "temp_backup.db");
            
            try
            {
                // Extract database path from connection string
                var dbPath = ExtractDatabasePath(_connectionString);
                if (string.IsNullOrEmpty(dbPath))
                {
                    _logger.LogError("Could not extract database path from connection string");
                    return;
                }
                
                // Create a backup using SQLite's VACUUM INTO command
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Ensure WAL checkpoint is processed
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                        await cmd.ExecuteNonQueryAsync();
                    }
                    
                    // Create backup
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = $"VACUUM INTO '{tempDbPath}';";
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                
                // Create a zip file containing the backup
                using (var zipArchive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    zipArchive.CreateEntryFromFile(tempDbPath, "database.db");
                    
                    // Add a metadata file with backup information
                    var metadataEntry = zipArchive.CreateEntry("metadata.txt");
                    using (var writer = new StreamWriter(metadataEntry.Open()))
                    {
                        await writer.WriteLineAsync($"Backup created: {DateTime.Now}");
                        await writer.WriteLineAsync($"Application version: 1.0.0");
                        await writer.WriteLineAsync($"Database path: {dbPath}");
                    }
                }
                
                // Delete temporary database file
                if (File.Exists(tempDbPath))
                {
                    File.Delete(tempDbPath);
                }
                
                _logger.LogInfo($"Database backup completed successfully: {backupPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create database backup", ex);
                
                // Clean up any temporary files
                if (File.Exists(tempDbPath))
                {
                    File.Delete(tempDbPath);
                }
                
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                
                throw;
            }
        }

        /// <summary>
        /// Cleans up old backup files to maintain the maximum number of backups
        /// </summary>
        private Task CleanupOldBackupsAsync()
        {
            try
            {
                var backupFiles = Directory.GetFiles(_backupDirectory, "swai_*.bak")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();
                
                if (backupFiles.Count > _maxBackups)
                {
                    _logger.LogInfo($"Cleaning up old backups. Current count: {backupFiles.Count}, Max: {_maxBackups}");
                    
                    foreach (var file in backupFiles.Skip(_maxBackups))
                    {
                        _logger.LogInfo($"Deleting old backup: {file.Name}");
                        file.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error cleaning up old backups", ex);
            }
            
            return Task.CompletedTask;
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
