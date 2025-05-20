using Microsoft.Data.Sqlite;
using System;
using System.IO;

class Program
{
    static void Main()
    {
        // Initialize SQLitePCL.raw
        SQLitePCL.Batteries_V2.Init();

        // Create a connection
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Get SQLite version
        using var versionCmd = connection.CreateCommand();
        versionCmd.CommandText = "SELECT sqlite_version()";
        var version = versionCmd.ExecuteScalar();
        Console.WriteLine($"SQLite Version: {version}");

        // Enable extensions
        connection.EnableExtensions(true);

        // Try to load the extension
        try
        {
            string extensionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sqlite-vss.dll");
            Console.WriteLine($"Loading extension from: {extensionPath}");
            connection.LoadExtension(extensionPath);
            Console.WriteLine("Extension loaded successfully!");

            // Try to create a vector table
            using var createTableCmd = connection.CreateCommand();
            createTableCmd.CommandText = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS TestVectors
                USING vss0(
                    id TEXT PRIMARY KEY,
                    embedding BLOB,
                    metadata TEXT,
                    dims(1536),
                    distance('cosine')
                );";
            createTableCmd.ExecuteNonQuery();
            Console.WriteLine("Vector table created successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading extension: {ex.Message}");
            Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
        }
    }
}
