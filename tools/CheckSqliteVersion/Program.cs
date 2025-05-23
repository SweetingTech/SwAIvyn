using Microsoft.Data.Sqlite;
using System;

class Program
{
    static void Main()
    {
        // Initialize SQLitePCL.raw
        SQLitePCL.Batteries_V2.Init();

        // Create a connection
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Execute a query to get the SQLite version
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version()";
        var version = command.ExecuteScalar();

        Console.WriteLine($"SQLite Version: {version}");
    }
}
