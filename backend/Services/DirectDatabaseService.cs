using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Direct database service for creating all necessary tables and default user.
    /// This service is responsible for ensuring the core database schema exists on startup,
    /// bypassing Entity Framework Core migrations for initial setup.
    /// </summary>
    public interface IDirectDatabaseService
    {
        Task InitializeDatabaseSchemaAndDefaultUserAsync();
    }

    public class DirectDatabaseService : IDirectDatabaseService
    {
        private readonly string _connectionString;
        private readonly ISimpleLoggerService _logger;        public DirectDatabaseService(IConfiguration configuration, ISimpleLoggerService logger)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var appSettingsDataDir = configuration["AppSettings:DataDirectory"] ?? "../data";
            var csBuilder = new SqliteConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(csBuilder.DataSource) && !Path.IsPathRooted(csBuilder.DataSource))
            {
                // The connection string is relative, resolve it based on the application's base directory
                // This ensures portability and consistency with the "data" directory relative to the executable.
                string appBaseDirectory = AppContext.BaseDirectory;
                string dbFileName = Path.GetFileName(csBuilder.DataSource);
                string fullPathToDb = Path.Combine(appBaseDirectory, appSettingsDataDir, dbFileName);
                csBuilder.DataSource = fullPathToDb;
            }
            _connectionString = csBuilder.ToString();
            _logger = logger;
            _logger.LogInfo($"DirectDatabaseService using resolved connection string: {_connectionString}");
        }

        public async Task InitializeDatabaseSchemaAndDefaultUserAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // Enable WAL mode for better concurrency
                using (var walCommand = new SqliteCommand("PRAGMA journal_mode=WAL;", connection))
                {
                    await walCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("Enabled WAL mode for SQLite.");
                }
                using (var syncCommand = new SqliteCommand("PRAGMA synchronous=NORMAL;", connection))
                {
                    await syncCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("Set synchronous mode to NORMAL for SQLite.");
                }

                // Create Users table
                await CreateTableIfNotExists(connection, "Users", @"
                    CREATE TABLE Users (
                        Id TEXT NOT NULL,
                        Username TEXT NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        PINCode TEXT NOT NULL,
                        RecoveryPhrase TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        LastLogin TEXT NOT NULL,
                        LastSelectedCharacterId TEXT,
                        CONSTRAINT PK_Users PRIMARY KEY (Id)
                    );", "IX_Users_Username", "CREATE UNIQUE INDEX IX_Users_Username ON Users (Username);"
                );

                // Create Avatars table
                await CreateTableIfNotExists(connection, "Avatars", @"
                    CREATE TABLE Avatars (
                        Id TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        Name TEXT NOT NULL,
                        ImagePath TEXT,
                        Personality TEXT,
                        VoiceSettings TEXT,
                        Description TEXT,
                        Scenario TEXT,
                        FirstMessage TEXT,
                        MessageExample TEXT,
                        SystemPrompt TEXT,
                        PostHistoryInstructions TEXT,
                        AlternateGreetings TEXT,
                        Tags TEXT,
                        Creator TEXT,
                        CreatorNotes TEXT,
                        CharacterVersion TEXT,
                        Talkativeness REAL NOT NULL,
                        IsFavorite INTEGER NOT NULL,
                        Extensions TEXT,
                        YamlProfile TEXT,
                        CreatedAt TEXT NOT NULL,
                        LastModified TEXT NOT NULL,
                        CONSTRAINT PK_Avatars PRIMARY KEY (Id),
                        CONSTRAINT FK_Avatars_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                    );", null, null // No unique index for Avatars by default
                );

                // Create Settings table
                await CreateTableIfNotExists(connection, "Settings", @"
                    CREATE TABLE Settings (
                        Id TEXT NOT NULL,
                        UserId TEXT,
                        Key TEXT NOT NULL,
                        Value TEXT,
                        LastModified TEXT NOT NULL,
                        CONSTRAINT PK_Settings PRIMARY KEY (Id),
                        CONSTRAINT FK_Settings_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                    );", "IX_Settings_UserId_Key", "CREATE UNIQUE INDEX IX_Settings_UserId_Key ON Settings (UserId, Key) WHERE UserId IS NOT NULL;"
                );

                // Create Folders table
                await CreateTableIfNotExists(connection, "Folders", @"
                    CREATE TABLE Folders (
                        Id TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        ParentId TEXT,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        Icon TEXT,
                        Color TEXT,
                        CreatedAt TEXT NOT NULL,
                        LastModified TEXT NOT NULL,
                        CONSTRAINT PK_Folders PRIMARY KEY (Id),
                        CONSTRAINT FK_Folders_Folders_ParentId FOREIGN KEY (ParentId) REFERENCES Folders (Id) ON DELETE RESTRICT,
                        CONSTRAINT FK_Folders_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                    );", null, null
                );

                // Create Conversations table
                await CreateTableIfNotExists(connection, "Conversations", @"
                    CREATE TABLE Conversations (
                        Id TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        FolderId TEXT,
                        Title TEXT NOT NULL,
                        Summary TEXT,
                        Status TEXT NOT NULL,
                        CreatedUtc TEXT NOT NULL,
                        UpdatedUtc TEXT NOT NULL,
                        LastOpenUtc TEXT NOT NULL,
                        Tags TEXT,
                        CONSTRAINT PK_Conversations PRIMARY KEY (Id),
                        CONSTRAINT FK_Conversations_Folders_FolderId FOREIGN KEY (FolderId) REFERENCES Folders (Id) ON DELETE SET NULL,
                        CONSTRAINT FK_Conversations_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                    );", "IX_Conversations_UserId_CreatedUtc", "CREATE INDEX IX_Conversations_UserId_CreatedUtc ON Conversations (UserId, CreatedUtc);"
                );

                // Create ChatHistories table
                await CreateTableIfNotExists(connection, "ChatHistories", @"
                    CREATE TABLE ChatHistories (
                        Id TEXT NOT NULL,
                        ConversationId TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        Message TEXT NOT NULL,
                        Sender TEXT NOT NULL,
                        Timestamp TEXT NOT NULL,
                        CONSTRAINT PK_ChatHistories PRIMARY KEY (Id),
                        CONSTRAINT FK_ChatHistories_Conversations_ConversationId FOREIGN KEY (ConversationId) REFERENCES Conversations (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_ChatHistories_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE NO ACTION
                    );", null, null
                );

                // Create ChatIndices table
                await CreateTableIfNotExists(connection, "ChatIndices", @"
                    CREATE TABLE ChatIndices (
                        Id TEXT NOT NULL,
                        ConversationId TEXT NOT NULL,
                        Content TEXT,
                        Embedding TEXT,
                        ContentType TEXT,
                        Role TEXT,
                        FilePath TEXT,
                        CreatedUtc TEXT NOT NULL,
                        CONSTRAINT PK_ChatIndices PRIMARY KEY (Id),
                        CONSTRAINT FK_ChatIndices_Conversations_ConversationId FOREIGN KEY (ConversationId) REFERENCES Conversations (Id) ON DELETE CASCADE
                    );", "IX_ChatIndices_ConversationId_CreatedUtc", "CREATE INDEX IX_ChatIndices_ConversationId_CreatedUtc ON ChatIndices (ConversationId, CreatedUtc);"
                );

                // Create MemoryItems table
                await CreateTableIfNotExists(connection, "MemoryItems", @"
                    CREATE TABLE MemoryItems (
                        Id TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        Content TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        LastAccessed TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        IsShared INTEGER NOT NULL,
                        TargetStore TEXT,
                        CONSTRAINT PK_MemoryItems PRIMARY KEY (Id),
                        CONSTRAINT FK_MemoryItems_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                    );", "IX_MemoryItems_UserId_Category", "CREATE INDEX IX_MemoryItems_UserId_Category ON MemoryItems (UserId, Category);"
                );

                // Create PromptInfo table
                await CreateTableIfNotExists(connection, "PromptInfo", @"
                    CREATE TABLE PromptInfo (
                        Id TEXT NOT NULL,
                        AvatarId TEXT NOT NULL,
                        Prompt TEXT,
                        IsActive INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        LastModified TEXT NOT NULL,
                        CONSTRAINT PK_PromptInfo PRIMARY KEY (Id),
                        CONSTRAINT FK_PromptInfo_Avatars_AvatarId FOREIGN KEY (AvatarId) REFERENCES Avatars (Id) ON DELETE CASCADE
                    );", "IX_PromptInfo_AvatarId_IsActive", "CREATE INDEX IX_PromptInfo_AvatarId_IsActive ON PromptInfo (AvatarId, IsActive);"
                );

                // Apply schema migrations for existing tables
                await ApplySchemaMigrations(connection);

                // Ensure default user exists
                await EnsureDefaultUserExists(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in InitializeDatabaseSchemaAndDefaultUserAsync: {ex.Message}");
                throw;
            }
        }

        private async Task CreateTableIfNotExists(SqliteConnection connection, string tableName, string createTableSql, string indexName, string createIndexSql)
        {
            var checkTableSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}';";
            using var checkCommand = new SqliteCommand(checkTableSql, connection);
            var tableExists = await checkCommand.ExecuteScalarAsync();

            if (tableExists == null)
            {
                _logger.LogInfo($"{tableName} table does not exist. Creating {tableName} table...");
                using var createTableCommand = new SqliteCommand(createTableSql, connection);
                await createTableCommand.ExecuteNonQueryAsync();
                _logger.LogInfo($"{tableName} table created successfully.");

                if (indexName != null && createIndexSql != null)
                {
                    _logger.LogInfo($"Creating index {indexName} on {tableName}...");
                    using var createIndexCommand = new SqliteCommand(createIndexSql, connection);
                    await createIndexCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo($"Index {indexName} created successfully.");
                }
            }
            else
            {
                _logger.LogInfo($"{tableName} table already exists.");
            }
        }

        private async Task EnsureDefaultUserExists(SqliteConnection connection)
        {
            var checkUsersSql = "SELECT COUNT(*) FROM Users;";
            using var checkUsersCommand = new SqliteCommand(checkUsersSql, connection);
            var userCount = Convert.ToInt32(await checkUsersCommand.ExecuteScalarAsync());

            if (userCount == 0)
            {
                _logger.LogInfo("No users found. Creating default user named 'user'...");

                var insertUserSql = @"
                    INSERT INTO Users (Id, Username, PasswordHash, PINCode, RecoveryPhrase, CreatedAt, LastLogin)
                    VALUES (@Id, @Username, @PasswordHash, @PINCode, @RecoveryPhrase, @CreatedAt, @LastLogin);";

                using var insertCommand = new SqliteCommand(insertUserSql, connection);
                var userId = Guid.NewGuid().ToString();
                var currentTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                insertCommand.Parameters.AddWithValue("@Id", userId);
                insertCommand.Parameters.AddWithValue("@Username", "user");
                insertCommand.Parameters.AddWithValue("@PasswordHash", "");
                insertCommand.Parameters.AddWithValue("@PINCode", "");
                insertCommand.Parameters.AddWithValue("@RecoveryPhrase", "");
                insertCommand.Parameters.AddWithValue("@CreatedAt", currentTime);
                insertCommand.Parameters.AddWithValue("@LastLogin", currentTime);

                await insertCommand.ExecuteNonQueryAsync();
                _logger.LogInfo($"Default user 'user' created successfully with ID: {userId}");
            }
            else
            {
                _logger.LogInfo($"Found {userCount} existing user(s). No need to create default user.");
            }
        }

        /// <summary>
        /// Applies schema migrations to existing tables to ensure they have all required columns.
        /// </summary>
        /// <param name="connection">The SQLite connection.</param>
        private async Task ApplySchemaMigrations(SqliteConnection connection)
        {
            _logger.LogInfo("Starting schema migrations...");
            try
            {
                // Check if Users table has LastSelectedCharacterId column
                var checkColumnSql = "PRAGMA table_info(Users);";
                using var checkCommand = new SqliteCommand(checkColumnSql, connection);
                using var reader = await checkCommand.ExecuteReaderAsync();

                bool hasLastSelectedCharacterIdColumn = false;
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString(1); // Column name is at index 1 in PRAGMA table_info
                    if (columnName == "LastSelectedCharacterId")
                    {
                        hasLastSelectedCharacterIdColumn = true;
                        break;
                    }
                }
                reader.Close();

                // Add LastSelectedCharacterId column if it doesn't exist
                if (!hasLastSelectedCharacterIdColumn)
                {
                    _logger.LogInfo("Adding LastSelectedCharacterId column to Users table...");
                    var addColumnSql = "ALTER TABLE Users ADD COLUMN LastSelectedCharacterId TEXT;";
                    using var addColumnCommand = new SqliteCommand(addColumnSql, connection);
                    await addColumnCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("LastSelectedCharacterId column added successfully.");
                }
                else
                {
                    _logger.LogInfo("Users table already has LastSelectedCharacterId column.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying schema migrations: {ex.Message}");
                throw;
            }
        }
    }
}
