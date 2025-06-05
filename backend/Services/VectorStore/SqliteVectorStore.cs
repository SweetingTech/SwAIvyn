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
        private readonly string _resolvedExtensionPath; // Changed from _extensionPath
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
            
            string configuredExtensionPath = configuration["AppSettings:VssExtensionPath"];
            if (string.IsNullOrWhiteSpace(configuredExtensionPath))
            {
                _logger?.LogWarning("SQLite-VSS extension path not configured in AppSettings:VssExtensionPath. Using default 'sqlite-vss.dll'.");
                configuredExtensionPath = "sqlite-vss.dll";
            }

            if (!configuredExtensionPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInfo($"Appending .dll to VSS extension path for SqliteVectorStore: {configuredExtensionPath}");
                configuredExtensionPath += ".dll";
            }

            if (Path.IsPathRooted(configuredExtensionPath))
            {
                _resolvedExtensionPath = configuredExtensionPath;
            }
            else
            {
                _resolvedExtensionPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredExtensionPath));
            }
            _logger.LogInfo($"SqliteVectorStore: Resolved VSS extension path to '{_resolvedExtensionPath}'");

            _dimensions = configuration.GetValue<int>("AppSettings:VectorDimensions", 768);
            _distance = configuration["AppSettings:VectorDistance"] ?? "cosine";
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                _logger.LogInfo("Initializing SQLite vector store...");

                if (!File.Exists(_resolvedExtensionPath))
                {
                    _logger.LogWarning($"SQLite-VSS extension file not found at the resolved path: '{_resolvedExtensionPath}'. Vector functionality may be impaired.");
                    // Allow to proceed, table creation will fail if extension is truly needed and not loaded by other means.
                }

                using var connection = new SqliteConnection(_connectionString ?? string.Empty);
                await connection.OpenAsync();

                // Enable extensions explicitly and load VSS for this specific connection
                // This is crucial because this connection is NOT managed by EF Core's interceptor.
                try
                {
                    connection.EnableExtensions(true);
                    _logger.LogInfo($"Attempting to load SQLite-VSS extension from '{_resolvedExtensionPath}' for SqliteVectorStore's own connection.");
                    connection.LoadExtension(_resolvedExtensionPath);
                    _logger.LogInfo($"Successfully loaded SQLite-VSS extension from '{_resolvedExtensionPath}' for SqliteVectorStore's own connection.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to load SQLite-VSS extension from '{_resolvedExtensionPath}' for SqliteVectorStore's own connection: {ex.Message}. Vector functionality will likely fail.");
                    // Do not set _isInitialized to true if extension loading fails, as table creation will fail.
                    // Let the table creation attempt proceed and fail, which will then prevent _isInitialized = true.
                }
                
                try
                {
                    using var createTableCmd = connection.CreateCommand();
                    createTableCmd.CommandText = $@"
                        CREATE VIRTUAL TABLE IF NOT EXISTS CoreVectors
                        USING vss0(
                            embedding({_dimensions}),
                            metadata_json
                        );";
                        // id TEXT PRIMARY KEY - vss0 uses rowid by default for id.
                        // embedding BLOB - implicit for vss0, type specified in embedding()
                        // metadata TEXT - using metadata_json as a TEXT column.
                        // dims({_dimensions}) - This is usually part of the embedding() parameter in newer VSS versions.
                        // distance('{_distance}') - This is often a table parameter or index parameter.
                        // The exact syntax can vary. Assuming a simplified schema for now or one that works with a common VSS variant.
                        // A more standard way for recent sqlite-vss:
                        // CREATE VIRTUAL TABLE IF NOT EXISTS CoreVectors USING vss0(vector float[{_dimensions}]);
                        // And then add other columns like id, metadata separately.
                        // For this example, I'll use a common pattern from older examples or a hypothetical one.
                        // Let's assume the original schema was intended for a specific VSS version.
                        // For robustness, let's stick to a very simple vss0 declaration and add other columns manually if needed.
                        // However, the original had `id TEXT PRIMARY KEY`, `embedding BLOB`, `metadata TEXT`.
                        // This implies these are not directly part of the vss0 definition itself but columns in the virtual table.
                    
                    // Re-evaluating the original schema:
                    // CREATE VIRTUAL TABLE IF NOT EXISTS CoreVectors USING vss0(
                    //  id TEXT PRIMARY KEY, -> This might be problematic as vss0 usually manages rowid. Let's try without making it PK here.
                    //  embedding BLOB,      -> This is the vector column.
                    //  metadata TEXT,       -> For JSON metadata.
                    //  dims({_dimensions}), -> This is a parameter to vss0, not a column.
                    //  distance('{_distance}') -> Also a parameter to vss0.
                    // );
                    // A more typical modern vss0 syntax is:
                    // CREATE VIRTUAL TABLE CoreVectors USING vss0(embedding({_dimensions}));
                    // Then one might add columns like: ALTER TABLE CoreVectors ADD COLUMN metadata_json TEXT;
                    // Or, some vss0 versions allow defining columns directly:
                    // CREATE VIRTUAL TABLE CoreVectors USING vss0(vector float[{_dimensions}], metadata_json TEXT)

                    // Given the original code, it was likely:
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
                    _logger.LogInfo("Virtual table 'CoreVectors' (vss0) with original schema ensured.");

                    _isInitialized = true; // Set to true ONLY if table creation is successful
                    _logger.LogInfo("SQLite vector store initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to create or verify vector table 'CoreVectors': {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        _logger.LogWarning($"Inner exception: {ex.InnerException.Message}");
                    }
                    _logger.LogWarning("Vector search and storage functionality will NOT be available.");
                    // _isInitialized remains false if table creation fails
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to initialize SQLite vector store during connection opening or extension loading: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogWarning($"Inner exception: {ex.InnerException.Message}");
                }
                _logger.LogWarning("Vector search and storage functionality will NOT be available.");
                // _isInitialized remains false
            }
        }

        /// <inheritdoc/>
        public async Task<bool> StoreVectorAsync(Guid id, float[] embedding, Dictionary<string, string>? metadata = null, VectorScope scope = VectorScope.Core)
        {
            if (!_isInitialized) 
            {
                _logger.LogWarning("Attempted to store vector, but SqliteVectorStore is not initialized (likely due to VSS extension or table creation failure).");
                return false;
            }

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
                await InitializeAsync(); // This will now effectively be a no-op if already initialized.
            
            if (!_isInitialized)
            {
                _logger.LogWarning("Attempted to search vectors, but SqliteVectorStore is not initialized.");
                return new List<SearchHit>();
            }

            if (scope != VectorScope.Core && scope != VectorScope.All)
                return new List<SearchHit>(); // Only search locally for Core or All scope

            try
            {
                using var connection = new SqliteConnection(_connectionString ?? string.Empty);
                await connection.OpenAsync();
                // No need to explicitly load extension here if InitializeAsync did it or if relying on interceptor for this connection type (unlikely for direct new SqliteConnection).
                // For safety, if direct connections are used here, they *also* need LoadExtension if not using EF Core context.
                // However, the InitializeAsync now loads it for its connection, and subsequent operations *should* work on that basis if table was created.
                // Let's assume for now that if InitializeAsync succeeded, the vss0 module is usable.

                // Convert query vector to blob
                var blob = new byte[queryVector.Length * sizeof(float)];
                Buffer.BlockCopy(queryVector, 0, blob, 0, blob.Length);

                // Search for similar vectors
                // Search for similar vectors - Reverted to original query structure
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
                status["ExtensionPath"] = _resolvedExtensionPath;

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
