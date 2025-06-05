using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Direct database service for creating Users table and default user
    /// </summary>
    public interface IDirectDatabaseService
    {
        Task CreateUsersTableAndDefaultUserAsync();
    }

    public class DirectDatabaseService : IDirectDatabaseService
    {
        private readonly string _connectionString;
        private readonly ISimpleLoggerService _logger;

        public DirectDatabaseService(IConfiguration configuration, ISimpleLoggerService logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        public async Task CreateUsersTableAndDefaultUserAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // First, check if Users table exists
                var checkTableSql = @"
                    SELECT name FROM sqlite_master 
                    WHERE type='table' AND name='Users';";

                using var checkCommand = new SqliteCommand(checkTableSql, connection);
                var tableExists = await checkCommand.ExecuteScalarAsync();

                if (tableExists == null)
                {
                    _logger.LogInfo("Users table does not exist. Creating Users table...");

                    // Create Users table with the exact schema from the migration
                    var createTableSql = @"
                        CREATE TABLE Users (
                            Id TEXT NOT NULL,
                            Username TEXT NOT NULL,
                            PasswordHash TEXT NOT NULL,
                            PINCode TEXT NOT NULL,
                            RecoveryPhrase TEXT NOT NULL,
                            CreatedAt TEXT NOT NULL,
                            LastLogin TEXT NOT NULL,
                            CONSTRAINT PK_Users PRIMARY KEY (Id)
                        );";

                    using var createTableCommand = new SqliteCommand(createTableSql, connection);
                    await createTableCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("Users table created successfully.");

                    // Create unique index on Username
                    var createIndexSql = @"
                        CREATE UNIQUE INDEX IX_Users_Username ON Users (Username);";

                    using var createIndexCommand = new SqliteCommand(createIndexSql, connection);
                    await createIndexCommand.ExecuteNonQueryAsync();
                    _logger.LogInfo("Unique index on Username created successfully.");
                }
                else
                {
                    _logger.LogInfo("Users table already exists.");
                }

                // Check if any users exist
                var checkUsersSql = "SELECT COUNT(*) FROM Users;";
                using var checkUsersCommand = new SqliteCommand(checkUsersSql, connection);
                var userCount = Convert.ToInt32(await checkUsersCommand.ExecuteScalarAsync());

                if (userCount == 0)
                {
                    _logger.LogInfo("No users found. Creating default user named 'user'...");

                    // Insert default user named "user"
                    var insertUserSql = @"
                        INSERT INTO Users (Id, Username, PasswordHash, PINCode, RecoveryPhrase, CreatedAt, LastLogin)
                        VALUES (@Id, @Username, @PasswordHash, @PINCode, @RecoveryPhrase, @CreatedAt, @LastLogin);";

                    using var insertCommand = new SqliteCommand(insertUserSql, connection);
                    var userId = "00000000-0000-0000-0000-000000000001";
                    var currentTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                    insertCommand.Parameters.AddWithValue("@Id", userId);
                    insertCommand.Parameters.AddWithValue("@Username", "user");
                    insertCommand.Parameters.AddWithValue("@PasswordHash", ""); // No password needed for single-user app
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
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateUsersTableAndDefaultUserAsync: {ex.Message}");
                throw;
            }
        }
    }
}
