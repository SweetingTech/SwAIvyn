using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Services.Graph;
using SwAIvyn.Services.VectorStore;
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
        Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage);

        /// <summary>
        /// Generates an AI response to a user message and stores both in the conversation, with optional auto-memory
        /// </summary>
        Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage, bool saveMemory, string memoryCategory = "conversation", string engineOverride = null, string modelOverride = null);

        /// <summary>
        /// Gets the current LLM engine and model for a user
        /// </summary>
        Task<Dictionary<string, string>> GetCurrentLlmSettingsAsync(Guid? userId = null);

        /// <summary>
        /// Sets the default LLM engine and model for a user
        /// </summary>
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
        private readonly IBrainGraphService _brainGraphService;
        private readonly IVectorRouter _vectorRouter;
        private readonly IHybridSearchService _hybridSearchService;

        // Setting keys
        private const string DEFAULT_LLM_ENGINE_KEY = "DefaultLlmEngine";
        private const string DEFAULT_LLM_MODEL_KEY = "DefaultLlmModel";

        /// <summary>
        /// Initializes a new instance of the AiChatService
        /// </summary>
        public AiChatService(
            ILlmConnectorService llmConnector,
            IConversationService conversationService,
            ISettingsService settingsService,
            ISimpleLoggerService logger,
            IConfiguration configuration,
            ApplicationDbContext dbContext,
            IBrainGraphService brainGraphService,
            IVectorRouter vectorRouter,
            IHybridSearchService hybridSearchService)
        {
            _llmConnector = llmConnector;
            _conversationService = conversationService;
            _settingsService = settingsService;
            _logger = logger;
            _configuration = configuration;
            _dbContext = dbContext;
            _brainGraphService = brainGraphService;
            _vectorRouter = vectorRouter;
            _hybridSearchService = hybridSearchService;
        }

        /// <inheritdoc/>
        public async Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage)
        {
            return await GenerateAndStoreResponseAsync(conversationId, userId, userMessage, false, "conversation", null, null);
        }

        /// <inheritdoc/>
        public async Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage, bool saveMemory, string memoryCategory = "conversation", string engineOverride = null, string modelOverride = null)
        {
            _logger.LogInfo("🚀 AiChatService: GenerateAndStoreResponseAsync called");
            _logger.LogInfo($"🚀 ConversationId={conversationId}, UserId={userId}, Message='{userMessage}', SaveMemory={saveMemory}, EngineOverride='{engineOverride}', ModelOverride='{modelOverride}'");

            try
            {
                // 1. Store the user message
                await _conversationService.AppendMessageAsync(conversationId, userId, "user", userMessage);
                _logger.LogInfo("✅ User message stored successfully");

                // 2. Determine LLM engine and model
                string engine;
                string model;

                if (!string.IsNullOrEmpty(engineOverride))
                {
                    _logger.LogInfo($"🚀 Using LLM override - Engine: {engineOverride}, Model: {modelOverride ?? "default"}");
                    engine = engineOverride;
                    model = modelOverride; // Can be null if engine doesn't require specific model (e.g. LMStudio) or to use engine's default
                }
                else
                {
                    var defaultSettings = await GetCurrentLlmSettingsAsync(userId);
                    engine = defaultSettings["engine"];
                    model = defaultSettings["model"];
                    _logger.LogInfo($"🚀 Using default LLM settings - Engine: {engine}, Model: {model}");
                }

                // 3. Determine system prompt from conversation or fallback character
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

                string systemPrompt = null;
                if (conversation != null && !string.IsNullOrEmpty(conversation.CharacterSystemPrompt))
                {
                    _logger.LogInfo("🚀 Using character from conversation context");
                    systemPrompt = conversation.CharacterSystemPrompt;
                    _logger.LogInfo($"✅ Character system prompt length: {systemPrompt.Length}");
                }
                else
                {
                    _logger.LogInfo("🚀 No character in conversation, falling back to first available character");
                    var firstCharacter = await _dbContext.Avatars
                        .Where(a => a.UserId == userId)
                        .OrderBy(a => a.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (firstCharacter != null)
                    {
                        systemPrompt = firstCharacter.SystemPrompt;
                        if (string.IsNullOrEmpty(systemPrompt))
                        {
                            systemPrompt = $"You are roleplaying as {firstCharacter.Name}. {firstCharacter.Personality} {firstCharacter.Description}";
                        }
                        _logger.LogInfo($"✅ Fallback character '{firstCharacter.Name}' system prompt length: {systemPrompt.Length}");
                    }
                    else
                    {
                        _logger.LogInfo("⚠️ No characters available for user, using generic assistant prompt");
                        systemPrompt = "You are a helpful AI assistant.";
                    }
                }

                // 4. Build structured messages
                var messages = new List<Dictionary<string, string>>();
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    messages.Add(new Dictionary<string, string>
                    {
                        { "role", "system" },
                        { "content", systemPrompt }
                    });
                    _logger.LogInfo("✅ Added system prompt to messages");
                }
                else
                {
                    _logger.LogWarning("⚠️ No system prompt provided");
                }

                // 5. Perform hybrid search for context
                string contextualInformation = "";
                try
                {
                    _logger.LogInfo("🔍 Searching for relevant context using hybrid search service...");
                    var searchResults = await _hybridSearchService.SearchAsync(userMessage, userId, topK: 10);

                    if (searchResults.Any())
                    {
                        _logger.LogInfo($"✅ Found {searchResults.Count} relevant results from hybrid search");

                        var memoryTexts = new List<string>();
                        var conversationTexts = new List<string>();
                        var documentTexts = new List<string>();

                        foreach (var result in searchResults)
                        {
                            switch (result.Source.ToLower())
                            {
                                case "neo4j":
                                    if (result.Metadata.ContainsKey("entity_type") ||
                                        result.Metadata.ContainsKey("category") ||
                                        result.Metadata.ContainsKey("source"))
                                    {
                                        memoryTexts.Add($"- {result.Content}");
                                    }
                                    break;

                                case "weaviate":
                                    if (result.Metadata.TryGetValue("search_type", out var st) &&
                                        st.ToString() == "conversation")
                                    {
                                        conversationTexts.Add($"- {result.Content}");
                                    }
                                    else
                                    {
                                        documentTexts.Add($"- {result.Content}");
                                    }
                                    break;

                                case "sqlite":
                                    conversationTexts.Add($"- {result.Content}");
                                    break;
                            }
                        }

                        if (memoryTexts.Any())
                        {
                            contextualInformation += $"\n\nRelevant memories:\n{string.Join("\n", memoryTexts)}";
                            _logger.LogInfo($"✅ Included {memoryTexts.Count} memories in context");
                        }

                        if (conversationTexts.Any())
                        {
                            contextualInformation += $"\n\nRelevant past conversations:\n{string.Join("\n", conversationTexts)}";
                            _logger.LogInfo($"✅ Included {conversationTexts.Count} conversations in context");
                        }

                        if (documentTexts.Any())
                        {
                            contextualInformation += $"\n\nRelevant information from documents:\n{string.Join("\n", documentTexts)}";
                            _logger.LogInfo($"✅ Included {documentTexts.Count} documents in context");
                        }
                    }
                    else
                    {
                        _logger.LogInfo("ℹ️ No relevant context found from hybrid search");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Hybrid search service failed: {ex.Message}");
                }

                if (!string.IsNullOrEmpty(contextualInformation))
                {
                    contextualInformation += "\n\nUse this information to provide more informed and personalized responses.";
                }

                // 6. Add user message with contextual info
                var userContent = userMessage + contextualInformation;
                messages.Add(new Dictionary<string, string>
                {
                    { "role", "user" },
                    { "content", userContent }
                });

                _logger.LogInfo($"📤 Sending {messages.Count} structured messages to LLM");

                // 7. Generate AI response
                string aiResponse = await _llmConnector.GenerateResponseAsync(messages, engine, model, userId);
                _logger.LogInfo($"✅ AI response generated (length: {aiResponse?.Length ?? 0})");

                // 8. Store AI response
                await _conversationService.AppendMessageAsync(conversationId, userId, "assistant", aiResponse);
                _logger.LogInfo("✅ AI response stored successfully");

                // 9. Process auto-memory
                await ProcessAutoMemoryAsync(userMessage, aiResponse, userId, saveMemory, memoryCategory);

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
                string defaultEngine = await _settingsService.GetSettingAsync(
                    userId,
                    DEFAULT_LLM_ENGINE_KEY,
                    _configuration["AppSettings:DefaultLlmEngine"] ?? "ollama");

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
                if (engine != "ollama" && engine != "lmstudio" && engine != "openai" && engine != "claude")
                {
                    throw new ArgumentException("Invalid engine. Must be 'ollama', 'lmstudio', 'openai' or 'claude'.");
                }

                await _settingsService.SetSettingAsync(userId, DEFAULT_LLM_ENGINE_KEY, engine);

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
        /// Processes memory storage for chat messages - only stores memories when explicitly requested
        /// </summary>
        private async Task ProcessAutoMemoryAsync(string userMessage, string aiResponse, Guid userId, bool saveMemory, string memoryCategory)
        {
            try
            {
                var autoMemoryEnabled = await _settingsService.GetSettingAsync(userId, "AutoMemoryEnabled", "false");
                if (autoMemoryEnabled.ToLowerInvariant() != "true" && !saveMemory)
                {
                    _logger.LogInfo("🧠 Auto-memory disabled for user, skipping auto-detection");
                    return;
                }

                string memoryContent = null;
                string actualCategory = memoryCategory ?? "conversation";

                if (userMessage.StartsWith("remember:", StringComparison.OrdinalIgnoreCase))
                {
                    memoryContent = userMessage.Substring(9).Trim();
                    actualCategory = "explicit";
                    _logger.LogInfo($"🧠 Explicit memory trigger detected: '{memoryContent}'");
                }
                else if (saveMemory)
                {
                    memoryContent = $"User: {userMessage}\nAI: {aiResponse}";
                    _logger.LogInfo($"🧠 Flag-based memory save requested for category: {actualCategory}");
                }
                else
                {
                    _logger.LogInfo("🧠 No explicit memory request detected - skipping memory storage");
                    return;
                }

                if (!string.IsNullOrEmpty(memoryContent))
                {
                    var memoryId = Guid.NewGuid();
                    var metadata = new Dictionary<string, string>
                    {
                        { "userId", userId.ToString() },
                        { "category", actualCategory },
                        { "timestamp", DateTime.UtcNow.ToString("O") },
                        { "source", "auto-chat" }
                    };

                    var memoryItem = new Data.Entities.MemoryItem
                    {
                        Id = memoryId,
                        UserId = userId,
                        Content = memoryContent,
                        Category = actualCategory,
                        IsShared = false,
                        CreatedAt = DateTime.UtcNow,
                        LastAccessed = DateTime.UtcNow
                    };

                    _dbContext.Memories.Add(memoryItem);
                    await _dbContext.SaveChangesAsync();

                    var success = await _brainGraphService.AddMemoryAsync(memoryId, memoryContent, metadata);

                    if (success)
                    {
                        _logger.LogInfo($"✅ Auto-memory saved successfully - ID: {memoryId}, Category: {actualCategory}");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ Failed to save memory to vector store - ID: {memoryId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error processing auto-memory: {ex.Message}", ex);
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
