using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using YamlDotNet.Serialization;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for character management operations
    /// </summary>
    public interface ICharacterService
    {
        /// <summary>
        /// Gets all characters for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of characters</returns>
        Task<List<AvatarInfo>> GetCharactersAsync(Guid? userId = null);

        /// <summary>
        /// Gets a character by ID
        /// </summary>
        /// <param name="characterId">Character ID</param>
        /// <returns>Character or null if not found</returns>
        Task<AvatarInfo?> GetCharacterByIdAsync(Guid characterId);

        /// <summary>
        /// Gets a character by name
        /// </summary>
        /// <param name="name">Character name</param>
        /// <param name="userId">User ID</param>
        /// <returns>Character or null if not found</returns>
        Task<AvatarInfo?> GetCharacterByNameAsync(string name, Guid? userId = null);

        /// <summary>
        /// Creates a new character
        /// </summary>
        /// <param name="character">Character to create</param>
        /// <returns>Created character</returns>
        Task<AvatarInfo> CreateCharacterAsync(AvatarInfo character);

        /// <summary>
        /// Updates an existing character
        /// </summary>
        /// <param name="character">Character to update</param>
        /// <returns>Updated character</returns>
        Task<AvatarInfo> UpdateCharacterAsync(AvatarInfo character);

        /// <summary>
        /// Deletes a character
        /// </summary>
        /// <param name="characterId">Character ID</param>
        /// <returns>True if deleted successfully</returns>
        Task<bool> DeleteCharacterAsync(Guid characterId);

        /// <summary>
        /// Gets the system prompt for a character
        /// </summary>
        /// <param name="character">Character</param>
        /// <returns>System prompt</returns>
        string GetSystemPrompt(AvatarInfo character);

        /// <summary>
        /// Converts YAML profile to system prompt
        /// </summary>
        /// <param name="yamlProfile">YAML profile content</param>
        /// <returns>System prompt</returns>
        string ConvertYamlToPrompt(string yamlProfile);
    }

    /// <summary>
    /// Service for character management operations
    /// </summary>
    public class CharacterService : ICharacterService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ISimpleLoggerService _logger;

        public CharacterService(ApplicationDbContext dbContext, ISimpleLoggerService logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<AvatarInfo>> GetCharactersAsync(Guid? userId = null)
        {
            try
            {
                var query = _dbContext.Avatars.AsQueryable();

                // If userId is provided, filter by user, otherwise get all characters
                if (userId.HasValue)
                {
                    query = query.Where(a => a.UserId == userId.Value);
                }

                return await query.OrderBy(a => a.Name).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get characters", ex);
                return new List<AvatarInfo>();
            }
        }

        /// <inheritdoc/>
        public async Task<AvatarInfo?> GetCharacterByIdAsync(Guid characterId)
        {
            try
            {
                return await _dbContext.Avatars.FirstOrDefaultAsync(a => a.Id == characterId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get character by ID {characterId}", ex);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<AvatarInfo?> GetCharacterByNameAsync(string name, Guid? userId = null)
        {
            try
            {
                var query = _dbContext.Avatars.Where(a => a.Name == name);

                if (userId.HasValue)
                {
                    query = query.Where(a => a.UserId == userId.Value);
                }

                return await query.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get character by name {name}", ex);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<AvatarInfo> CreateCharacterAsync(AvatarInfo character)
        {
            try
            {
                character.Id = Guid.NewGuid();
                character.CreatedAt = DateTime.UtcNow;
                character.LastModified = DateTime.UtcNow;

                _dbContext.Avatars.Add(character);
                await _dbContext.SaveChangesAsync();

                _logger.LogInfo($"Created character: {character.Name} with ID: {character.Id}");
                return character;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create character {character.Name}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<AvatarInfo> UpdateCharacterAsync(AvatarInfo character)
        {
            try
            {
                character.LastModified = DateTime.UtcNow;
                _dbContext.Avatars.Update(character);
                await _dbContext.SaveChangesAsync();

                _logger.LogInfo($"Updated character: {character.Name} with ID: {character.Id}");
                return character;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update character {character.Id}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteCharacterAsync(Guid characterId)
        {
            try
            {
                var character = await _dbContext.Avatars.FindAsync(characterId);
                if (character == null)
                {
                    return false;
                }

                _dbContext.Avatars.Remove(character);
                await _dbContext.SaveChangesAsync();

                _logger.LogInfo($"Deleted character: {character.Name} with ID: {characterId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete character {characterId}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public string GetSystemPrompt(AvatarInfo character)
        {
            if (!string.IsNullOrEmpty(character.SystemPrompt))
            {
                return character.SystemPrompt;
            }

            if (!string.IsNullOrEmpty(character.YamlProfile))
            {
                return ConvertYamlToPrompt(character.YamlProfile);
            }

            // Fallback to basic prompt
            return $@"You are roleplaying as {character.Name}.
Description: {character.Description}
Personality: {character.Personality}
Remain in character and respond accordingly.";
        }

        /// <inheritdoc/>
        public string ConvertYamlToPrompt(string yamlProfile)
        {
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var yaml = deserializer.Deserialize<Dictionary<string, object>>(yamlProfile);

                return $@"
You are roleplaying as the AI character below. Remain in character.

Name: {GetValueOrDefault(yaml, "name", "Unknown")}
Description: {GetValueOrDefault(yaml, "description", "")}
Personality: {GetValueOrDefault(yaml, "personality", "")}
Scenario: {GetValueOrDefault(yaml, "scenario", "")}
Talkativeness Level: {GetValueOrDefault(yaml, "talkativeness", "0.5")}

Start the conversation with:
{GetValueOrDefault(yaml, "first_message", "")}

Example:
{GetValueOrDefault(yaml, "message_example", "")}
".Trim();
            }
            catch (Exception ex)
            {
                return $"Error parsing YAML: {ex.Message}";
            }
        }

        private static string GetValueOrDefault(Dictionary<string, object> yaml, string key, string defaultValue)
        {
            if (yaml.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }
    }
}
