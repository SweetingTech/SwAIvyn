using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SwAIvyn.Services.VectorStore
{
    /// <summary>
    /// Intercepts database connection open events to load the SQLite-VSS extension
    /// </summary>
    public class SqliteVssExtensionInterceptor : DbConnectionInterceptor
    {
        private readonly string _resolvedExtensionPath;
        private readonly ISimpleLoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the SqliteVssExtensionInterceptor
        /// </summary>
        /// <param name="configuredExtensionPath">The configured path (filename or relative/absolute path) for the extension from AppSettings.</param>
        /// <param name="logger">Logger service</param>
        public SqliteVssExtensionInterceptor(
            string configuredExtensionPath, // No default, must be provided from Program.cs
            ISimpleLoggerService logger = null)
        {
            _logger = logger;
            string extensionPath = configuredExtensionPath;

            if (string.IsNullOrWhiteSpace(extensionPath))
            {
                _logger?.LogWarning("SQLite-VSS extension path is not configured. Using default 'sqlite-vss.dll'.");
                extensionPath = "sqlite-vss.dll";
            }

            if (!extensionPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInfo($"Appending .dll to VSS extension path: {extensionPath}");
                extensionPath += ".dll";
            }

            if (Path.IsPathRooted(extensionPath))
            {
                _resolvedExtensionPath = extensionPath;
            }
            else
            {
                _resolvedExtensionPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, extensionPath));
            }
            _logger?.LogInfo($"SQLite-VSS Interceptor: Resolved extension path to '{_resolvedExtensionPath}'");
        }

        /// <summary>
        /// Called when a connection is opened
        /// </summary>
        public override void ConnectionOpened(
            DbConnection connection,
            ConnectionEndEventData eventData)
        {
            if (connection is SqliteConnection sqlite)
            {
                try
                {
                    // Get SQLite version
                    using var cmd = sqlite.CreateCommand();
                    cmd.CommandText = "SELECT sqlite_version()";
                    var version = cmd.ExecuteScalar();
                    _logger?.LogInfo($"SQLite Version: {version}");

                    _logger?.LogInfo($"Attempting to load SQLite-VSS extension from '{_resolvedExtensionPath}' for connection {connection.GetHashCode()}");
                    sqlite.EnableExtensions(true);
                    sqlite.LoadExtension(_resolvedExtensionPath);
                    _logger?.LogInfo($"Successfully loaded SQLite-VSS extension from '{_resolvedExtensionPath}' for connection {connection.GetHashCode()}");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Failed to load SQLite-VSS extension from '{_resolvedExtensionPath}': {ex.Message}");
                    _logger?.LogWarning("Vector search functionality will not be available.");
                    // Don't bubble up the exception, allow the application to continue
                }
            }

            base.ConnectionOpened(connection, eventData);
        }

        /// <summary>
        /// Called when a connection is opened asynchronously
        /// </summary>
        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (connection is SqliteConnection sqlite)
            {
                try
                {
                    // Get SQLite version
                    using var cmd = sqlite.CreateCommand();
                    cmd.CommandText = "SELECT sqlite_version()";
                    var version = await cmd.ExecuteScalarAsync(cancellationToken);
                    _logger?.LogInfo($"SQLite Version: {version}");

                    _logger?.LogInfo($"Attempting to load SQLite-VSS extension from '{_resolvedExtensionPath}' for connection {connection.GetHashCode()}");
                    sqlite.EnableExtensions(true);
                    sqlite.LoadExtension(_resolvedExtensionPath);
                    _logger?.LogInfo($"Successfully loaded SQLite-VSS extension from '{_resolvedExtensionPath}' for connection {connection.GetHashCode()}");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Failed to load SQLite-VSS extension from '{_resolvedExtensionPath}': {ex.Message}");
                    _logger?.LogWarning("Vector search functionality will not be available.");
                    // Don't bubble up the exception, allow the application to continue
                }
            }

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
