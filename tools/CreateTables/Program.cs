using System;
using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Find the database path dynamically
        string dbPath = FindDatabasePath();

        if (string.IsNullOrEmpty(dbPath))
        {
            Console.WriteLine("❌ Database not found. Please ensure the database exists or will be created.");
            // Try to create the database directory and file if it doesn't exist
            dbPath = CreateDatabasePath();
        }

        Console.WriteLine($"📍 Using database: {dbPath}");

        Console.WriteLine("🔧 Creating Users, Avatars and Prompts tables...");

        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            Console.WriteLine("✅ Connected to database");

            // Create Users table
            var createUsersTable = @"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Users"" PRIMARY KEY,
                ""Username"" TEXT NOT NULL,
                ""PasswordHash"" TEXT NOT NULL,
                ""PINCode"" TEXT NOT NULL,
                ""RecoveryPhrase"" TEXT NOT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""LastLogin"" TEXT NOT NULL,
                ""LastSelectedCharacterId"" TEXT NULL
            );";

            // Create Avatars table
            var createAvatarsTable = @"
            CREATE TABLE IF NOT EXISTS ""Avatars"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Avatars"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""Name"" TEXT NOT NULL,
                ""ImagePath"" TEXT NOT NULL,
                ""Personality"" TEXT NOT NULL,
                ""VoiceSettings"" TEXT NOT NULL,
                ""Description"" TEXT NOT NULL,
                ""Scenario"" TEXT NOT NULL,
                ""FirstMessage"" TEXT NOT NULL,
                ""MessageExample"" TEXT NOT NULL,
                ""SystemPrompt"" TEXT NOT NULL,
                ""PostHistoryInstructions"" TEXT NOT NULL,
                ""AlternateGreetings"" TEXT NOT NULL,
                ""Tags"" TEXT NOT NULL,
                ""Creator"" TEXT NOT NULL,
                ""CreatorNotes"" TEXT NOT NULL,
                ""CharacterVersion"" TEXT NOT NULL,
                ""Talkativeness"" REAL NOT NULL,
                ""IsFavorite"" INTEGER NOT NULL,
                ""Extensions"" TEXT NOT NULL,
                ""YamlProfile"" TEXT NOT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""LastModified"" TEXT NOT NULL
            );";

            // Create Prompts table
            var createPromptsTable = @"
            CREATE TABLE IF NOT EXISTS ""Prompts"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Prompts"" PRIMARY KEY,
                ""AvatarId"" TEXT NOT NULL,
                ""Name"" TEXT NOT NULL,
                ""PromptType"" TEXT NOT NULL,
                ""SystemPrompt"" TEXT NOT NULL,
                ""InitialMessage"" TEXT NOT NULL,
                ""ExampleConversation"" TEXT NOT NULL,
                ""AdditionalContext"" TEXT NOT NULL,
                ""Temperature"" REAL NOT NULL,
                ""MaxTokens"" INTEGER NOT NULL,
                ""IsActive"" INTEGER NOT NULL,
                ""Version"" TEXT NOT NULL,
                ""Metadata"" TEXT NOT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""LastModified"" TEXT NOT NULL
            );";

            // Create indexes
            var createIndexes = @"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Username"" ON ""Users"" (""Username"");
            CREATE INDEX IF NOT EXISTS ""IX_Avatars_UserId"" ON ""Avatars"" (""UserId"");
            CREATE INDEX IF NOT EXISTS ""IX_Prompts_AvatarId_IsActive"" ON ""Prompts"" (""AvatarId"", ""IsActive"");";

            using var command = connection.CreateCommand();

            Console.WriteLine("📋 Creating Users table...");
            command.CommandText = createUsersTable;
            await command.ExecuteNonQueryAsync();

            Console.WriteLine("📋 Creating Avatars table...");
            command.CommandText = createAvatarsTable;
            await command.ExecuteNonQueryAsync();

            Console.WriteLine("📋 Creating Prompts table...");
            command.CommandText = createPromptsTable;
            await command.ExecuteNonQueryAsync();

            Console.WriteLine("📋 Creating indexes...");
            command.CommandText = createIndexes;
            await command.ExecuteNonQueryAsync();

            // Create default user if none exists
            Console.WriteLine("👤 Checking for default user...");
            command.CommandText = "SELECT COUNT(*) FROM Users;";
            var userCount = Convert.ToInt32(await command.ExecuteScalarAsync());

            if (userCount == 0)
            {
                Console.WriteLine("Creating default user...");
                var insertUserSql = @"
                INSERT INTO Users (Id, Username, PasswordHash, PINCode, RecoveryPhrase, CreatedAt, LastLogin, LastSelectedCharacterId)
                VALUES (@Id, @Username, @PasswordHash, @PINCode, @RecoveryPhrase, @CreatedAt, @LastLogin, @LastSelectedCharacterId);";

                command.CommandText = insertUserSql;
                command.Parameters.Clear();

                var userId = "00000000-0000-0000-0000-000000000001";
                var currentTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                command.Parameters.AddWithValue("@Id", userId);
                command.Parameters.AddWithValue("@Username", "user");
                command.Parameters.AddWithValue("@PasswordHash", "");
                command.Parameters.AddWithValue("@PINCode", "");
                command.Parameters.AddWithValue("@RecoveryPhrase", "");
                command.Parameters.AddWithValue("@CreatedAt", currentTime);
                command.Parameters.AddWithValue("@LastLogin", currentTime);
                command.Parameters.AddWithValue("@LastSelectedCharacterId", DBNull.Value);

                await command.ExecuteNonQueryAsync();
                Console.WriteLine($"✅ Default user created with ID: {userId}");
            }
            else
            {
                Console.WriteLine($"✅ Found {userCount} existing user(s)");
            }

            // Verify tables exist
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND (name='Users' OR name='Avatars' OR name='Prompts');";
            command.Parameters.Clear();
            using var reader = await command.ExecuteReaderAsync();

            Console.WriteLine("✅ Tables created successfully:");
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"   - {reader["name"]}");
            }
            reader.Close();

            // Check current counts
            command.CommandText = "SELECT COUNT(*) FROM Users;";
            var finalUserCount = await command.ExecuteScalarAsync();
            Console.WriteLine($"📊 Users count: {finalUserCount}");

            command.CommandText = "SELECT COUNT(*) FROM Avatars;";
            var avatarCount = await command.ExecuteScalarAsync();
            Console.WriteLine($"📊 Avatars count: {avatarCount}");

            // Load character cards from frontend/AI directory
            Console.WriteLine("🎭 Loading character cards from filesystem...");
            await LoadCharacterCardsAsync(connection);

            Console.WriteLine("🎉 Database setup completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static string FindDatabasePath()
    {
        // Try multiple possible paths relative to the tool's location
        string[] possiblePaths = {
            "../../Sqldatabase/swai-vyn.db",           // From tools/CreateTables to root/Sqldatabase
            "../../../Sqldatabase/swai-vyn.db",        // Alternative depth
            "../../backend/Sqldatabase/swai-vyn.db",   // If in backend folder
            "../Sqldatabase/swai-vyn.db",              // Direct parent
            "Sqldatabase/swai-vyn.db"                  // Current directory
        };

        foreach (string path in possiblePaths)
        {
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // If not found, try to find it by searching up the directory tree
        string currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            string dbPath = Path.Combine(currentDir, "Sqldatabase", "swai-vyn.db");
            if (File.Exists(dbPath))
            {
                return dbPath;
            }

            DirectoryInfo parent = Directory.GetParent(currentDir);
            currentDir = parent?.FullName;
        }

        return null;
    }

    static string CreateDatabasePath()
    {
        // Try to create the database in the most likely location
        string currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            // Look for a directory that suggests we're in the project root
            if (Directory.Exists(Path.Combine(currentDir, "backend")) ||
                Directory.Exists(Path.Combine(currentDir, "frontend")) ||
                Directory.Exists(Path.Combine(currentDir, "tools")))
            {
                string sqlDatabaseDir = Path.Combine(currentDir, "Sqldatabase");
                Directory.CreateDirectory(sqlDatabaseDir);
                return Path.Combine(sqlDatabaseDir, "swai-vyn.db");
            }

            DirectoryInfo parent = Directory.GetParent(currentDir);
            currentDir = parent?.FullName;
        }

        // Fallback: create in relative path
        string fallbackDir = Path.GetFullPath("../../Sqldatabase");
        Directory.CreateDirectory(fallbackDir);
        return Path.Combine(fallbackDir, "swai-vyn.db");
    }

    static async Task LoadCharacterCardsAsync(SqliteConnection connection)
    {
        try
        {
            // Find the frontend/AI directory
            string aiDirectory = FindAIDirectory();

            if (string.IsNullOrEmpty(aiDirectory) || !Directory.Exists(aiDirectory))
            {
                Console.WriteLine("⚠️  frontend/AI directory not found, skipping character loading");
                return;
            }

            Console.WriteLine($"📁 Found AI directory: {aiDirectory}");

            var characterDirectories = Directory.GetDirectories(aiDirectory);
            Console.WriteLine($"📋 Found {characterDirectories.Length} character directories");

            foreach (var characterDir in characterDirectories)
            {
                await LoadCharacterFromDirectoryAsync(connection, characterDir, aiDirectory);
            }

            Console.WriteLine($"✅ Character loading completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading characters: {ex.Message}");
        }
    }

    static string FindAIDirectory()
    {
        // Try multiple possible paths
        string[] possiblePaths = {
            "../../frontend/AI",
            "../../../frontend/AI",
            "../frontend/AI",
            "frontend/AI"
        };

        foreach (string path in possiblePaths)
        {
            string fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Search up the directory tree
        string currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            string aiPath = Path.Combine(currentDir, "frontend", "AI");
            if (Directory.Exists(aiPath))
            {
                return aiPath;
            }

            DirectoryInfo parent = Directory.GetParent(currentDir);
            currentDir = parent?.FullName;
        }

        return null;
    }

    static async Task LoadCharacterFromDirectoryAsync(SqliteConnection connection, string characterDir, string aiDirectory)
    {
        try
        {
            string characterName = Path.GetFileName(characterDir);
            Console.WriteLine($"📝 Processing character: {characterName}");

            // Look for YAML file
            var yamlFiles = Directory.GetFiles(characterDir, "*.yaml");
            if (yamlFiles.Length == 0)
            {
                Console.WriteLine($"⚠️  No YAML file found for {characterName}");
                return;
            }

            string yamlFile = yamlFiles[0];
            string yamlContent = await File.ReadAllTextAsync(yamlFile);

            // Look for image file
            var imageFiles = Directory.GetFiles(characterDir, "*.jpg")
                .Concat(Directory.GetFiles(characterDir, "*.png"))
                .Concat(Directory.GetFiles(characterDir, "*.jpeg"))
                .ToArray();

            string imagePath = "default-avatar.png";
            if (imageFiles.Length > 0)
            {
                string relativeToAiDir = Path.GetRelativePath(aiDirectory, imageFiles[0]);
                imagePath = $"character-images/{relativeToAiDir.Replace('\\', '/')}";
            }

            // Check if character already exists
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM Avatars WHERE Name = @name;";
            checkCommand.Parameters.AddWithValue("@name", characterName);

            var existingCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
            if (existingCount > 0)
            {
                Console.WriteLine($"✅ Character {characterName} already exists, skipping");
                return;
            }

            // Parse basic info from YAML (simple parsing)
            string description = ExtractYamlValue(yamlContent, "description") ?? "";
            string personality = ExtractYamlValue(yamlContent, "personality") ?? "";
            string scenario = ExtractYamlValue(yamlContent, "scenario") ?? "";
            string firstMessage = ExtractYamlValue(yamlContent, "first_message") ?? "";

            // Insert character
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Avatars (
                    Id, UserId, Name, ImagePath, Personality, VoiceSettings, Description,
                    Scenario, FirstMessage, MessageExample, SystemPrompt, PostHistoryInstructions,
                    AlternateGreetings, Tags, Creator, CreatorNotes, CharacterVersion,
                    Talkativeness, IsFavorite, Extensions, YamlProfile, CreatedAt, LastModified
                ) VALUES (
                    @Id, @UserId, @Name, @ImagePath, @Personality, @VoiceSettings, @Description,
                    @Scenario, @FirstMessage, @MessageExample, @SystemPrompt, @PostHistoryInstructions,
                    @AlternateGreetings, @Tags, @Creator, @CreatorNotes, @CharacterVersion,
                    @Talkativeness, @IsFavorite, @Extensions, @YamlProfile, @CreatedAt, @LastModified
                );";

            var characterId = Guid.NewGuid().ToString();
            var defaultUserId = "00000000-0000-0000-0000-000000000001";
            var currentTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            insertCommand.Parameters.AddWithValue("@Id", characterId);
            insertCommand.Parameters.AddWithValue("@UserId", defaultUserId);
            insertCommand.Parameters.AddWithValue("@Name", characterName);
            insertCommand.Parameters.AddWithValue("@ImagePath", imagePath);
            insertCommand.Parameters.AddWithValue("@Personality", personality);
            insertCommand.Parameters.AddWithValue("@VoiceSettings", "{}");
            insertCommand.Parameters.AddWithValue("@Description", description);
            insertCommand.Parameters.AddWithValue("@Scenario", scenario);
            insertCommand.Parameters.AddWithValue("@FirstMessage", firstMessage);
            insertCommand.Parameters.AddWithValue("@MessageExample", "");
            insertCommand.Parameters.AddWithValue("@SystemPrompt", $"You are {characterName}. {description} {personality}");
            insertCommand.Parameters.AddWithValue("@PostHistoryInstructions", "");
            insertCommand.Parameters.AddWithValue("@AlternateGreetings", "[]");
            insertCommand.Parameters.AddWithValue("@Tags", "[]");
            insertCommand.Parameters.AddWithValue("@Creator", "SwAIvyn");
            insertCommand.Parameters.AddWithValue("@CreatorNotes", "");
            insertCommand.Parameters.AddWithValue("@CharacterVersion", "1.0");
            insertCommand.Parameters.AddWithValue("@Talkativeness", 0.5);
            insertCommand.Parameters.AddWithValue("@IsFavorite", 0);
            insertCommand.Parameters.AddWithValue("@Extensions", "{}");
            insertCommand.Parameters.AddWithValue("@YamlProfile", yamlContent);
            insertCommand.Parameters.AddWithValue("@CreatedAt", currentTime);
            insertCommand.Parameters.AddWithValue("@LastModified", currentTime);

            await insertCommand.ExecuteNonQueryAsync();
            Console.WriteLine($"✅ Created character: {characterName} with ID: {characterId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading character from {characterDir}: {ex.Message}");
        }
    }

    static string ExtractYamlValue(string yamlContent, string key)
    {
        try
        {
            var lines = yamlContent.Split('\n');
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Substring(line.IndexOf(':') + 1).Trim();
                    // Remove quotes if present
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    return value;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return null;
    }
}
