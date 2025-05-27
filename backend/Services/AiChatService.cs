using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using YamlDotNet.Serialization;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for AI chat service
    /// </summary>
    public interface IAiChatService
    {
        /// <summary>
        /// Generates an AI response to a user message and stores both in the conversation
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="userMessage">User message</param>
        /// <returns>The AI response</returns>
        Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage);

        /// <summary>
        /// Gets the current LLM engine and model for a user
        /// </summary>
        /// <param name="userId">User ID (optional)</param>
        /// <returns>Dictionary with engine and model</returns>
        Task<Dictionary<string, string>> GetCurrentLlmSettingsAsync(Guid? userId = null);

        /// <summary>
        /// Sets the default LLM engine and model for a user
        /// </summary>
        /// <param name="userId">User ID (optional)</param>
        /// <param name="engine">LLM engine (ollama or lmstudio)</param>
        /// <param name="model">LLM model name (for Ollama)</param>
        /// <returns>True if successful</returns>
        Task<bool> SetDefaultLlmSettingsAsync(Guid? userId, string engine, string model = null);
    }

    /// <summary>
    /// Service for AI chat functionality
    /// </summary>
    public class AiChatService : IAiChatService
    {
        private readonly ILlmConnectorService _llmConnector;
        private readonly IConversationService _conversationService;
        private readonly ISettingsService _settingsService;
        private readonly ISimpleLoggerService _logger;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _dbContext;
        private readonly IDefaultCharacterService _defaultCharacterService;
        private readonly IBrainService _brainService;

        // Setting keys
        private const string DEFAULT_LLM_ENGINE_KEY = "DefaultLlmEngine";
        private const string DEFAULT_LLM_MODEL_KEY = "DefaultLlmModel";

        /// <summary>
        /// Initializes a new instance of the AiChatService
        /// </summary>
        /// <param name="llmConnector">LLM connector service</param>
        /// <param name="conversationService">Conversation service</param>
        /// <param name="settingsService">Settings service</param>
        /// <param name="logger">Logger service</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="dbContext">Database context</param>
        /// <param name="defaultCharacterService">Default character service</param>
        /// <param name="brainService">Brain service for memory search</param>
        public AiChatService(
            ILlmConnectorService llmConnector,
            IConversationService conversationService,
            ISettingsService settingsService,
            ISimpleLoggerService logger,
            IConfiguration configuration,
            ApplicationDbContext dbContext,
            IDefaultCharacterService defaultCharacterService,
            IBrainService brainService)
        {
            _llmConnector = llmConnector;
            _conversationService = conversationService;
            _settingsService = settingsService;
            _logger = logger;
            _configuration = configuration;
            _dbContext = dbContext;
            _defaultCharacterService = defaultCharacterService;
            _brainService = brainService;
        }

        /// <inheritdoc/>
        public async Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage)
        {
            _logger.LogInfo("🚀 AiChatService: GenerateAndStoreResponseAsync called");
            _logger.LogInfo($"🚀 ConversationId={conversationId}, UserId={userId}, Message='{userMessage}'");

            try
            {
                // Store the user message
                await _conversationService.AppendMessageAsync(conversationId, userId, "user", userMessage);
                _logger.LogInfo("✅ User message stored successfully");

                // Get the current LLM settings
                var settings = await GetCurrentLlmSettingsAsync(userId);
                string engine = settings["engine"];
                string model = settings["model"];
                _logger.LogInfo($"🚀 LLM settings - Engine: {engine}, Model: {model}");

                // Get conversation to check for character context
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

                string systemPrompt = null;

                // Check if conversation has a specific character assigned
                if (conversation != null && !string.IsNullOrEmpty(conversation.CharacterSystemPrompt))
                {
                    _logger.LogInfo("🚀 Using character from conversation context");
                    systemPrompt = conversation.CharacterSystemPrompt;
                    _logger.LogInfo($"✅ Character system prompt retrieved from conversation - Length: {systemPrompt.Length}");
                }
                else
                {
                    // Fall back to default GLaDOS character
                    _logger.LogInfo("🚀 No character in conversation, falling back to GLaDOS default");
                    systemPrompt = await _defaultCharacterService.GetDefaultSystemPromptAsync();
                    _logger.LogInfo($"✅ GLaDOS default system prompt retrieved - Length: {systemPrompt?.Length ?? 0}");
                }

                // Prepare structured messages for the LLM
                var messages = new List<Dictionary<string, string>>();

                // Add system prompt (character personality)
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    messages.Add(new Dictionary<string, string>
                    {
                        { "role", "system" },
                        { "content", systemPrompt }
                    });
                    _logger.LogInfo("✅ Using character system prompt for response");
                }
                else
                {
                    _logger.LogWarning("⚠️ No system prompt available - using no character context");
                }

                // Search for relevant memories
                string memoryContext = "";
                try
                {
                    _logger.LogInfo("🧠 Searching for relevant memories...");
                    var memoryHits = await _brainService.SearchAsync(userMessage, limit: 5);

                    if (memoryHits.Any())
                    {
                        _logger.LogInfo($"✅ Found {memoryHits.Count} relevant memories");
                        var memoryTexts = new List<string>();

                        foreach (var hit in memoryHits)
                        {
                            if (hit.Metadata != null && hit.Metadata.ContainsKey("userId"))
                            {
                                // Only include memories from the same user
                                if (hit.Metadata["userId"] == userId.ToString())
                                {
                                    // Get the actual memory content from the database using the ID
                                    var memory = await _dbContext.Memories.FindAsync(hit.Id);
                                    if (memory != null)
                                    {
                                        memoryTexts.Add($"- {memory.Content}");
                                    }
                                }
                            }
                        }

                        if (memoryTexts.Any())
                        {
                            memoryContext = $"\n\nRelevant memories from previous conversations:\n{string.Join("\n", memoryTexts)}\n\nUse this information to provide more informed and personalized responses.";
                            _logger.LogInfo($"✅ Including {memoryTexts.Count} user memories in context");
                        }
                    }
                    else
                    {
                        _logger.LogInfo("ℹ️ No relevant memories found");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to search memories: {ex.Message}");
                    // Continue without memories - don't fail the entire request
                }

                // Add user message with memory context
                var userContent = userMessage + memoryContext;
                messages.Add(new Dictionary<string, string>
                {
                    { "role", "user" },
                    { "content", userContent }
                });

                _logger.LogInfo($"📤 Sending {messages.Count} structured messages to LLM");

                // Generate the AI response using structured messages
                string aiResponse = await _llmConnector.GenerateResponseAsync(messages, engine, model, userId);
                _logger.LogInfo($"✅ AI response generated - Length: {aiResponse?.Length ?? 0}");

                // Store the AI response
                await _conversationService.AppendMessageAsync(conversationId, userId, "assistant", aiResponse);
                _logger.LogInfo("✅ AI response stored successfully");

                return aiResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error generating AI response: {ex.Message}");
                _logger.LogError($"🚨 StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> GetCurrentLlmSettingsAsync(Guid? userId = null)
        {
            try
            {
                // Get the default engine from settings or configuration
                string defaultEngine = await _settingsService.GetSettingAsync(
                    userId,
                    DEFAULT_LLM_ENGINE_KEY,
                    _configuration["AppSettings:DefaultLlmEngine"] ?? "ollama");

                // Get the default model from settings or configuration
                string defaultModel = await _settingsService.GetSettingAsync(
                    userId,
                    DEFAULT_LLM_MODEL_KEY,
                    _configuration["AppSettings:DefaultLlmModel"]);

                return new Dictionary<string, string>
                {
                    { "engine", defaultEngine },
                    { "model", defaultModel }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting LLM settings", ex);

                // Fallback to defaults
                return new Dictionary<string, string>
                {
                    { "engine", "ollama" },
                    { "model", null }
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetDefaultLlmSettingsAsync(Guid? userId, string engine, string model = null)
        {
            try
            {
                // Validate engine
                if (engine != "ollama" && engine != "lmstudio")
                {
                    throw new ArgumentException("Invalid engine. Must be 'ollama' or 'lmstudio'.");
                }

                // Set the default engine
                await _settingsService.SetSettingAsync(userId, DEFAULT_LLM_ENGINE_KEY, engine);

                // Set the default model (if provided)
                if (!string.IsNullOrEmpty(model))
                {
                    await _settingsService.SetSettingAsync(userId, DEFAULT_LLM_MODEL_KEY, model);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error setting LLM settings", ex);
                return false;
            }
        }

        /// <summary>
        /// Updates character's SystemPrompt from YamlProfile
        /// </summary>
        public async Task<bool> UpdateCharacterSystemPromptAsync(Guid characterId)
        {
            try
            {
                var character = await _dbContext.Avatars.FindAsync(characterId);
                if (character == null || string.IsNullOrEmpty(character.YamlProfile))
                    return false;

                var deserializer = new DeserializerBuilder().Build();
                var yaml = deserializer.Deserialize<dynamic>(character.YamlProfile);

                character.SystemPrompt = ConvertYamlToPrompt(yaml);
                character.LastModified = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating character system prompt: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts YAML character profile to system prompt
        /// </summary>
        private static string ConvertYamlToPrompt(dynamic yaml)
        {
            try
            {
                return $@"
You are roleplaying as the AI character below. Remain in character at all times.

Name: {yaml["name"]}
Description: {yaml["description"]}
Personality: {yaml["personality"]}
Scenario: {yaml["scenario"]}
Talkativeness Level: {yaml["talkativeness"]}

Start conversations with:
{yaml["first_message"]}

Example conversation:
{yaml["message_example"]}
".Trim();
            }
            catch (Exception)
            {
                return "You are a helpful AI assistant.";
            }
        }
    }
}
