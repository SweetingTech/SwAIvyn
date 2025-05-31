using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Services.Interfaces;
using SwAIvyn.Data.Entities;
using SwAIvyn.Enums;
using SwAIvyn.Services.Graph;
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
        /// Generates an AI response to a user message and stores both in the conversation, with optional auto-memory
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="userMessage">User message</param>
        /// <param name="saveMemory">Whether to automatically save this conversation as a memory</param>
        /// <param name="memoryCategory">Category for auto-saved memories</param>
        /// <returns>The AI response</returns>
        Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage, bool saveMemory, string memoryCategory = "conversation");

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
    {        private readonly ILlmConnectorService _llmConnector;
        private readonly IConversationService _conversationService;
        private readonly ISettingsService _settingsService;
        private readonly ISimpleLoggerService _logger;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _dbContext;
        private readonly IMemoryService _memoryService;
        private readonly IBrainGraphService _brainGraphService;
        private readonly IHybridSearchService _hybridSearchService;

        // Setting keys
        private const string DEFAULT_LLM_ENGINE_KEY = "DefaultLlmEngine";
        private const string DEFAULT_LLM_MODEL_KEY = "DefaultLlmModel";        /// <summary>
        /// Initializes a new instance of the AiChatService
        /// </summary>
        /// <param name="llmConnector">LLM connector service</param>
        /// <param name="conversationService">Conversation service</param>
        /// <param name="settingsService">Settings service</param>
        /// <param name="logger">Logger service</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="dbContext">Database context</param>
        /// <param name="memoryService">Memory service for three-database harmony</param>
        /// <param name="brainGraphService">Brain graph service</param>
        /// <param name="hybridSearchService">Hybrid search service</param>
        public AiChatService(
            ILlmConnectorService llmConnector,
            IConversationService conversationService,
            ISettingsService settingsService,
            ISimpleLoggerService logger,
            IConfiguration configuration,
            ApplicationDbContext dbContext,
            IMemoryService memoryService,
            IBrainGraphService brainGraphService,
            IHybridSearchService hybridSearchService)
        {
            _llmConnector = llmConnector;
            _conversationService = conversationService;
            _settingsService = settingsService;
            _logger = logger;
            _configuration = configuration;
            _dbContext = dbContext;
            _memoryService = memoryService;
            _brainGraphService = brainGraphService;
            _hybridSearchService = hybridSearchService;
            _brainGraphService = brainGraphService;
        }

        /// <inheritdoc/>
        public async Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage)
        {
            return await GenerateAndStoreResponseAsync(conversationId, userId, userMessage, false, "conversation");
        }

        /// <inheritdoc/>
        public async Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage, bool saveMemory, string memoryCategory = "conversation")
        {
            _logger.LogInfo("🚀 AiChatService: GenerateAndStoreResponseAsync called");
            _logger.LogInfo($"🚀 ConversationId={conversationId}, UserId={userId}, Message='{userMessage}', SaveMemory={saveMemory}");

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
                    // Fall back to first available character from database
                    _logger.LogInfo("🚀 No character in conversation, falling back to first available character");
                    var firstCharacter = await _dbContext.Avatars
                        .Where(a => a.UserId == userId)
                        .OrderBy(a => a.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (firstCharacter != null)
                    {
                        systemPrompt = firstCharacter.SystemPrompt ?? "";
                        if (string.IsNullOrEmpty(systemPrompt))
                        {
                            // Generate a basic system prompt from character data
                            systemPrompt = $"You are roleplaying as {firstCharacter.Name}. {firstCharacter.Personality} {firstCharacter.Description}";
                        }
                        _logger.LogInfo($"✅ Fallback character '{firstCharacter.Name}' system prompt retrieved - Length: {systemPrompt.Length}");
                    }
                    else
                    {
                        _logger.LogInfo("⚠️ No characters available for user, using generic assistant prompt");
                        systemPrompt = "You are a helpful AI assistant.";
                    }
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
                }                // Search for relevant context from multiple sources using hybrid search
                string contextualInformation = "";
                
                try
                {
                    _logger.LogInfo("🔍 Searching for relevant context using hybrid search...");
                    
                    var hybridResults = await _hybridSearchService.SearchAsync(userId, userMessage, maxResults: 10);
                    
                    if (hybridResults.Any())
                    {
                        _logger.LogInfo($"✅ Hybrid search found {hybridResults.Count} total results");
                        
                        // Group results by type for better context organization
                        var memoryResults = hybridResults.Where(r => r.Source == "memory").Take(3).ToList();
                        var chatResults = hybridResults.Where(r => r.Source == "chat").Take(3).ToList();
                        var documentResults = hybridResults.Where(r => r.Source == "document").Take(3).ToList();
                        
                        // Add memory context
                        if (memoryResults.Any())
                        {
                            var memoryTexts = memoryResults.Select(r => $"- {r.Content}").ToList();
                            contextualInformation += $"\n\nRelevant memories from previous conversations:\n{string.Join("\n", memoryTexts)}";
                            _logger.LogInfo($"✅ Including {memoryTexts.Count} memories in context");
                        }
                        
                        // Add chat context
                        if (chatResults.Any())
                        {
                            var chatTexts = chatResults.Select(r => $"- {r.Content}").ToList();
                            contextualInformation += $"\n\nRelevant past conversations:\n{string.Join("\n", chatTexts)}";
                            _logger.LogInfo($"✅ Including {chatTexts.Count} conversation chunks in context");
                        }
                        
                        // Add document context
                        if (documentResults.Any())
                        {
                            var documentTexts = documentResults.Select(r => $"- {r.Content}").ToList();
                            contextualInformation += $"\n\nRelevant information from uploaded documents:\n{string.Join("\n", documentTexts)}";
                            _logger.LogInfo($"✅ Including {documentTexts.Count} document chunks in context");
                        }
                        
                        if (string.IsNullOrEmpty(contextualInformation))
                        {
                            _logger.LogInfo("ℹ️ No relevant context found in hybrid search results");
                        }
                    }
                    else
                    {
                        _logger.LogInfo("ℹ️ No relevant context found in hybrid search");
                        
                        // Fallback to individual searches if hybrid search returns no results
                        _logger.LogInfo("🔄 Falling back to individual database searches...");
                        contextualInformation = await FallbackContextSearchAsync(userId, userMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to perform hybrid search: {ex.Message}");
                    // Fallback to individual searches
                    _logger.LogInfo("🔄 Falling back to individual database searches...");
                    contextualInformation = await FallbackContextSearchAsync(userId, userMessage);
                }

                // Add instruction for using contextual information
                if (!string.IsNullOrEmpty(contextualInformation))
                {
                    contextualInformation += "\n\nUse this information to provide more informed and personalized responses.";
                }

                // Add user message with contextual information
                var userContent = userMessage + contextualInformation;
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

                // Auto-memory logic and conversation chunk storage
                await ProcessAutoMemoryAsync(userMessage, aiResponse, userId, saveMemory, memoryCategory);

                // Store conversation chunk for future retrieval
                await ProcessConversationChunkAsync(userMessage, aiResponse, userId, conversationId);

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
        }        /// <summary>
        /// Processes auto-memory logic for chat messages
        /// </summary>
        private async Task ProcessAutoMemoryAsync(string userMessage, string aiResponse, Guid userId, bool saveMemory, string memoryCategory)
        {
            try
            {
                // Check if auto-memory is enabled for this user
                var autoMemoryEnabled = await _settingsService.GetSettingAsync(userId, "AutoMemoryEnabled", "false");
                if (autoMemoryEnabled.ToLowerInvariant() != "true" && !saveMemory)
                {
                    _logger.LogInfo("🧠 Auto-memory disabled for user, skipping auto-detection");
                    return;
                }

                string memoryContent = null;
                string actualCategory = memoryCategory ?? "conversation";

                // 1. Check for explicit "remember:" keyword trigger
                if (userMessage.StartsWith("remember:", StringComparison.OrdinalIgnoreCase))
                {
                    memoryContent = userMessage.Substring(9).Trim(); // Remove "remember:" prefix
                    actualCategory = "explicit";
                    _logger.LogInfo($"🧠 Explicit memory trigger detected: '{memoryContent}'");
                }
                // 2. Check for explicit saveMemory flag
                else if (saveMemory)
                {
                    memoryContent = $"User: {userMessage}\nAI: {aiResponse}";
                    _logger.LogInfo($"🧠 Flag-based memory save requested for category: {actualCategory}");
                }
                // 3. Auto-detect memorable content (heuristic approach)
                else if (IsMemorableContent(userMessage, aiResponse))
                {
                    memoryContent = $"User: {userMessage}\nAI: {aiResponse}";
                    actualCategory = "auto-detected";
                    _logger.LogInfo($"🧠 Auto-detected memorable content");
                }                // Save memory if we have content to save
                if (!string.IsNullOrEmpty(memoryContent))
                {
                    // Use MemoryService facade for unified memory creation across all three databases
                    var memory = new MemoryItem
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Content = memoryContent,
                        Category = actualCategory,
                        IsShared = false,
                        CreatedAt = DateTime.UtcNow,
                        LastAccessed = DateTime.UtcNow,
                        TargetStore = VectorTarget.Neo4j
                    };
                    var (success, createdMemory) = await _memoryService.CreateMemoryAsync(memory);

                    if (success)
                    {
                        _logger.LogInfo($"✅ Auto-memory saved successfully through MemoryService - Category: {actualCategory}");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ Failed to save memory through MemoryService - Category: {actualCategory}");
                    }
                }
            }            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error processing auto-memory: {ex.Message}", ex);
                // Don't throw - auto-memory failure shouldn't break chat
            }
        }

        /// <summary>
        /// Determines if content is memorable using heuristics
        /// </summary>
        private static bool IsMemorableContent(string userMessage, string aiResponse)
        {
            // Make auto-memory detection much more selective to reduce noise
            
            // Only consider explicitly stated personal information
            var personalKeywords = new[]
            {
                "my name is", "remember that i", "remember that my", "don't forget that i", "don't forget that my",
                "important to remember", "please remember", "you should know that i", "you should know that my",
                "for future reference", "keep in mind that i", "keep in mind that my"
            };

            // Only consider explicitly requested memory storage
            var memoryRequestKeywords = new[]
            {
                "remember this", "save this", "store this", "memorize this", "note this down",
                "add to memory", "remember for later", "don't forget this"
            };

            var combinedText = (userMessage + " " + aiResponse).ToLowerInvariant();

            // Check for explicit personal information statements
            if (personalKeywords.Any(keyword => combinedText.Contains(keyword)))
            {
                return true;
            }

            // Check for explicit memory requests
            if (memoryRequestKeywords.Any(keyword => combinedText.Contains(keyword)))
            {
                return true;
            }

            // Only save very long, detailed responses to complex questions (indicating learning)
            if (userMessage.Contains("?") && aiResponse.Length > 500 && 
                (combinedText.Contains("explain") || combinedText.Contains("how to") || combinedText.Contains("what is") || combinedText.Contains("why")))
            {
                return true;
            }

            // Don't auto-save casual conversations, greetings, or simple questions
            return false;
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

        /// <summary>
        /// Processes conversation chunks for future retrieval
        /// </summary>
        private async Task ProcessConversationChunkAsync(string userMessage, string aiResponse, Guid userId, Guid conversationId)
        {
            try
            {
                // Create conversation chunk content
                var chunkContent = $"User: {userMessage}\nAI: {aiResponse}";

                // Only store meaningful conversation chunks (avoid very short exchanges)
                if (chunkContent.Length < 50)
                {
                    _logger.LogInfo("🔄 Skipping conversation chunk storage - too short");
                    return;
                }

                var chunkId = Guid.NewGuid();
                var metadata = new Dictionary<string, string>
                {
                    { "userId", userId.ToString() },
                    { "conversationId", conversationId.ToString() },
                    { "timestamp", DateTime.UtcNow.ToString("O") },
                    { "source", "chat-exchange" },
                    { "content", chunkContent }
                };

                // Store conversation chunk in Neo4j for future retrieval
                var success = await _brainGraphService.AddConversationChunkAsync(chunkId, chunkContent, metadata);

                if (success)
                {
                    _logger.LogInfo($"✅ Conversation chunk stored successfully - ID: {chunkId}");
                }
                else
                {
                    _logger.LogWarning($"⚠️ Failed to store conversation chunk - ID: {chunkId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error processing conversation chunk: {ex.Message}", ex);
                // Don't throw - conversation chunk failure shouldn't break chat
            }
        }

        /// <summary>
        /// Fallback context search using individual database calls when hybrid search is unavailable
        /// </summary>
        private async Task<string> FallbackContextSearchAsync(Guid userId, string userMessage)
        {
            string contextualInformation = "";
            
            // 1. Search for relevant memories using MemoryService fan-out
            try
            {
                _logger.LogInfo("🧠 Fallback: Searching for relevant memories using MemoryService...");
                var memoryResults = await _memoryService.SearchMemoriesAsync(userId, userMessage, maxResults: 3);

                if (memoryResults.Any())
                {
                    _logger.LogInfo($"✅ Found {memoryResults.Count} relevant memories");
                    var memoryTexts = memoryResults.Select(m => $"- {m.Memory.Content}").ToList();

                    if (memoryTexts.Any())
                    {
                        contextualInformation += $"\n\nRelevant memories from previous conversations:\n{string.Join("\n", memoryTexts)}";
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

            // 2. Search for relevant conversation chunks using MemoryService
            try
            {
                _logger.LogInfo("💬 Fallback: Searching for relevant conversation chunks...");
                var conversationResults = await _memoryService.GetGraphMemoriesAsync(userId, userMessage, maxResults: 3);

                if (conversationResults.Any())
                {
                    _logger.LogInfo($"✅ Found {conversationResults.Count} relevant conversation chunks");
                    var conversationTexts = conversationResults.Select(m => $"- {m.Content}").ToList();

                    if (conversationTexts.Any())
                    {
                        contextualInformation += $"\n\nRelevant past conversations:\n{string.Join("\n", conversationTexts)}";
                        _logger.LogInfo($"✅ Including {conversationTexts.Count} conversation chunks in context");
                    }
                }
                else
                {
                    _logger.LogInfo("ℹ️ No relevant conversation chunks found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ Failed to search conversation chunks: {ex.Message}");
                // Continue without conversation chunks - don't fail the entire request
            }

            // 3. Search for relevant uploaded documents using MemoryService
            try
            {
                _logger.LogInfo("📄 Fallback: Searching for relevant uploaded documents...");
                var uploadResults = await _memoryService.GetDocumentMemoriesAsync(userId, maxResults: 3);

                if (uploadResults.Any())
                {
                    _logger.LogInfo($"✅ Found {uploadResults.Count} relevant upload chunks");
                    var uploadTexts = uploadResults.Select(m => $"- {m.Content}").ToList();

                    if (uploadTexts.Any())
                    {
                        contextualInformation += $"\n\nRelevant information from uploaded documents:\n{string.Join("\n", uploadTexts)}";
                        _logger.LogInfo($"✅ Including {uploadTexts.Count} upload chunks in context");
                    }
                }
                else
                {
                    _logger.LogInfo("ℹ️ No relevant uploaded documents found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ Failed to search uploaded documents: {ex.Message}");
                // Continue without uploads - don't fail the entire request
            }
            
            return contextualInformation;
        }
    }
}
