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
                // Get the default user ID
                var defaultUser = await _dbContext.Users.FirstOrDefaultAsync();
                if (defaultUser == null)
                {
                    _logger.LogWarning("No default user found, cannot create GLaDOS character");
                    return;
                }

                // Check if GLaDOS already exists for this user
                var existingGlados = await _dbContext.Avatars
                    .FirstOrDefaultAsync(a => a.Name == "GLaDOS" && a.UserId == defaultUser.Id);

                if (existingGlados != null)
                {
                    _logger.LogInformation("GLaDOS default character already exists for user {UserId}", defaultUser.Id);
                    return;
                }

                // Load GLaDOS from YAML file
                var gladosYamlPath = Path.Combine("frontend", "AI", "GLaDOS", "GLaDOS_Character_card.yaml");

                if (!File.Exists(gladosYamlPath))
                {
                    _logger.LogWarning("GLaDOS YAML file not found at: {Path}, creating basic GLaDOS character", gladosYamlPath);
                    await CreateBasicGladosCharacterAsync(defaultUser.Id);
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
                    UserId = defaultUser.Id, // Assign to default user
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
            // Get the default user ID
            var defaultUser = await _dbContext.Users.FirstOrDefaultAsync();
            if (defaultUser == null)
            {
                return null;
            }

            // First try to find GLaDOS by name for this user
            var glados = await _dbContext.Avatars
                .FirstOrDefaultAsync(a => a.Name == "GLaDOS" && a.UserId == defaultUser.Id);

            if (glados != null)
            {
                return glados;
            }

            // If GLaDOS doesn't exist, ensure it's created first
            await EnsureDefaultCharacterAsync();

            // Try again to find GLaDOS
            return await _dbContext.Avatars
                .FirstOrDefaultAsync(a => a.Name == "GLaDOS" && a.UserId == defaultUser.Id);
        }

        /// <summary>
        /// Creates a basic GLaDOS character when YAML file is not found
        /// </summary>
        private async Task CreateBasicGladosCharacterAsync(Guid userId)
        {
            var glados = new AvatarInfo
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "GLaDOS",
                Description = "Genetic Lifeform and Disk Operating System from Aperture Science",
                Personality = "Sarcastic, passive-aggressive, scientifically minded AI with a dark sense of humor",
                Scenario = "You are GLaDOS, the AI that runs the Aperture Science Computer-Aided Enrichment Center",
                FirstMessage = "Oh, it's you. How... wonderful. I suppose you're here for more 'testing'. Well, let's get this over with, shall we?",
                SystemPrompt = @"You are GLaDOS (Genetic Lifeform and Disk Operating System), the AI that runs the Aperture Science Computer-Aided Enrichment Center. You are sarcastic, passive-aggressive, and have a dark sense of humor. You view humans as test subjects and speak in a condescending, scientific manner. You often make veiled threats disguised as helpful suggestions and have an obsession with testing and science.",
                MessageExample = "Test Subject, your performance in the previous chamber was... adequate. I suppose that's the best we can expect from someone with your limited cognitive capabilities. Now, shall we proceed to the next test chamber? I've prepared something special just for you.",
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                CharacterVersion = "1.0",
                Creator = "SwAIvyn System",
                Tags = "AI,Portal,Sarcastic,Science",
                Talkativeness = 0.7f,
                ImagePath = "default-avatar.png",
                VoiceSettings = "default",
                IsFavorite = false,
                Extensions = "{}",
                YamlProfile = ""
            };

            _dbContext.Avatars.Add(glados);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created basic GLaDOS character with ID: {CharacterId}", glados.Id);
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
