using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for the settings service
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets a setting value for a specific user (or global if userId is null).
        /// </summary>
        Task<string> GetSettingAsync(Guid? userId, string key, string defaultValue = null);

        /// <summary>
        /// Gets all settings for a specific user (or global if userId is null),
        /// merging in any missing keys from configuration.
        /// </summary>
        Task<Dictionary<string, string>> GetAllSettingsAsync(Guid? userId);

        /// <summary>
        /// Sets a setting value for a specific user (or global if userId is null).
        /// </summary>
        Task<bool> SetSettingAsync(Guid? userId, string key, string value);

        /// <summary>
        /// Sets multiple settings for a specific user (or global if userId is null).
        /// </summary>
        Task<bool> SetSettingsAsync(Guid? userId, Dictionary<string, string> settings);

        /// <summary>
        /// Gets the Ollama API URL from settings or configuration.
        /// </summary>
        Task<string> GetOllamaApiUrlAsync(Guid? userId);

        /// <summary>
        /// Gets the LM Studio API URL from settings or configuration.
        /// </summary>
        Task<string> GetLmStudioApiUrlAsync(Guid? userId);

        /// <summary>
        /// Gets the OpenAI API URL from settings or configuration.
        /// </summary>
        Task<string> GetOpenAiApiUrlAsync(Guid? userId);

        /// <summary>
        /// Gets the OpenAI API key from settings or configuration.
        /// </summary>
        Task<string> GetOpenAiApiKeyAsync(Guid? userId);

        /// <summary>
        /// Gets the Claude API URL from settings or configuration.
        /// </summary>
        Task<string> GetClaudeApiUrlAsync(Guid? userId);

        /// <summary>
        /// Gets the Claude API key from settings or configuration.
        /// </summary>
        Task<string> GetClaudeApiKeyAsync(Guid? userId);

        /// <summary>
        /// Determines whether streaming is enabled (user-specific or global).
        /// </summary>
        Task<bool> GetEnableStreamingAsync(Guid? userId);

        /// <summary>
        /// Gets the Neo4j URI from settings or configuration.
        /// </summary>
        Task<string> GetNeo4jUriAsync(Guid? userId);

        /// <summary>
        /// Gets the Neo4j Bolt port from settings or configuration.
        /// </summary>
        Task<int> GetNeo4jBoltPortAsync(Guid? userId);

        /// <summary>
        /// Gets the Neo4j HTTP port from settings or configuration.
        /// </summary>
        Task<int> GetNeo4jHttpPortAsync(Guid? userId);

        /// <summary>
        /// Gets the ElevenLabs API key from settings or configuration.
        /// </summary>
        Task<string> GetElevenLabsApiKeyAsync(Guid? userId);

        /// <summary>
        /// Gets the default ElevenLabs voice ID from settings or configuration.
        /// </summary>
        Task<string> GetElevenLabsVoiceIdAsync(Guid? userId);

        /// <summary>
        /// Initializes a set of default settings for a brand-new user.
        /// Does nothing if a given key already exists in the database.
        /// </summary>
        Task<bool> InitializeDefaultSettingsAsync(Guid userId);

        /// <summary>
        /// Gets the default LLM engine for a user (or global), falling back to "ollama".
        /// </summary>
        Task<string> GetDefaultLlmEngineAsync(Guid? userId);

        /// <summary>
        /// Gets the default LLM model for a user (or global).
        /// </summary>
        Task<string> GetDefaultLlmModelAsync(Guid? userId);

        /// <summary>
        /// Gets the ElevenLabs API key for text-to-speech.
        /// </summary>
        Task<string> GetTtsApiKeyAsync(Guid? userId);

        /// <summary>
        /// Gets the selected ElevenLabs voice for text-to-speech.
        /// </summary>
        Task<string> GetTtsVoiceAsync(Guid? userId);
    }

    /// <summary>
    /// Service for managing user-scoped and global settings.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ISimpleLoggerService _logger;

        // --------------------------
        // Configuration keys (AppSettings)
        // --------------------------
        private const string OLLAMA_API_URL_KEY        = "OllamaApiUrl";
        private const string LM_STUDIO_API_URL_KEY     = "LmStudioApiUrl";
        private const string OPENAI_API_URL_KEY        = "OpenAiApiUrl";
        private const string OPENAI_API_KEY_KEY        = "OpenAiApiKey";
        private const string CLAUDE_API_URL_KEY        = "ClaudeApiUrl";
        private const string CLAUDE_API_KEY_KEY        = "ClaudeApiKey";
        private const string NEO4J_URI_KEY             = "Neo4jUri";
        private const string NEO4J_BOLT_PORT_KEY       = "Neo4jBoltPort";
        private const string NEO4J_HTTP_PORT_KEY       = "Neo4jHttpPort";
        private const string ELEVENLABS_API_KEY_KEY    = "ElevenLabsApiKey";
        private const string ELEVENLABS_VOICE_ID_KEY   = "ElevenLabsVoiceId";
        private const string ENABLE_STREAMING_KEY      = "EnableStreaming";
        private const string DEFAULT_LLM_ENGINE_KEY    = "DefaultLlmEngine";
        private const string DEFAULT_LLM_MODEL_KEY     = "DefaultLlmModel";
        private const string TTS_API_KEY               = "TtsElevenLabsApiKey";
        private const string TTS_VOICE_KEY             = "TtsElevenLabsVoice";

        /// <summary>
        /// Constructs a new SettingsService.
        /// </summary>
        public SettingsService(
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<string> GetSettingAsync(Guid? userId, string key, string defaultValue = null)
        {
            try
            {
                // 1) Look up in the database (user-scoped if userId != null; global if userId == null)
                var setting = await _dbContext.Settings
                    .Where(s => s.UserId == userId && s.Key == key)
                    .FirstOrDefaultAsync();

                if (setting != null)
                    return setting.Value;

                // 2) Fallback to configuration under "AppSettings:<key>"
                var configValue = _configuration[$"AppSettings:{key}"];
                return string.IsNullOrEmpty(configValue) ? defaultValue : configValue;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting setting '{key}' for user '{userId}'", ex);
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> GetAllSettingsAsync(Guid? userId)
        {
            try
            {
                // 1) Fetch all user-scoped (or global) settings from the DB
                var dbSettings = await _dbContext.Settings
                    .Where(s => s.UserId == userId)
                    .ToDictionaryAsync(s => s.Key, s => s.Value);

                // 2) Grab everything under "AppSettings" from configuration
                var appSettings = _configuration
                    .GetSection("AppSettings")
                    .GetChildren()
                    .ToDictionary(x => x.Key, x => x.Value);

                // 3) For any key in configuration not in DB, add it
                foreach (var kv in appSettings)
                {
                    if (!dbSettings.ContainsKey(kv.Key))
                        dbSettings[kv.Key] = kv.Value;
                }

                return dbSettings;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting all settings for user '{userId}'", ex);
                return new Dictionary<string, string>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetSettingAsync(Guid? userId, string key, string value)
        {
            try
            {
                // Create or update a row with (UserId = userId, Key = key)
                var existing = await _dbContext.Settings
                    .Where(s => s.UserId == userId && s.Key == key)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    var newSetting = new Settings
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,   // null means “global”
                        Key = key,
                        Value = value,
                        LastModified = DateTime.UtcNow
                    };
                    _dbContext.Settings.Add(newSetting);
                }
                else
                {
                    existing.Value = value;
                    existing.LastModified = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting '{key}' = '{value}' for user '{userId}'", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetSettingsAsync(Guid? userId, Dictionary<string, string> settings)
        {
            try
            {
                foreach (var kv in settings)
                {
                    await SetSettingAsync(userId, kv.Key, kv.Value);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting multiple settings for user '{userId}'", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetOllamaApiUrlAsync(Guid? userId)
            => await GetSettingAsync(userId, OLLAMA_API_URL_KEY, "http://localhost:11434");

        /// <inheritdoc/>
        public async Task<string> GetLmStudioApiUrlAsync(Guid? userId)
            => await GetSettingAsync(userId, LM_STUDIO_API_URL_KEY, "http://localhost:1234");

        /// <inheritdoc/>
        public async Task<string> GetOpenAiApiUrlAsync(Guid? userId)
            => await GetSettingAsync(userId, OPENAI_API_URL_KEY, "https://api.openai.com/v1");

        /// <inheritdoc/>
        public async Task<string> GetOpenAiApiKeyAsync(Guid? userId)
            => await GetSettingAsync(userId, OPENAI_API_KEY_KEY, string.Empty);

        /// <inheritdoc/>
        public async Task<string> GetClaudeApiUrlAsync(Guid? userId)
            => await GetSettingAsync(userId, CLAUDE_API_URL_KEY, "https://api.anthropic.com/v1");

        /// <inheritdoc/>
        public async Task<string> GetClaudeApiKeyAsync(Guid? userId)
            => await GetSettingAsync(userId, CLAUDE_API_KEY_KEY, string.Empty);

        /// <inheritdoc/>
        public async Task<bool> GetEnableStreamingAsync(Guid? userId)
        {
            var raw = await GetSettingAsync(userId, ENABLE_STREAMING_KEY, "true");
            return bool.TryParse(raw, out var result) ? result : true;
        }

        /// <inheritdoc/>
        public async Task<string> GetNeo4jUriAsync(Guid? userId)
            => await GetSettingAsync(userId, NEO4J_URI_KEY, "http://localhost:7474");

        /// <inheritdoc/>
        public async Task<int> GetNeo4jBoltPortAsync(Guid? userId)
        {
            var raw = await GetSettingAsync(userId, NEO4J_BOLT_PORT_KEY, "7687");
            return int.TryParse(raw, out var parsed) ? parsed : 7687;
        }

        /// <inheritdoc/>
        public async Task<int> GetNeo4jHttpPortAsync(Guid? userId)
        {
            var raw = await GetSettingAsync(userId, NEO4J_HTTP_PORT_KEY, "7474");
            return int.TryParse(raw, out var parsed) ? parsed : 7474;
        }

        /// <inheritdoc/>
        public async Task<string> GetElevenLabsApiKeyAsync(Guid? userId)
            => await GetSettingAsync(userId, ELEVENLABS_API_KEY_KEY, string.Empty);

        /// <inheritdoc/>
        public async Task<string> GetElevenLabsVoiceIdAsync(Guid? userId)
            => await GetSettingAsync(userId, ELEVENLABS_VOICE_ID_KEY, string.Empty);

        /// <inheritdoc/>
        public async Task<string> GetTtsApiKeyAsync(Guid? userId)
            => await GetSettingAsync(userId, TTS_API_KEY, string.Empty);

        /// <inheritdoc/>
        public async Task<string> GetTtsVoiceAsync(Guid? userId)
            => await GetSettingAsync(userId, TTS_VOICE_KEY, "Rachel");

        /// <inheritdoc/>
        public async Task<bool> InitializeDefaultSettingsAsync(Guid userId)
        {
            try
            {
                _logger.LogInfo($"Initializing default settings for user '{userId}'");

                var defaultSettings = new Dictionary<string, string>
                {
                    { DEFAULT_LLM_ENGINE_KEY,    "ollama" },
                    { DEFAULT_LLM_MODEL_KEY,     string.Empty },
                    { OLLAMA_API_URL_KEY,        "http://localhost:11434" },
                    { LM_STUDIO_API_URL_KEY,     "http://localhost:1234" },
                    { OPENAI_API_URL_KEY,        "https://api.openai.com/v1" },
                    { OPENAI_API_KEY_KEY,        string.Empty },
                    { CLAUDE_API_URL_KEY,        "https://api.anthropic.com/v1" },
                    { CLAUDE_API_KEY_KEY,        string.Empty },
                    { NEO4J_URI_KEY,             "http://localhost:7474" },
                    { NEO4J_BOLT_PORT_KEY,       "7687" },
                    { NEO4J_HTTP_PORT_KEY,       "7474" },
                    { ELEVENLABS_API_KEY_KEY,    string.Empty },
                    { ELEVENLABS_VOICE_ID_KEY,   string.Empty },
                    { ENABLE_STREAMING_KEY,      "true" },
                    { TTS_API_KEY,               string.Empty },
                    { TTS_VOICE_KEY,             "Rachel" }
                };

                foreach (var kv in defaultSettings)
                {
                    var exists = await _dbContext.Settings
                        .Where(s => s.UserId == userId && s.Key == kv.Key)
                        .AnyAsync();

                    if (!exists)
                    {
                        await SetSettingAsync(userId, kv.Key, kv.Value);
                    }
                }

                _logger.LogInfo($"Default settings initialized for user '{userId}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing default settings for user '{userId}'", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetDefaultLlmEngineAsync(Guid? userId)
            => await GetSettingAsync(userId, DEFAULT_LLM_ENGINE_KEY, "ollama");

        /// <inheritdoc/>
        public async Task<string> GetDefaultLlmModelAsync(Guid? userId)
            => await GetSettingAsync(userId, DEFAULT_LLM_MODEL_KEY, string.Empty);
    }
}
