using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Service that initializes all required directories for the application
    /// </summary>
    public class DirectoryInitializerService
    {
        private readonly ISimpleLoggerService _logger;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the DirectoryInitializerService
        /// </summary>
        /// <param name="logger">Logger service</param>
        /// <param name="configuration">Application configuration</param>
        public DirectoryInitializerService(
            ISimpleLoggerService logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Initializes all required directories for the application
        /// </summary>
        public void InitializeDirectories()
        {
            try
            {
                _logger.LogInfo("Initializing application directories...");

                // Create data directory
                var dataDir = _configuration["AppSettings:DataDirectory"] ?? "../data";
                EnsureDirectoryExists(dataDir);

                // Create avatars directory
                var avatarsDir = _configuration["AppSettings:AvatarsDirectory"] ?? "../data/avatars";
                EnsureDirectoryExists(avatarsDir);

                // Create uploads directory
                var uploadsDir = _configuration["AppSettings:UploadsDirectory"] ?? "../data/uploads";
                EnsureDirectoryExists(uploadsDir);

                // Create backups directory
                var backupsDir = _configuration["AppSettings:BackupsDirectory"] ?? "../data/backups";
                EnsureDirectoryExists(backupsDir);

                // Create modules directory
                var modulesDir = _configuration["AppSettings:ModulesDirectory"] ?? "../data/modules";
                EnsureDirectoryExists(modulesDir);

                // Create sessions directory
                var sessionsDir = _configuration["AppSettings:SessionsDirectory"] ?? "../data/sessions";
                EnsureDirectoryExists(sessionsDir);

                // Create logs directory
                var logsDir = _configuration["AppSettings:LogDirectory"] ?? "../logs";
                EnsureDirectoryExists(logsDir);

                _logger.LogInfo("Application directories initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Failed to initialize application directories", ex);
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
    }
}
