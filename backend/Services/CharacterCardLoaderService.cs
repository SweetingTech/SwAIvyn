using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using YamlDotNet.Serialization;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Service for loading character cards from the filesystem and storing them in the database
    /// </summary>
    public class CharacterCardLoaderService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CharacterCardLoaderService> _logger;
        private readonly IDeserializer _yamlDeserializer;
        private readonly string _characterCardsPath;

        public CharacterCardLoaderService(
            ApplicationDbContext dbContext,
            ILogger<CharacterCardLoaderService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _yamlDeserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            
            // Path to character cards directory
            _characterCardsPath = Path.Combine(Directory.GetCurrentDirectory(), "frontend", "AI");
        }

        /// <summary>
        /// Loads all character cards from the filesystem and stores them in the database
        /// </summary>
        public async Task LoadCharacterCardsAsync()
        {
            try
            {
                _logger.LogInformation("Starting character card loading from: {Path}", _characterCardsPath);

                if (!Directory.Exists(_characterCardsPath))
                {
                    _logger.LogWarning("Character cards directory not found: {Path}", _characterCardsPath);
                    return;
                }

                var characterDirectories = Directory.GetDirectories(_characterCardsPath);
                _logger.LogInformation("Found {Count} character directories", characterDirectories.Length);

                foreach (var characterDir in characterDirectories)
                {
                    await LoadCharacterFromDirectoryAsync(characterDir);
                }

                _logger.LogInformation("Character card loading completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading character cards");
            }
        }

        /// <summary>
        /// Loads a character from a specific directory
        /// </summary>
        private async Task LoadCharacterFromDirectoryAsync(string characterDir)
        {
            try
            {
                var characterName = Path.GetFileName(characterDir);
                _logger.LogInformation("Processing character directory: {CharacterName}", characterName);

                // Look for YAML files
                var yamlFiles = Directory.GetFiles(characterDir, "*.yaml")
                    .Concat(Directory.GetFiles(characterDir, "*.yml"))
                    .ToArray();

                if (yamlFiles.Length == 0)
                {
                    _logger.LogWarning("No YAML files found in {Directory}", characterDir);
                    return;
                }

                var yamlFile = yamlFiles.First();
                _logger.LogInformation("Found YAML file: {YamlFile}", yamlFile);

                // Read and parse YAML
                var yamlContent = await File.ReadAllTextAsync(yamlFile);
                var characterData = _yamlDeserializer.Deserialize<CharacterCardData>(yamlContent);

                if (characterData == null)
                {
                    _logger.LogWarning("Failed to parse YAML file: {YamlFile}", yamlFile);
                    return;
                }

                // Look for image files
                var imageFiles = Directory.GetFiles(characterDir, "*.jpg")
                    .Concat(Directory.GetFiles(characterDir, "*.jpeg"))
                    .Concat(Directory.GetFiles(characterDir, "*.png"))
                    .Concat(Directory.GetFiles(characterDir, "*.gif"))
                    .ToArray();

                string imagePath = null;
                if (imageFiles.Length > 0)
                {
                    imagePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), imageFiles.First());
                    _logger.LogInformation("Found image file: {ImageFile}", imagePath);
                }

                // Get default user ID
                var defaultUser = await _dbContext.Users.FirstOrDefaultAsync();
                if (defaultUser == null)
                {
                    _logger.LogWarning("No default user found, skipping character: {CharacterName}", characterName);
                    return;
                }

                // Check if character already exists
                var existingCharacter = await _dbContext.Avatars
                    .FirstOrDefaultAsync(a => a.Name == characterData.Name && a.UserId == defaultUser.Id);

                if (existingCharacter != null)
                {
                    _logger.LogInformation("Character {CharacterName} already exists, updating...", characterData.Name);
                    await UpdateExistingCharacterAsync(existingCharacter, characterData, yamlContent, imagePath);
                }
                else
                {
                    _logger.LogInformation("Creating new character: {CharacterName}", characterData.Name);
                    await CreateNewCharacterAsync(defaultUser.Id, characterData, yamlContent, imagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing character directory: {Directory}", characterDir);
            }
        }

        /// <summary>
        /// Creates a new character in the database
        /// </summary>
        private async Task CreateNewCharacterAsync(Guid userId, CharacterCardData characterData, string yamlContent, string imagePath)
        {
            var character = new AvatarInfo
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = characterData.Name ?? "Unknown Character",
                Description = characterData.Description ?? "",
                Personality = characterData.Personality ?? "",
                ImagePath = imagePath ?? "default-avatar.png",
                YamlProfile = yamlContent,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                IsFavorite = characterData.Favorite
            };

            // Generate system prompt from character data
            character.SystemPrompt = GenerateSystemPrompt(characterData);

            _dbContext.Avatars.Add(character);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created character: {CharacterName} with ID: {CharacterId}", character.Name, character.Id);
        }

        /// <summary>
        /// Updates an existing character in the database
        /// </summary>
        private async Task UpdateExistingCharacterAsync(AvatarInfo existingCharacter, CharacterCardData characterData, string yamlContent, string imagePath)
        {
            existingCharacter.Name = characterData.Name ?? existingCharacter.Name;
            existingCharacter.Description = characterData.Description ?? existingCharacter.Description;
            existingCharacter.Personality = characterData.Personality ?? existingCharacter.Personality;
            existingCharacter.YamlProfile = yamlContent;
            existingCharacter.LastModified = DateTime.UtcNow;
            existingCharacter.IsFavorite = characterData.Favorite;

            if (!string.IsNullOrEmpty(imagePath))
            {
                existingCharacter.ImagePath = imagePath;
            }

            // Update system prompt
            existingCharacter.SystemPrompt = GenerateSystemPrompt(characterData);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated character: {CharacterName}", existingCharacter.Name);
        }

        /// <summary>
        /// Generates a system prompt from character data
        /// </summary>
        private string GenerateSystemPrompt(CharacterCardData characterData)
        {
            var prompt = $"You are roleplaying as the AI character below. Remain fully in character at all times.\n\n";
            
            if (!string.IsNullOrEmpty(characterData.Name))
                prompt += $"Name: {characterData.Name}\n";
            
            if (!string.IsNullOrEmpty(characterData.Description))
                prompt += $"Description: {characterData.Description}\n";
            
            if (!string.IsNullOrEmpty(characterData.Personality))
                prompt += $"Personality: {characterData.Personality}\n";
            
            if (!string.IsNullOrEmpty(characterData.Scenario))
                prompt += $"Scenario: {characterData.Scenario}\n";
            
            if (!string.IsNullOrEmpty(characterData.CreatorNotes))
                prompt += $"Creator Notes: {characterData.CreatorNotes}\n";

            prompt += "\nRespond using the character's voice and personality at all times. Stay in character and never break the roleplay.";

            return prompt;
        }
    }

    /// <summary>
    /// Data structure for parsing YAML character cards
    /// </summary>
    public class CharacterCardData
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Personality { get; set; } = "";
        public string Scenario { get; set; } = "";
        public string FirstMessage { get; set; } = "";
        public string MessageExample { get; set; } = "";
        public string CreatorNotes { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public bool Favorite { get; set; } = false;
        public float Talkativeness { get; set; } = 0.5f;
        public string CharacterVersion { get; set; } = "1.0";
        public string Avatar { get; set; } = "";
        public string Chat { get; set; } = "";
        public string Spec { get; set; } = "";
        public string SpecVersion { get; set; } = "";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Extensions { get; set; } = new();
    }
}
