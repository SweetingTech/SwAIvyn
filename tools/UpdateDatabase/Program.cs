using System;
using System.Data.SQLite;

class Program
{
    static void Main()
    {
        string connectionString = "Data Source=C:\\Users\\djay\\Desktop\\data\\swai-vyn.db";
        
        try
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                Console.WriteLine("Connected to database successfully.");

                // Add missing columns to Avatars table
                string[] alterCommands = {
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

                foreach (string command in alterCommands)
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
}
