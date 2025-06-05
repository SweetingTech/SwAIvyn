using System;
using System.Data;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string dbPath = @"..\..\data\swai-vyn.db";
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"❌ Database not found at: {dbPath}");
            return;
        }

        Console.WriteLine("🔧 Creating Avatars and Prompts tables...");

        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            Console.WriteLine("✅ Connected to database");

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
            CREATE INDEX IF NOT EXISTS ""IX_Avatars_UserId"" ON ""Avatars"" (""UserId"");
            CREATE INDEX IF NOT EXISTS ""IX_Prompts_AvatarId_IsActive"" ON ""Prompts"" (""AvatarId"", ""IsActive"");";

            using var command = connection.CreateCommand();

            Console.WriteLine("📋 Creating Avatars table...");
            command.CommandText = createAvatarsTable;
            await command.ExecuteNonQueryAsync();

            Console.WriteLine("📋 Creating Prompts table...");
            command.CommandText = createPromptsTable;
            await command.ExecuteNonQueryAsync();

            Console.WriteLine("📋 Creating indexes...");
            command.CommandText = createIndexes;
            await command.ExecuteNonQueryAsync();

            // Verify tables exist
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND (name='Avatars' OR name='Prompts');";
            using var reader = await command.ExecuteReaderAsync();
            
            Console.WriteLine("✅ Tables created successfully:");
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"   - {reader["name"]}");
            }
            reader.Close();

            // Check current avatar count
            command.CommandText = "SELECT COUNT(*) FROM Avatars;";
            var avatarCount = await command.ExecuteScalarAsync();
            Console.WriteLine($"📊 Current Avatars count: {avatarCount}");

            Console.WriteLine("🎉 Database setup completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}
