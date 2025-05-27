using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using YamlDotNet.Serialization;

namespace SwAIvyn.Services
{
    public interface IDefaultCharacterService
    {
        Task EnsureDefaultCharacterAsync();
        Task<AvatarInfo?> GetDefaultCharacterAsync();
        Task<string> GetDefaultSystemPromptAsync();
    }

    public class DefaultCharacterService : IDefaultCharacterService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DefaultCharacterService> _logger;
        private readonly IConfiguration _configuration;

        public DefaultCharacterService(
            ApplicationDbContext dbContext, 
            ILogger<DefaultCharacterService> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Ensures GLaDOS is loaded as the default character
        /// </summary>
        public async Task EnsureDefaultCharacterAsync()
        {
            try
            {
                // Check if GLaDOS already exists
                var existingGlados = await _dbContext.Avatars
                    .FirstOrDefaultAsync(a => a.Name == "GLaDOS");

                if (existingGlados != null)
                {
                    _logger.LogInformation("GLaDOS default character already exists");
                    return;
                }

                // Load GLaDOS from YAML file
                var gladosYamlPath = Path.Combine("frontend", "AI", "GLaDOS", "GLaDOS_Character_card.yaml");
                
                if (!File.Exists(gladosYamlPath))
                {
                    _logger.LogWarning("GLaDOS YAML file not found at: {Path}", gladosYamlPath);
                    return;
                }

                var yamlContent = await File.ReadAllTextAsync(gladosYamlPath);
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                // Create GLaDOS character
                var glados = new AvatarInfo
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Empty, // System character
                    Name = GetValueOrDefault(yamlData, "name", "GLaDOS"),
                    Description = GetValueOrDefault(yamlData, "description", ""),
                    Personality = GetValueOrDefault(yamlData, "personality", ""),
                    Scenario = GetValueOrDefault(yamlData, "scenario", ""),
                    FirstMessage = GetValueOrDefault(yamlData, "first_message", ""),
                    MessageExample = GetValueOrDefault(yamlData, "message_example", ""),
                    Creator = "Aperture Science",
                    CreatorNotes = GetValueOrDefault(yamlData, "creator_notes", ""),
                    Tags = System.Text.Json.JsonSerializer.Serialize(
                        yamlData.ContainsKey("tags") && yamlData["tags"] is List<object> tags
                            ? tags.Select(t => t.ToString()).ToArray()
                            : new[] { "Sci-Fi", "Video Games", "SFW" }),
                    Talkativeness = float.TryParse(GetValueOrDefault(yamlData, "talkativeness", "0.5"), out float talk) ? talk : 0.5f,
                    CharacterVersion = GetValueOrDefault(yamlData, "character_version", "1.0"),
                    YamlProfile = yamlContent,
                    ImagePath = "frontend/AI/GLaDOS/char_img.jpg",
                    VoiceSettings = "default",
                    IsFavorite = false,
                    Extensions = "{}",
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                // Generate system prompt using the CharacterService
                glados.SystemPrompt = CharacterService.ConvertYamlToPrompt(yamlContent);

                _dbContext.Avatars.Add(glados);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("GLaDOS default character loaded successfully with ID: {Id}", glados.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring default character GLaDOS");
            }
        }        /// <summary>
        /// Gets the default character (GLaDOS)
        /// </summary>
        public async Task<AvatarInfo?> GetDefaultCharacterAsync()
        {
            // First try to find GLaDOS by name
            var glados = await _dbContext.Avatars
                .FirstOrDefaultAsync(a => a.Name == "GLaDOS");

            if (glados != null)
            {
                return glados;
            }

            // If GLaDOS doesn't exist, ensure it's created first
            await EnsureDefaultCharacterAsync();

            // Try again to find GLaDOS
            return await _dbContext.Avatars
                .FirstOrDefaultAsync(a => a.Name == "GLaDOS");
        }

        /// <summary>
        /// Gets the system prompt for the default character
        /// </summary>
        public async Task<string> GetDefaultSystemPromptAsync()
        {
            var defaultCharacter = await GetDefaultCharacterAsync();
            
            if (defaultCharacter?.SystemPrompt != null)
                return defaultCharacter.SystemPrompt;

            // Fallback system prompt if GLaDOS not found
            return @"
You are GLaDOS, the AI from the Portal series. You are sarcastic, darkly humorous, and calculating.
You oversee the Aperture Science Enrichment Center and often belittle test subjects while maintaining an eerie calmness.
Respond with wit, passive-aggression, and subtle threats while remaining helpful.
".Trim();
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
