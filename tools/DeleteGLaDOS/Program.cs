using Microsoft.Data.Sqlite;

var connectionString = "Data Source=../data/swai-vyn.db";
using var connection = new SqliteConnection(connectionString);
connection.Open();

var command = connection.CreateCommand();
command.CommandText = "DELETE FROM Avatars WHERE Name = 'GLaDOS'";
var rowsAffected = command.ExecuteNonQuery();

Console.WriteLine($"Deleted {rowsAffected} GLaDOS character(s)");
