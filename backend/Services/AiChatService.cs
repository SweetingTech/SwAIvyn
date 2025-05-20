using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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
        public AiChatService(
            ILlmConnectorService llmConnector,
            IConversationService conversationService,
            ISettingsService settingsService,
            ISimpleLoggerService logger,
            IConfiguration configuration)
        {
            _llmConnector = llmConnector;
            _conversationService = conversationService;
            _settingsService = settingsService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <inheritdoc/>
        public async Task<string> GenerateAndStoreResponseAsync(Guid conversationId, Guid userId, string userMessage)
        {
            try
            {
                // Store the user message
                await _conversationService.AppendMessageAsync(conversationId, userId, "user", userMessage);

                // Get the current LLM settings
                var settings = await GetCurrentLlmSettingsAsync(userId);
                string engine = settings["engine"];
                string model = settings["model"];

                _logger.LogInfo($"Generating AI response using {engine} {(model != null ? $"with model {model}" : "")}");

                // Generate the AI response
                string aiResponse = await _llmConnector.GenerateResponseAsync(userMessage, engine, model, userId);

                // Store the AI response
                await _conversationService.AppendMessageAsync(conversationId, userId, "assistant", aiResponse);

                return aiResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating AI response for conversation {conversationId}", ex);
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
    }
}
