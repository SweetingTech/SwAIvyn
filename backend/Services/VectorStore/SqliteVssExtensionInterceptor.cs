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
        private readonly string _extensionName;
        private readonly ISimpleLoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the SqliteVssExtensionInterceptor
        /// </summary>
        /// <param name="extensionName">The name of the extension to load</param>
        /// <param name="logger">Logger service</param>
        public SqliteVssExtensionInterceptor(
            string extensionName = "sqlite-vss.dll",
            ISimpleLoggerService logger = null)
        {
            _extensionName = extensionName;
            _logger = logger;
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

                    _logger?.LogInfo($"Loading SQLite-VSS extension '{_extensionName}' for connection {connection.GetHashCode()}");
                    sqlite.EnableExtensions(true);
                    sqlite.LoadExtension(_extensionName);
                    _logger?.LogInfo($"Successfully loaded SQLite-VSS extension for connection {connection.GetHashCode()}");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Failed to load SQLite-VSS extension '{_extensionName}': {ex.Message}");
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

                    _logger?.LogInfo($"Loading SQLite-VSS extension '{_extensionName}' for connection {connection.GetHashCode()}");
                    sqlite.EnableExtensions(true);
                    sqlite.LoadExtension(_extensionName);
                    _logger?.LogInfo($"Successfully loaded SQLite-VSS extension for connection {connection.GetHashCode()}");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Failed to load SQLite-VSS extension '{_extensionName}': {ex.Message}");
                    _logger?.LogWarning("Vector search functionality will not be available.");
                    // Don't bubble up the exception, allow the application to continue
                }
            }

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
