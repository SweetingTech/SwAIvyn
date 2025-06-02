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
                        SortOrder INTEGER NOT NULL DEFAULT 0,
                        CONSTRAINT PK_Folders PRIMARY KEY (Id)
                    );", null, null
                );

                // Create Conversations table
                await CreateTableIfNotExists(connection, "Conversations", @"
                    CREATE TABLE Conversations (
                        Id TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        FolderId TEXT,
                        CharacterId TEXT,
                        CharacterSystemPrompt TEXT,
                        Title TEXT NOT NULL,
                        Summary TEXT,
                        Status TEXT NOT NULL,
                        CreatedUtc TEXT NOT NULL,
                        UpdatedUtc TEXT NOT NULL,
                        LastOpenUtc TEXT NOT NULL,
                        Tags TEXT,
                        CONSTRAINT PK_Conversations PRIMARY KEY (Id)
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

                // Ensure Conversations table has required columns (critical fix)
                await EnsureConversationsTableSchema(connection);

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
        /// Ensures the Conversations table has all required columns.
        /// This is a critical fix for the CharacterId column issue.
        /// </summary>
        /// <param name="connection">The SQLite connection.</param>
        private async Task EnsureConversationsTableSchema(SqliteConnection connection)
        {
            _logger.LogInfo("🔧 CRITICAL FIX: Checking and fixing Conversations table schema...");
            try
            {
                var checkConversationsColumnsSql = "PRAGMA table_info(Conversations);";
                using var checkConversationsColumnsCommand = new SqliteCommand(checkConversationsColumnsSql, connection);
                using var conversationsReader = await checkConversationsColumnsCommand.ExecuteReaderAsync();

                var hasCharacterId = false;
                var hasCharacterSystemPrompt = false;
                var hasCreatedUtc = false;
                var hasLastOpenUtc = false;
                var hasUpdatedUtc = false;

                while (await conversationsReader.ReadAsync())
                {
                    var columnName = conversationsReader.GetString(1); // Column name is at index 1
                    if (columnName == "CharacterId") hasCharacterId = true;
                    if (columnName == "CharacterSystemPrompt") hasCharacterSystemPrompt = true;
                    if (columnName == "CreatedUtc") hasCreatedUtc = true;
                    if (columnName == "LastOpenUtc") hasLastOpenUtc = true;
                    if (columnName == "UpdatedUtc") hasUpdatedUtc = true;
                }
                conversationsReader.Close();

                if (!hasCharacterId)
                {
                    _logger.LogInfo("🔧 Adding CharacterId column to Conversations table...");
                    var addCharacterIdSql = "ALTER TABLE Conversations ADD COLUMN CharacterId TEXT;";
                    using var addCharacterIdCommand = new SqliteCommand(addCharacterIdSql, connection);
                    await addCharacterIdCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("✅ CharacterId column added to Conversations table.");
                }
                else
                {
                    _logger.LogInfo("✅ Conversations table already has CharacterId column.");
                }

                if (!hasCharacterSystemPrompt)
                {
                    _logger.LogInfo("🔧 Adding CharacterSystemPrompt column to Conversations table...");
                    var addCharacterSystemPromptSql = "ALTER TABLE Conversations ADD COLUMN CharacterSystemPrompt TEXT;";
                    using var addCharacterSystemPromptCommand = new SqliteCommand(addCharacterSystemPromptSql, connection);
                    await addCharacterSystemPromptCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("✅ CharacterSystemPrompt column added to Conversations table.");
                }
                else
                {
                    _logger.LogInfo("✅ Conversations table already has CharacterSystemPrompt column.");
                }

                if (!hasCreatedUtc)
                {
                    _logger.LogInfo("🔧 Adding CreatedUtc column to Conversations table...");
                    var addCreatedUtcSql = "ALTER TABLE Conversations ADD COLUMN CreatedUtc TEXT;";
                    using var addCreatedUtcCommand = new SqliteCommand(addCreatedUtcSql, connection);
                    await addCreatedUtcCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("✅ CreatedUtc column added to Conversations table.");
                }

                if (!hasLastOpenUtc)
                {
                    _logger.LogInfo("🔧 Adding LastOpenUtc column to Conversations table...");
                    var addLastOpenUtcSql = "ALTER TABLE Conversations ADD COLUMN LastOpenUtc TEXT;";
                    using var addLastOpenUtcCommand = new SqliteCommand(addLastOpenUtcSql, connection);
                    await addLastOpenUtcCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("✅ LastOpenUtc column added to Conversations table.");
                }

                if (!hasUpdatedUtc)
                {
                    _logger.LogInfo("🔧 Adding UpdatedUtc column to Conversations table...");
                    var addUpdatedUtcSql = "ALTER TABLE Conversations ADD COLUMN UpdatedUtc TEXT;";
                    using var addUpdatedUtcCommand = new SqliteCommand(addUpdatedUtcSql, connection);
                    await addUpdatedUtcCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("✅ UpdatedUtc column added to Conversations table.");
                }

                _logger.LogInfo("✅ Conversations table schema check completed successfully.");

                // Also check and fix ChatIndices table schema
                _logger.LogInfo("🔧 CRITICAL FIX: Checking and fixing ChatIndices table schema...");
                var checkChatIndicesColumnsSql = "PRAGMA table_info(ChatIndices);";
                using var checkChatIndicesColumnsCommand = new SqliteCommand(checkChatIndicesColumnsSql, connection);
                using var chatIndicesReader = await checkChatIndicesColumnsCommand.ExecuteReaderAsync();

                var hasMessageId = false;
                var hasMetadata = false;

                while (await chatIndicesReader.ReadAsync())
                {
                    var columnName = chatIndicesReader.GetString(1); // Column name is at index 1
                    if (columnName == "MessageId") hasMessageId = true;
                    if (columnName == "Metadata") hasMetadata = true;
                }
                chatIndicesReader.Close();

                if (!hasMessageId)
                {
                    _logger.LogInfo("🔧 Adding MessageId column to ChatIndices table...");
                    var addMessageIdSql = "ALTER TABLE ChatIndices ADD COLUMN MessageId TEXT;";
                    using var addMessageIdCommand = new SqliteCommand(addMessageIdSql, connection);
                    await addMessageIdCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("✅ MessageId column added to ChatIndices table.");
                }
                else
                {
                    _logger.LogInfo("✅ ChatIndices table already has MessageId column.");
                }

                if (!hasMetadata)
                {
                    _logger.LogInfo("🔧 Adding Metadata column to ChatIndices table...");
                    var addMetadataSql = "ALTER TABLE ChatIndices ADD COLUMN Metadata TEXT;";
                    using var addMetadataCommand = new SqliteCommand(addMetadataSql, connection);
                    await addMetadataCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("✅ Metadata column added to ChatIndices table.");
                }
                else
                {
                    _logger.LogInfo("✅ ChatIndices table already has Metadata column.");
                }

                _logger.LogInfo("✅ ChatIndices table schema check completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error ensuring Conversations table schema: {ex.Message}");
                throw;
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

                // Check if Memories table exists (Entity Framework expects this name, not MemoryItems)
                var checkMemoriesTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='Memories';";
                using var checkMemoriesCommand = new SqliteCommand(checkMemoriesTableSql, connection);
                var memoriesTableExists = await checkMemoriesCommand.ExecuteScalarAsync() != null;

                // Drop and recreate the Memories table to fix foreign key issues
                if (memoriesTableExists)
                {
                    _logger.LogInfo("Dropping existing Memories table to fix foreign key constraint issues...");
                    var dropMemoriesTableSql = "DROP TABLE Memories;";
                    using var dropMemoriesCommand = new SqliteCommand(dropMemoriesTableSql, connection);
                    await dropMemoriesCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("Existing Memories table dropped.");
                }

                // Create the Memories table (always create it now)
                _logger.LogInfo("Creating Memories table for Entity Framework...");

                // First, let's check what users exist in the database for debugging
                var checkUsersSql = "SELECT Id, Username FROM Users LIMIT 5;";
                using var checkUsersCommand = new SqliteCommand(checkUsersSql, connection);
                using var usersReader = await checkUsersCommand.ExecuteReaderAsync();
                _logger.LogInfo("Existing users in database:");
                while (await usersReader.ReadAsync())
                {
                    var userId = usersReader.GetString(0);
                    var username = usersReader.GetString(1);
                    _logger.LogInfo($"  User ID: {userId}, Username: {username}");
                }
                usersReader.Close();

                // Create the Memories table without foreign key constraint initially to avoid issues
                var createMemoriesTableSql = @"
                    CREATE TABLE Memories (
                        Id TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        Content TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        LastAccessed TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        IsShared INTEGER NOT NULL,
                        TargetStore INTEGER NOT NULL DEFAULT 0,
                        CONSTRAINT PK_Memories PRIMARY KEY (Id)
                    );";

                using var createMemoriesCommand = new SqliteCommand(createMemoriesTableSql, connection);
                await createMemoriesCommand.ExecuteNonQueryAsync();

                var createMemoriesIndexSql = "CREATE INDEX IX_Memories_UserId_Category ON Memories (UserId, Category);";
                using var createMemoriesIndexCommand = new SqliteCommand(createMemoriesIndexSql, connection);
                await createMemoriesIndexCommand.ExecuteNonQueryAsync();

                _logger.LogInfo("Memories table created successfully (without FK constraint to avoid issues).");

                // Force fix Settings table foreign key issues
                _logger.LogInfo("Dropping existing Settings table to fix foreign key constraint issues...");
                var dropSettingsTableSql = "DROP TABLE IF EXISTS Settings;";
                using var dropSettingsCommand = new SqliteCommand(dropSettingsTableSql, connection);
                await dropSettingsCommand.ExecuteNonQueryAsync();
                _logger.LogInfo("Existing Settings table dropped.");

                // Create Settings table without foreign key constraint
                _logger.LogInfo("Creating Settings table for Entity Framework...");
                var createSettingsTableSql = @"
                    CREATE TABLE Settings (
                        Id TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        Key TEXT NOT NULL,
                        Value TEXT,
                        LastModified TEXT NOT NULL,
                        CONSTRAINT PK_Settings PRIMARY KEY (Id)
                    );";

                using var createSettingsCommand = new SqliteCommand(createSettingsTableSql, connection);
                await createSettingsCommand.ExecuteNonQueryAsync();

                var createSettingsIndexSql = "CREATE INDEX IX_Settings_UserId_Key ON Settings (UserId, Key);";
                using var createSettingsIndexCommand = new SqliteCommand(createSettingsIndexSql, connection);
                await createSettingsIndexCommand.ExecuteNonQueryAsync();

                _logger.LogInfo("Settings table created successfully (without FK constraint to avoid issues).");

                // Force fix Avatars table foreign key issues
                _logger.LogInfo("Dropping existing Avatars table to fix foreign key constraint issues...");
                var dropAvatarsTableSql = "DROP TABLE IF EXISTS Avatars;";
                using var dropAvatarsCommand = new SqliteCommand(dropAvatarsTableSql, connection);
                await dropAvatarsCommand.ExecuteNonQueryAsync();
                _logger.LogInfo("Existing Avatars table dropped.");

                // Create Avatars table without foreign key constraint
                _logger.LogInfo("Creating Avatars table for Entity Framework...");
                var createAvatarsTableSql = @"
                    CREATE TABLE Avatars (
                        Id TEXT NOT NULL,
                        UserId TEXT NOT NULL,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        Personality TEXT,
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
                        ImagePath TEXT,
                        VoiceSettings TEXT,
                        YamlProfile TEXT,
                        CreatedAt TEXT NOT NULL,
                        LastModified TEXT NOT NULL,
                        CONSTRAINT PK_Avatars PRIMARY KEY (Id)
                    );";

                using var createAvatarsCommand = new SqliteCommand(createAvatarsTableSql, connection);
                await createAvatarsCommand.ExecuteNonQueryAsync();

                var createAvatarsIndexSql = "CREATE INDEX IX_Avatars_UserId ON Avatars (UserId);";
                using var createAvatarsIndexCommand = new SqliteCommand(createAvatarsIndexSql, connection);
                await createAvatarsIndexCommand.ExecuteNonQueryAsync();

                _logger.LogInfo("Avatars table created successfully (without FK constraint to avoid issues).");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying schema migrations: {ex.Message}");
                throw;
            }

            // Always check and fix missing columns regardless of table creation
            try
            {
                // Fix Conversations table missing columns
                _logger.LogInfo("Checking and fixing Conversations table schema...");
                var checkConversationsColumnsSql = "PRAGMA table_info(Conversations);";
                using var checkConversationsColumnsCommand = new SqliteCommand(checkConversationsColumnsSql, connection);
                using var conversationsReader = await checkConversationsColumnsCommand.ExecuteReaderAsync();

                var hasCharacterId = false;
                var hasCharacterSystemPrompt = false;
                var hasCreatedUtc = false;
                var hasLastOpenUtc = false;
                var hasUpdatedUtc = false;

                while (await conversationsReader.ReadAsync())
                {
                    var columnName = conversationsReader.GetString(1); // Column name is at index 1
                    if (columnName == "CharacterId") hasCharacterId = true;
                    if (columnName == "CharacterSystemPrompt") hasCharacterSystemPrompt = true;
                    if (columnName == "CreatedUtc") hasCreatedUtc = true;
                    if (columnName == "LastOpenUtc") hasLastOpenUtc = true;
                    if (columnName == "UpdatedUtc") hasUpdatedUtc = true;
                }
                conversationsReader.Close();

                if (!hasCharacterId)
                {
                    _logger.LogInfo("Adding CharacterId column to Conversations table...");
                    var addCharacterIdSql = "ALTER TABLE Conversations ADD COLUMN CharacterId TEXT;";
                    using var addCharacterIdCommand = new SqliteCommand(addCharacterIdSql, connection);
                    await addCharacterIdCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("CharacterId column added to Conversations table.");
                }

                if (!hasCharacterSystemPrompt)
                {
                    _logger.LogInfo("Adding CharacterSystemPrompt column to Conversations table...");
                    var addCharacterSystemPromptSql = "ALTER TABLE Conversations ADD COLUMN CharacterSystemPrompt TEXT;";
                    using var addCharacterSystemPromptCommand = new SqliteCommand(addCharacterSystemPromptSql, connection);
                    await addCharacterSystemPromptCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("CharacterSystemPrompt column added to Conversations table.");
                }

                if (!hasCreatedUtc)
                {
                    _logger.LogInfo("Adding CreatedUtc column to Conversations table...");
                    var addCreatedUtcSql = "ALTER TABLE Conversations ADD COLUMN CreatedUtc TEXT;";
                    using var addCreatedUtcCommand = new SqliteCommand(addCreatedUtcSql, connection);
                    await addCreatedUtcCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("CreatedUtc column added to Conversations table.");
                }

                if (!hasLastOpenUtc)
                {
                    _logger.LogInfo("Adding LastOpenUtc column to Conversations table...");
                    var addLastOpenUtcSql = "ALTER TABLE Conversations ADD COLUMN LastOpenUtc TEXT;";
                    using var addLastOpenUtcCommand = new SqliteCommand(addLastOpenUtcSql, connection);
                    await addLastOpenUtcCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("LastOpenUtc column added to Conversations table.");
                }

                if (!hasUpdatedUtc)
                {
                    _logger.LogInfo("Adding UpdatedUtc column to Conversations table...");
                    var addUpdatedUtcSql = "ALTER TABLE Conversations ADD COLUMN UpdatedUtc TEXT;";
                    using var addUpdatedUtcCommand = new SqliteCommand(addUpdatedUtcSql, connection);
                    await addUpdatedUtcCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("UpdatedUtc column added to Conversations table.");
                }

                // Fix Folders table missing columns
                _logger.LogInfo("Checking and fixing Folders table schema...");
                var checkFoldersColumnsSql = "PRAGMA table_info(Folders);";
                using var checkFoldersColumnsCommand = new SqliteCommand(checkFoldersColumnsSql, connection);
                using var foldersReader = await checkFoldersColumnsCommand.ExecuteReaderAsync();

                var foldersHasCreatedUtc = false;
                var foldersHasUpdatedUtc = false;
                var foldersHasSortOrder = false;

                while (await foldersReader.ReadAsync())
                {
                    var columnName = foldersReader.GetString(1); // Column name is at index 1
                    if (columnName == "CreatedUtc") foldersHasCreatedUtc = true;
                    if (columnName == "UpdatedUtc") foldersHasUpdatedUtc = true;
                    if (columnName == "SortOrder") foldersHasSortOrder = true;
                }
                foldersReader.Close();

                if (!foldersHasCreatedUtc)
                {
                    _logger.LogInfo("Adding CreatedUtc column to Folders table...");
                    var addFoldersCreatedUtcSql = "ALTER TABLE Folders ADD COLUMN CreatedUtc TEXT;";
                    using var addFoldersCreatedUtcCommand = new SqliteCommand(addFoldersCreatedUtcSql, connection);
                    await addFoldersCreatedUtcCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("CreatedUtc column added to Folders table.");
                }

                if (!foldersHasUpdatedUtc)
                {
                    _logger.LogInfo("Adding UpdatedUtc column to Folders table...");
                    var addFoldersUpdatedUtcSql = "ALTER TABLE Folders ADD COLUMN UpdatedUtc TEXT;";
                    using var addFoldersUpdatedUtcCommand = new SqliteCommand(addFoldersUpdatedUtcSql, connection);
                    await addFoldersUpdatedUtcCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("UpdatedUtc column added to Folders table.");
                }

                if (!foldersHasSortOrder)
                {
                    _logger.LogInfo("Adding SortOrder column to Folders table...");
                    var addFoldersSortOrderSql = "ALTER TABLE Folders ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0;";
                    using var addFoldersSortOrderCommand = new SqliteCommand(addFoldersSortOrderSql, connection);
                    await addFoldersSortOrderCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("SortOrder column added to Folders table.");
                }

                _logger.LogInfo("Database schema fixes completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fixing database schema: {ex.Message}");
                throw;
            }
        }
    }
}
