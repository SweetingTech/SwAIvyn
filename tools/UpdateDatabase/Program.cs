using System;
using System.Data.SQLite;
using System.IO;

class Program
{
    static void Main()
    {
        // Find the database path dynamically
        string dbPath = FindDatabasePath();

        if (string.IsNullOrEmpty(dbPath))
        {
            Console.WriteLine("❌ Database not found. Please ensure the database exists.");
            return;
        }

        Console.WriteLine($"📍 Using database: {dbPath}");
        string connectionString = $"Data Source={dbPath}";
        
        try
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                Console.WriteLine("Connected to database successfully.");

                // Add missing columns to Users table
                string[] userAlterCommands = {
                    "ALTER TABLE Users ADD COLUMN LastSelectedCharacterId TEXT;"
                };

                // Add missing columns to Avatars table
                string[] avatarAlterCommands = {
                    "ALTER TABLE Avatars ADD COLUMN AlternateGreetings TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN CharacterVersion TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN Creator TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN CreatorNotes TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN Extensions TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN FirstMessage TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN MessageExample TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN PostHistoryInstructions TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN Scenario TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN SystemPrompt TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN Tags TEXT;",
                    "ALTER TABLE Avatars ADD COLUMN Talkativeness REAL DEFAULT 0.5;",
                    "ALTER TABLE Avatars ADD COLUMN YamlProfile TEXT;"
                };

                // Execute Users table updates
                foreach (string command in userAlterCommands)
                {
                    try
                    {
                        using (var cmd = new SQLiteCommand(command, connection))
                        {
                            cmd.ExecuteNonQuery();
                            Console.WriteLine($"Executed: {command}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Column might already exist or error: {ex.Message}");
                    }
                }

                // Execute Avatars table updates
                foreach (string command in avatarAlterCommands)
                {
                    try
                    {
                        using (var cmd = new SQLiteCommand(command, connection))
                        {
                            cmd.ExecuteNonQuery();
                            Console.WriteLine($"Executed: {command}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Column might already exist or error: {ex.Message}");
                    }
                }

                Console.WriteLine("Database schema updated successfully!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static string FindDatabasePath()
    {
        // Try multiple possible paths relative to the tool's location
        string[] possiblePaths = {
            "../../Sqldatabase/swai-vyn.db",           // From tools/UpdateDatabase to root/Sqldatabase
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
}
