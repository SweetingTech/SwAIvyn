using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwAIvyn.Services.VectorStore
{
    /// <summary>
    /// SQLite-based vector store using the VSS extension
    /// </summary>
    public class SqliteVectorStore : IVectorStore
    {
        private readonly string? _connectionString;
        private readonly ISimpleLoggerService _logger;
        private readonly string _extensionPath;
        private readonly int _dimensions;
        private readonly string _distance;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the SqliteVectorStore
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger service</param>
        public SqliteVectorStore(
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _extensionPath = configuration["AppSettings:VssExtensionPath"] ?? "sqlite-vss";
            _dimensions = configuration.GetValue<int>("AppSettings:VectorDimensions", 768);
            _distance = configuration["AppSettings:VectorDistance"] ?? "cosine";
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing SQLite vector store...");

                // Check if the extension file exists
                string? dataDir = null;
                try
                {
                    var builder = new SqliteConnectionStringBuilder(_connectionString ?? string.Empty);
                    dataDir = Path.GetDirectoryName(builder.DataSource);
                }
                catch
                {
                    dataDir = null;
                }
                string extensionFullPath = Path.Combine(dataDir ?? string.Empty, _extensionPath);
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string extensionAppPath = Path.Combine(appDir, _extensionPath);

                _logger.LogInfo($"Looking for SQLite-VSS extension at: {extensionFullPath} or {extensionAppPath}");

                if (!File.Exists(extensionFullPath) && !File.Exists(extensionAppPath))
                {
                    _logger.LogWarning($"SQLite-VSS extension file not found at {extensionFullPath} or {extensionAppPath}");
                    _logger.LogWarning($"Make sure the file '{_extensionPath}' exists in the data directory or application directory");
                }

                using var connection = new SqliteConnection(_connectionString ?? string.Empty);
                await connection.OpenAsync();

                // Enable extensions explicitly
                connection.EnableExtensions(true);
                _logger.LogInfo("SQLite extensions enabled");

                // The extension loading is now handled by the SqliteVssExtensionInterceptor
                // Just create the vector table if it doesn't exist
                try
                {
                    using var createTableCmd = connection.CreateCommand();
                    createTableCmd.CommandText = $@"
                        CREATE VIRTUAL TABLE IF NOT EXISTS CoreVectors
                        USING vss0(
                            id TEXT PRIMARY KEY,
                            embedding BLOB,
                            metadata TEXT,
                            dims({_dimensions}),
                            distance('{_distance}')
                        );";
                    await createTableCmd.ExecuteNonQueryAsync();

                    _isInitialized = true;
                    _logger.LogInfo("SQLite vector store initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to create vector table: {ex.Message}");

                    if (ex.InnerException != null)
                    {
                        _logger.LogWarning($"Inner exception: {ex.InnerException.Message}");
                    }

                    _logger.LogWarning("Vector search functionality will not be available.");

                    // Set initialized to true but with limited functionality
                    _isInitialized = true;
                    return; // Return without throwing an exception
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to initialize SQLite vector store: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogWarning($"Inner exception: {ex.InnerException.Message}");
                }
                _logger.LogWarning("Vector search functionality will not be available.");

                // Set initialized to true but with limited functionality
                _isInitialized = true;
                return;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> StoreVectorAsync(Guid id, float[] embedding, Dictionary<string, string>? metadata = null, VectorScope scope = VectorScope.Core)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (scope != VectorScope.Core && scope != VectorScope.All)
                return false; // Only store locally for Core or All scope

            try
            {
                using var connection = new SqliteConnection(_connectionString ?? string.Empty);
                await connection.OpenAsync();

                // Check if the VSS extension is available by testing for the CoreVectors table
                try
                {
                    using var testCmd = connection.CreateCommand();
                    testCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='CoreVectors';";
                    var tableName = await testCmd.ExecuteScalarAsync();

                    if (tableName == null)
                    {
                        _logger.LogWarning("CoreVectors table does not exist. Vector storage is not available.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to check for CoreVectors table: {ex.Message}");
                    return false;
                }

                // Convert embedding to blob
                var blob = new byte[embedding.Length * sizeof(float)];
                Buffer.BlockCopy(embedding, 0, blob, 0, blob.Length);

                // Convert metadata to JSON
                var metadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null;

                // Insert or replace the vector
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO CoreVectors(id, embedding, metadata)
                    VALUES (@Id, @Embedding, @Metadata);";
                cmd.Parameters.AddWithValue("@Id", id.ToString());
                cmd.Parameters.AddWithValue("@Embedding", blob);
                cmd.Parameters.AddWithValue("@Metadata", metadataJson ?? (object)DBNull.Value);

                var result = await cmd.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to store vector for ID {id}: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<SearchHit>> SearchAsync(float[] queryVector, int limit = 10, VectorScope scope = VectorScope.All)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (scope != VectorScope.Core && scope != VectorScope.All)
                return new List<SearchHit>(); // Only search locally for Core or All scope

            try
            {
                using var connection = new SqliteConnection(_connectionString ?? string.Empty);
                await connection.OpenAsync();

                // Check if the VSS extension is available by testing for the vss_search function
                try
                {
                    using var testCmd = connection.CreateCommand();
                    testCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='CoreVectors';";
                    var tableName = await testCmd.ExecuteScalarAsync();

                    if (tableName == null)
                    {
                        _logger.LogWarning("CoreVectors table does not exist. Vector search is not available.");
                        return new List<SearchHit>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to check for CoreVectors table: {ex.Message}");
                    return new List<SearchHit>();
                }

                // Convert query vector to blob
                var blob = new byte[queryVector.Length * sizeof(float)];
                Buffer.BlockCopy(queryVector, 0, blob, 0, blob.Length);

                // Search for similar vectors
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, metadata, vss_distance(CoreVectors) AS score
                    FROM CoreVectors
                    WHERE vss_search(CoreVectors, @QueryVector, @Limit);";
                cmd.Parameters.AddWithValue("@QueryVector", blob);
                cmd.Parameters.AddWithValue("@Limit", limit);

                var results = new List<SearchHit>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var hit = new SearchHit
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        Score = reader.GetFloat(2)
                    };

                    // Parse metadata if available
                    if (!reader.IsDBNull(1))
                    {
                        var metadataJson = reader.GetString(1);
                        hit.Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson);
                    }

                    results.Add(hit);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to search vectors: {ex.Message}");
                return new List<SearchHit>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteVectorAsync(Guid id, VectorScope scope = VectorScope.Core)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (scope != VectorScope.Core && scope != VectorScope.All)
                return false; // Only delete locally for Core or All scope

            try
            {
                using var connection = new SqliteConnection(_connectionString ?? string.Empty);
                await connection.OpenAsync();

                // Delete the vector
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM CoreVectors WHERE id = @Id;";
                cmd.Parameters.AddWithValue("@Id", id.ToString());

                var result = await cmd.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete vector for ID {id}: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>> GetStatusAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();

            try
            {
                using var connection = new SqliteConnection(_connectionString ?? string.Empty);
                await connection.OpenAsync();

                var status = new Dictionary<string, object>();

                // Get vector count
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM CoreVectors;";
                    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    status["VectorCount"] = count;
                }

                // Get database size
                var dbPath = new SqliteConnectionStringBuilder(_connectionString ?? string.Empty).DataSource;
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    status["DatabaseSize"] = fileInfo.Length;
                    status["DatabasePath"] = dbPath;
                }

                // Get extension info
                status["Dimensions"] = _dimensions;
                status["Distance"] = _distance;
                status["ExtensionPath"] = _extensionPath;

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get vector store status: {ex.Message}");
                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                // Try a simple query to check DB connection
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString ?? string.Empty);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
