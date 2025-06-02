using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

class Program
{    static void Main(string[] args)
    {
        // Navigate from tools/DatabaseRebuild back to SwAIvyn root
        string rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string databasePath = Path.Combine(rootPath, "data", "swai-vyn.db");
        // Look for bootdb.sql in the current working directory (tools/DatabaseRebuild)
        string sqlScriptPath = Path.Combine(Directory.GetCurrentDirectory(), "bootdb.sql");
        // Look for JSON export file in the root directory
        string jsonExportPath = Path.Combine(rootPath, "swai_database_export_20250529.json");

        Console.WriteLine("🔄 SwAIvyn Database Rebuild & Import Tool");
        Console.WriteLine("=========================================");
        Console.WriteLine($"Root path: {rootPath}");
        Console.WriteLine($"Database: {databasePath}");
        Console.WriteLine($"SQL Script: {sqlScriptPath}");
        Console.WriteLine($"JSON Export: {jsonExportPath}");
        Console.WriteLine();

        try
        {
            // Check if the SQL script exists
            if (!File.Exists(sqlScriptPath))
            {
                Console.WriteLine($"❌ Error: SQL script file '{sqlScriptPath}' not found!");
                Environment.Exit(1);
            }

            // Check if database file exists and back it up
            if (File.Exists(databasePath))
            {
                string backupPath = $"{databasePath}.backup.{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(databasePath, backupPath);
                Console.WriteLine($"📋 Backed up existing database to: {Path.GetFileName(backupPath)}");
            }

            // Read the SQL script content
            string sqlContent = File.ReadAllText(sqlScriptPath);
            Console.WriteLine("📖 SQL script loaded successfully");

            // Create connection string
            string connectionString = $"Data Source={databasePath}";

            // Create connection
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            Console.WriteLine("✅ Connected to database");

            // Split SQL content by semicolons and execute each statement
            var statements = sqlContent.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            using var command = connection.CreateCommand();
            int statementCount = 0;
            int successCount = 0;

            foreach (string statement in statements)
            {
                string trimmedStatement = statement.Trim();
                if (string.IsNullOrEmpty(trimmedStatement) || 
                    trimmedStatement.StartsWith("--") || 
                    trimmedStatement.StartsWith("/*"))
                {
                    continue;
                }

                try
                {
                    command.CommandText = trimmedStatement;
                    int result = command.ExecuteNonQuery();
                    statementCount++;
                    successCount++;

                    // Show progress for major operations
                    if (trimmedStatement.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase) ||
                        trimmedStatement.StartsWith("DROP", StringComparison.OrdinalIgnoreCase) ||
                        trimmedStatement.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
                        trimmedStatement.StartsWith("ALTER", StringComparison.OrdinalIgnoreCase))
                    {
                        string operation = trimmedStatement.Split(' ')[0].ToUpper();
                        Console.WriteLine($"📋 Executed {operation} statement ({statementCount})");
                    }
                }
                catch (Exception ex)
                {
                    statementCount++;
                    Console.WriteLine($"⚠️  Warning: Failed to execute statement {statementCount}: {ex.Message}");
                    Console.WriteLine($"   Statement: {trimmedStatement.Substring(0, Math.Min(100, trimmedStatement.Length))}...");
                }
            }

            Console.WriteLine($"✅ Executed {successCount}/{statementCount} SQL statements successfully");            // Verify database tables
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
            using var reader = command.ExecuteReader();
            
            var tables = new List<string>();
            while (reader.Read())
            {
                tables.Add(reader["name"].ToString()!);
            }
            reader.Close();

            Console.WriteLine("\n📊 Database tables created:");
            foreach (string table in tables)
            {
                Console.WriteLine($"   - {table}");
            }

            // Show verification results
            Console.WriteLine("\n🔍 Running verification queries...");
            
            var verificationQueries = new[]
            {
                new { Query = "SELECT COUNT(*) FROM Users", Name = "Users" },
                new { Query = "SELECT COUNT(*) FROM Avatars", Name = "Avatars" },
                new { Query = "SELECT COUNT(*) FROM Conversations", Name = "Conversations" },
                new { Query = "SELECT COUNT(*) FROM Settings", Name = "Settings" }
            };

            foreach (var query in verificationQueries)
            {
                try
                {
                    command.CommandText = query.Query;
                    long count = (long)command.ExecuteScalar()!;
                    Console.WriteLine($"   {query.Name}: {count} records");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   {query.Name}: Could not verify - {ex.Message}");
                }
            }            Console.WriteLine("\n🎉 Database rebuild completed successfully!");
            Console.WriteLine("💡 Your SwAIvyn database has been reset to its initial state with seed data.");

            // Now import data from JSON export if it exists
            if (File.Exists(jsonExportPath))
            {
                Console.WriteLine("\n📦 Importing data from JSON export...");
                ImportFromJson(connection, jsonExportPath);
            }
            else
            {
                Console.WriteLine($"\n⚠️  JSON export file not found: {jsonExportPath}");
                Console.WriteLine("Skipping data import. Database created with default seed data only.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }    static void ImportFromJson(SqliteConnection connection, string jsonPath)
    {
        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            Console.WriteLine("📄 JSON file loaded successfully");

            // Check if data is under "tables" key
            if (!root.TryGetProperty("tables", out var tablesElement))
            {
                Console.WriteLine("❌ Error: JSON file doesn't contain 'tables' property");
                return;
            }

            // Import order: Users first, then dependent tables
            string[] importOrder = { "Users", "Folders", "Avatars", "Conversations", "ChatHistories", "Settings", "Memories", "Prompts", "ChatIndices" };

            using var command = connection.CreateCommand();

            foreach (string tableName in importOrder)
            {
                if (tablesElement.TryGetProperty(tableName, out var tableElement) && 
                    tableElement.TryGetProperty("data", out var tableData) && 
                    tableData.ValueKind == JsonValueKind.Array)
                {
                    int recordCount = 0;
                    Console.WriteLine($"📋 Importing {tableName}...");

                    foreach (var record in tableData.EnumerateArray())
                    {
                        try
                        {
                            ImportRecord(command, tableName, record);
                            recordCount++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️  Warning: Failed to import record in {tableName}: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"✅ Imported {recordCount} records to {tableName}");
                }
                else
                {
                    Console.WriteLine($"⚠️  No data found for table: {tableName}");
                }
            }

            Console.WriteLine("🎉 JSON import completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error importing JSON: {ex.Message}");
        }
    }

    static void ImportRecord(SqliteCommand command, string tableName, JsonElement record)
    {
        var columns = new List<string>();
        var parameters = new List<string>();
        var values = new List<object>();

        foreach (var property in record.EnumerateObject())
        {
            columns.Add($"[{property.Name}]");
            parameters.Add($"@{property.Name}");
            
            object value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetDouble(),
                JsonValueKind.True => 1,
                JsonValueKind.False => 0,
                JsonValueKind.Null => DBNull.Value,
                _ => property.Value.GetRawText()
            };
            values.Add(value);
        }

        if (columns.Count > 0)
        {
            string sql = $"INSERT OR REPLACE INTO [{tableName}] ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})";
            command.CommandText = sql;
            command.Parameters.Clear();

            for (int i = 0; i < parameters.Count; i++)
            {
                command.Parameters.AddWithValue(parameters[i], values[i]);
            }

            command.ExecuteNonQuery();
        }
    }
}
