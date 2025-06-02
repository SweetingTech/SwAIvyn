using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for managing AI character profiles.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CharacterController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CharacterController> _logger;

        public CharacterController(ApplicationDbContext dbContext, ILogger<CharacterController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Test endpoint to verify controller is working
        /// </summary>
        /// <returns>Test response</returns>
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "Character controller is working", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Gets count of characters in database
        /// </summary>
        /// <returns>Character count</returns>
        [HttpGet("count")]
        public async Task<IActionResult> GetCharacterCount()
        {
            try
            {
                var count = await _dbContext.Avatars.CountAsync();
                return Ok(new { count = count, message = $"Found {count} characters in database" });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting character count", ex);
                return StatusCode(500, $"An error occurred while getting character count: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all character profiles for a user.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of character profiles</returns>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetCharacters(Guid userId)
        {
            var characters = await _dbContext.Avatars
                .Where(c => c.UserId == userId)
                .ToListAsync();

            return Ok(characters);
        }

        /// <summary>
        /// Gets all global character profiles (not tied to any specific user).
        /// </summary>
        /// <returns>List of global character profiles</returns>
        [HttpGet("global")]
        public async Task<IActionResult> GetGlobalCharacters()
        {
            var characters = await _dbContext.Avatars
                .Where(c => c.UserId == Guid.Empty)
                .ToListAsync();

            return Ok(characters);
        }

        /// <summary>
        /// Creates a new character profile.
        /// </summary>
        /// <param name="request">Character creation request</param>
        /// <returns>Created character profile</returns>
        [HttpPost]
        public async Task<IActionResult> CreateCharacter([FromBody] CreateCharacterRequest request)
        {
            var character = new AvatarInfo
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Name = request.Name,
                ImagePath = request.ImagePath,
                Personality = request.Personality,
                VoiceSettings = request.VoiceSettings,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            _dbContext.Avatars.Add(character);
            await _dbContext.SaveChangesAsync();

            return Ok(character);
        }

        /// <summary>
        /// Updates an existing character profile.
        /// </summary>
        /// <param name="id">Character ID</param>
        /// <param name="request">Character update request</param>
        /// <returns>Updated character profile</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCharacter(Guid id, [FromBody] UpdateCharacterRequest request)
        {
            var character = await _dbContext.Avatars.FindAsync(id);
            if (character == null)
            {
                return NotFound();
            }

            character.Name = request.Name;
            character.ImagePath = request.ImagePath;
            character.Personality = request.Personality;
            character.VoiceSettings = request.VoiceSettings;
            character.LastModified = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(character);
        }

        /// <summary>
        /// Deletes a character profile.
        /// </summary>
        /// <param name="id">Character ID</param>
        /// <returns>Action result</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCharacter(Guid id)
        {
            var character = await _dbContext.Avatars.FindAsync(id);
            if (character == null)
            {
                return NotFound();
            }

            _dbContext.Avatars.Remove(character);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Uploads and creates a character from a YAML character card
        /// </summary>
        /// <param name="request">Character card upload request</param>
        /// <returns>The created character</returns>
        [HttpPost("upload-card")]
        public async Task<IActionResult> UploadCharacterCard([FromBody] UploadCharacterCardRequest request)
        {
            try
            {
                // Parse YAML to extract character data
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlData = deserializer.Deserialize<Dictionary<string, object>>(request.CharacterCardYaml);

                // Helper function to safely get values from YAML
                string GetValueOrDefault(Dictionary<string, object> data, string key, string defaultValue = "")
                {
                    if (data.ContainsKey(key) && data[key] != null)
                        return data[key].ToString();
                    return defaultValue;
                }

                var character = new SwAIvyn.Data.Entities.AvatarInfo
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    Name = GetValueOrDefault(yamlData, "name", "Unnamed Character"),
                    Description = GetValueOrDefault(yamlData, "description"),
                    Personality = GetValueOrDefault(yamlData, "personality"),
                    Scenario = GetValueOrDefault(yamlData, "scenario"),
                    FirstMessage = GetValueOrDefault(yamlData, "first_message"),
                    MessageExample = GetValueOrDefault(yamlData, "message_example"),
                    CreatorNotes = GetValueOrDefault(yamlData, "creator_notes"),
                    CharacterVersion = GetValueOrDefault(yamlData, "character_version", "1.0"),
                    Talkativeness = float.TryParse(GetValueOrDefault(yamlData, "talkativeness", "0.5"), out float talk) ? talk : 0.5f,
                    IsFavorite = bool.TryParse(GetValueOrDefault(yamlData, "favorite", "false"), out bool fav) && fav,
                    Tags = yamlData.ContainsKey("tags") && yamlData["tags"] is List<object> tagList
                        ? System.Text.Json.JsonSerializer.Serialize(tagList.Select(t => t.ToString()).ToArray())
                        : "[]",
                    Creator = "SwAIvyn",
                    PostHistoryInstructions = "",
                    AlternateGreetings = "[]",
                    Extensions = "{}",
                    YamlProfile = request.CharacterCardYaml,
                    ImagePath = "default-avatar.png", // Will be updated when image is uploaded
                    VoiceSettings = "{}",
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                // Generate system prompt using the CharacterService
                character.SystemPrompt = CharacterService.ConvertYamlToPrompt(request.CharacterCardYaml);

                _dbContext.Avatars.Add(character);
                await _dbContext.SaveChangesAsync();

                return Ok(character);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing character card: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports a character as a JSON character card
        /// </summary>
        /// <param name="id">Character ID</param>
        /// <returns>Character card JSON</returns>
        [HttpGet("{id}/export")]
        public async Task<IActionResult> ExportCharacterCard(Guid id)
        {
            try
            {
                var character = await _dbContext.Avatars.FindAsync(id);
                if (character == null)
                    return NotFound();

                var characterCard = new CharacterCard
                {
                    Name = character.Name,
                    Description = character.Description,
                    Personality = character.Personality,
                    Scenario = character.Scenario,
                    FirstMessage = character.FirstMessage,
                    MessageExample = character.MessageExample,
                    CreatorComment = character.CreatorNotes,
                    Avatar = "none",
                    Chat = "",
                    Talkativeness = character.Talkativeness.ToString(),
                    Fav = character.IsFavorite,
                    Tags = System.Text.Json.JsonSerializer.Deserialize<string[]>(character.Tags) ?? new string[0],
                    Spec = "chara_card_v3",
                    SpecVersion = "3.0",
                    Data = new CharacterCardData
                    {
                        Name = character.Name,
                        Description = character.Description,
                        Personality = character.Personality,
                        Scenario = character.Scenario,
                        FirstMessage = character.FirstMessage,
                        MessageExample = character.MessageExample,
                        CreatorNotes = character.CreatorNotes,
                        SystemPrompt = character.SystemPrompt,
                        PostHistoryInstructions = character.PostHistoryInstructions,
                        Tags = System.Text.Json.JsonSerializer.Deserialize<string[]>(character.Tags) ?? new string[0],
                        Creator = character.Creator,
                        CharacterVersion = character.CharacterVersion,
                        AlternateGreetings = System.Text.Json.JsonSerializer.Deserialize<string[]>(character.AlternateGreetings) ?? new string[0],
                        Extensions = System.Text.Json.JsonSerializer.Deserialize<object>(character.Extensions) ?? new object(),
                        GroupOnlyGreetings = new string[0]
                    },
                    CreateDate = character.CreatedAt.ToString("yyyy-MM-dd @HH'h' mm'm' ss's' ffffff'ms'")
                };

                return Ok(characterCard);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error exporting character card: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates or updates a character from YAML profile
        /// </summary>
        /// <param name="request">YAML character request</param>
        /// <returns>The created/updated character</returns>
        [HttpPost("yaml")]
        public async Task<IActionResult> CreateCharacterFromYaml([FromBody] YamlCharacterRequest request)
        {
            try
            {
                // Parse YAML to extract character data
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlData = deserializer.Deserialize<Dictionary<string, object>>(request.YamlProfile);

                var character = new SwAIvyn.Data.Entities.AvatarInfo
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    YamlProfile = request.YamlProfile,
                    Name = yamlData.ContainsKey("name") ? yamlData["name"]?.ToString() ?? "YAML Character" : "YAML Character",
                    Description = yamlData.ContainsKey("description") ? yamlData["description"]?.ToString() ?? "" : "",
                    Personality = yamlData.ContainsKey("personality") ? yamlData["personality"]?.ToString() ?? "" : "",
                    Scenario = yamlData.ContainsKey("scenario") ? yamlData["scenario"]?.ToString() ?? "" : "",
                    FirstMessage = yamlData.ContainsKey("first_message") ? yamlData["first_message"]?.ToString() ?? "" : "",
                    MessageExample = yamlData.ContainsKey("mes_example") ? yamlData["mes_example"]?.ToString() ?? "" : "",
                    ImagePath = "default-avatar.png",
                    VoiceSettings = "default",
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                // Generate system prompt from character data
                character.SystemPrompt = GenerateSystemPromptFromCharacterData(character);

                _dbContext.Avatars.Add(character);
                await _dbContext.SaveChangesAsync();

                return Ok(character);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error creating character from YAML: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates a character's YAML profile
        /// </summary>
        /// <param name="id">Character ID</param>
        /// <param name="request">YAML character request</param>
        /// <returns>The updated character</returns>
        [HttpPut("{id}/yaml")]
        public async Task<IActionResult> UpdateCharacterYaml(Guid id, [FromBody] YamlCharacterRequest request)
        {
            try
            {
                var character = await _dbContext.Avatars.FindAsync(id);
                if (character == null)
                    return NotFound();

                // Parse YAML to extract character data
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlData = deserializer.Deserialize<Dictionary<string, object>>(request.YamlProfile);

                // Update character fields from YAML
                character.YamlProfile = request.YamlProfile;
                character.Name = yamlData.ContainsKey("name") ? yamlData["name"]?.ToString() ?? character.Name : character.Name;
                character.Description = yamlData.ContainsKey("description") ? yamlData["description"]?.ToString() ?? "" : character.Description;
                character.Personality = yamlData.ContainsKey("personality") ? yamlData["personality"]?.ToString() ?? "" : character.Personality;
                character.Scenario = yamlData.ContainsKey("scenario") ? yamlData["scenario"]?.ToString() ?? "" : character.Scenario;
                character.FirstMessage = yamlData.ContainsKey("first_message") ? yamlData["first_message"]?.ToString() ?? "" : character.FirstMessage;
                character.MessageExample = yamlData.ContainsKey("mes_example") ? yamlData["mes_example"]?.ToString() ?? "" : character.MessageExample;
                character.LastModified = DateTime.UtcNow;

                // Regenerate system prompt from updated character data
                character.SystemPrompt = GenerateSystemPromptFromCharacterData(character);

                await _dbContext.SaveChangesAsync();

                return Ok(character);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating character YAML: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the default character for a user
        /// </summary>
        /// <param name="request">Default character request</param>
        /// <returns>Success response</returns>
        [HttpPost("set-default")]
        public async Task<IActionResult> SetDefaultCharacter([FromBody] SetCharacterDefaultRequest request)
        {
            try
            {
                // Verify the character exists for this user
                var character = await _dbContext.Avatars
                    .FirstOrDefaultAsync(a => a.Id == request.CharacterId && a.UserId == request.UserId);

                if (character == null)
                {
                    return NotFound("Character not found for this user");
                }

                // Update the user's LastSelectedCharacterId
                var user = await _dbContext.Users.FindAsync(request.UserId);
                if (user != null)
                {
                    user.LastSelectedCharacterId = request.CharacterId;
                    await _dbContext.SaveChangesAsync();
                }

                // Also set it in settings for persistence
                var settingsService = HttpContext.RequestServices.GetRequiredService<ISettingsService>();
                await settingsService.SetDefaultCharacterAsync(request.UserId, request.CharacterId.ToString());

                return Ok(new { success = true, message = "Default character set successfully", characterName = character.Name });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error setting default character: {ex.Message}");
            }
        }

        // REMOVED: Filesystem loading endpoints - database-only approach
        // Users should upload YAML files through the upload endpoints instead

        // /// <summary>
        // /// Manually loads character cards from filesystem (GET version for testing)
        // /// </summary>
        // /// <returns>Success response</returns>
        // [HttpGet("load-from-filesystem")]
        // public async Task<IActionResult> LoadCharacterCardsFromFilesystemGet()
        // {
        //     try
        //     {
        //         var characterCardLoader = HttpContext.RequestServices.GetRequiredService<CharacterCardLoaderService>();
        //         await characterCardLoader.LoadCharacterCardsAsync();
        //
        //         return Ok(new { success = true, message = "Character cards loaded successfully" });
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError("Error loading character cards from filesystem", ex);
        //         return StatusCode(500, $"An error occurred while loading character cards: {ex.Message}");
        //     }
        // }
        //
        // /// <summary>
        // /// Manually loads character cards from filesystem
        // /// </summary>
        // /// <returns>Success response</returns>
        // [HttpPost("load-from-filesystem")]
        // public async Task<IActionResult> LoadCharacterCardsFromFilesystem()
        // {
        //     try
        //     {
        //         var characterCardLoader = HttpContext.RequestServices.GetRequiredService<CharacterCardLoaderService>();
        //         await characterCardLoader.LoadCharacterCardsAsync();
        //
        //         return Ok(new { success = true, message = "Character cards loaded successfully" });
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError("Error loading character cards from filesystem", ex);
        //         return StatusCode(500, $"An error occurred while loading character cards: {ex.Message}");
        //     }
        // }

        /// <summary>
        /// Generates a system prompt from character data
        /// </summary>
        /// <param name="character">Character data</param>
        /// <returns>System prompt string</returns>
        private string GenerateSystemPromptFromCharacterData(SwAIvyn.Data.Entities.AvatarInfo character)
        {
            var prompt = $"You are roleplaying as the AI character below. Remain fully in character at all times.\n\n";

            if (!string.IsNullOrEmpty(character.Name))
                prompt += $"Name: {character.Name}\n";

            if (!string.IsNullOrEmpty(character.Description))
                prompt += $"Description: {character.Description}\n";

            if (!string.IsNullOrEmpty(character.Personality))
                prompt += $"Personality: {character.Personality}\n";

            if (!string.IsNullOrEmpty(character.Scenario))
                prompt += $"Scenario: {character.Scenario}\n";

            if (!string.IsNullOrEmpty(character.CreatorNotes))
                prompt += $"Creator Notes: {character.CreatorNotes}\n";

            prompt += "\nRespond using the character's voice and personality at all times. Stay in character and never break the roleplay.";

            return prompt;
        }
    }

    /// <summary>
    /// Request model for creating a character profile.
    /// </summary>
    public class CreateCharacterRequest
    {
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public string Personality { get; set; }
        public string VoiceSettings { get; set; }
    }

    /// <summary>
    /// Request model for updating a character profile.
    /// </summary>
    public class UpdateCharacterRequest
    {
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public string Personality { get; set; }
        public string VoiceSettings { get; set; }
    }

    /// <summary>
    /// Request model for uploading a character card
    /// </summary>
    public class UploadCharacterCardRequest
    {
        public Guid UserId { get; set; }
        public string CharacterCardYaml { get; set; }
    }

    /// <summary>
    /// Request model for YAML character profiles
    /// </summary>
    public class YamlCharacterRequest
    {
        public Guid UserId { get; set; }
        public string YamlProfile { get; set; }
    }

    /// <summary>
    /// Request model for setting character default (Character Controller specific)
    /// </summary>
    public class SetCharacterDefaultRequest
    {
        public Guid UserId { get; set; }
        public Guid CharacterId { get; set; }
    }

    /// <summary>
    /// Character card data structure matching the standard format
    /// </summary>
    public class CharacterCard
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Personality { get; set; }
        public string Scenario { get; set; }
        public string FirstMessage { get; set; }
        public string MessageExample { get; set; }
        public string CreatorComment { get; set; }
        public string Avatar { get; set; }
        public string Chat { get; set; }
        public string Talkativeness { get; set; }
        public bool Fav { get; set; }
        public string[] Tags { get; set; }
        public string Spec { get; set; }
        public string SpecVersion { get; set; }
        public CharacterCardData Data { get; set; }
        public string CreateDate { get; set; }
    }

    /// <summary>
    /// Extended character card data
    /// </summary>
    public class CharacterCardData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Personality { get; set; }
        public string Scenario { get; set; }
        public string FirstMessage { get; set; }
        public string MessageExample { get; set; }
        public string SystemPrompt { get; set; }
        public string PostHistoryInstructions { get; set; }
        public string[] AlternateGreetings { get; set; }
        public string[] Tags { get; set; }
        public string Creator { get; set; }
        public string CreatorNotes { get; set; }
        public string CharacterVersion { get; set; }
        public string[] GroupOnlyGreetings { get; set; }
        public object Extensions { get; set; }
    }
}
