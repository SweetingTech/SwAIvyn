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
        /// Gets a setting value for a specific user
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <param name="key">Setting key</param>
        /// <param name="defaultValue">Default value if setting not found</param>
        /// <returns>Setting value</returns>
        Task<string> GetSettingAsync(Guid? userId, string key, string defaultValue = null);

        /// <summary>
        /// Gets all settings for a specific user
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>Dictionary of settings</returns>
        Task<Dictionary<string, string>> GetAllSettingsAsync(Guid? userId);

        /// <summary>
        /// Sets a setting value for a specific user
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <param name="key">Setting key</param>
        /// <param name="value">Setting value</param>
        /// <returns>Success indicator</returns>
        Task<bool> SetSettingAsync(Guid? userId, string key, string value);

        /// <summary>
        /// Sets multiple settings for a specific user
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <param name="settings">Dictionary of settings</param>
        /// <returns>Success indicator</returns>
        Task<bool> SetSettingsAsync(Guid? userId, Dictionary<string, string> settings);

        /// <summary>
        /// Gets the Ollama API URL from settings or configuration
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>Ollama API URL</returns>
        Task<string> GetOllamaApiUrlAsync(Guid? userId);

        /// <summary>
        /// Gets the LM Studio API URL from settings or configuration
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>LM Studio API URL</returns>
        Task<string> GetLmStudioApiUrlAsync(Guid? userId);

        /// <summary>
        /// Gets the OpenAI API URL
        /// </summary>
        Task<string> GetOpenAiApiUrlAsync(Guid? userId);

        /// <summary>
        /// Gets the OpenAI API key
        /// </summary>
        Task<string> GetOpenAiApiKeyAsync(Guid? userId);

        /// <summary>
        /// Gets the Claude API URL
        /// </summary>
        Task<string> GetClaudeApiUrlAsync(Guid? userId);

        /// <summary>
        /// Gets the Claude API key
        /// </summary>
        Task<string> GetClaudeApiKeyAsync(Guid? userId);

        /// <summary>
        /// Gets the streaming enabled setting for a user
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>True if streaming is enabled, false otherwise</returns>
        Task<bool> GetEnableStreamingAsync(Guid? userId);

        /// <summary>
        /// Gets the Neo4j URI from settings or configuration
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>Neo4j URI</returns>
        Task<string> GetNeo4jUriAsync(Guid? userId);

        /// <summary>
        /// Gets the Neo4j Bolt port from settings or configuration
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>Neo4j Bolt port</returns>
        Task<int> GetNeo4jBoltPortAsync(Guid? userId);

        /// <summary>
        /// Gets the Neo4j HTTP port from settings or configuration
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>Neo4j HTTP port</returns>
        Task<int> GetNeo4jHttpPortAsync(Guid? userId);

        /// <summary>
        /// Initializes default settings for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success indicator</returns>
        Task<bool> InitializeDefaultSettingsAsync(Guid userId);

        /// <summary>
        /// Gets the default LLM engine for a user
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>Default LLM engine</returns>
        Task<string> GetDefaultLlmEngineAsync(Guid? userId);

        /// <summary>
        /// Gets the default LLM model for a user
        /// </summary>
        /// <param name="userId">User ID (null for global settings)</param>
        /// <returns>Default LLM model</returns>
        Task<string> GetDefaultLlmModelAsync(Guid? userId);
    }

    /// <summary>
    /// Service for managing user and system settings
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ISimpleLoggerService _logger;

        // Setting keys
        private const string OLLAMA_API_URL_KEY = "OllamaApiUrl";
        private const string LM_STUDIO_API_URL_KEY = "LmStudioApiUrl";
        private const string NEO4J_URI_KEY = "Neo4jUri";
        private const string NEO4J_BOLT_PORT_KEY = "Neo4jBoltPort";
        private const string NEO4J_HTTP_PORT_KEY = "Neo4jHttpPort";
        private const string OPENAI_API_URL_KEY = "OpenAiApiUrl";
        private const string OPENAI_API_KEY_KEY = "OpenAiApiKey";
        private const string CLAUDE_API_URL_KEY = "ClaudeApiUrl";
        private const string CLAUDE_API_KEY_KEY = "ClaudeApiKey";

        /// <summary>
        /// Initializes a new instance of the SettingsService
        /// </summary>
        /// <param name="dbContext">Database context</param>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger service</param>
        public SettingsService(
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> GetSettingAsync(Guid? userId, string key, string defaultValue = null)
        {
            try
            {
                var setting = await _dbContext.Settings
                    .Where(s => s.UserId == userId && s.Key == key)
                    .FirstOrDefaultAsync();

                if (setting != null)
                {
                    return setting.Value;
                }

                // If user-specific setting not found, try to get from configuration
                var configValue = _configuration[$"AppSettings:{key}"];
                return configValue ?? defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting setting {key} for user {userId}", ex);
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> GetAllSettingsAsync(Guid? userId)
        {
            try
            {
                var settings = await _dbContext.Settings
                    .Where(s => s.UserId == userId)
                    .ToDictionaryAsync(s => s.Key, s => s.Value);

                // Add default settings from configuration if not present in database
                var appSettings = _configuration.GetSection("AppSettings")
                    .GetChildren()
                    .ToDictionary(x => x.Key, x => x.Value);

                foreach (var setting in appSettings)
                {
                    if (!settings.ContainsKey(setting.Key))
                    {
                        settings[setting.Key] = setting.Value;
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting all settings for user {userId}", ex);
                return new Dictionary<string, string>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetSettingAsync(Guid? userId, string key, string value)
        {
            try
            {
                // If no userId provided, get the first available user
                if (userId == null || userId == Guid.Empty)
                {
                    var firstUser = await _dbContext.Users.FirstOrDefaultAsync();
                    if (firstUser == null)
                    {
                        _logger.LogError($"Cannot save setting '{key}': No users exist in the database");
                        return false;
                    }
                    userId = firstUser.Id;
                    _logger.LogInfo($"Using first available user ID {userId} for setting '{key}'");
                }

                var setting = await _dbContext.Settings
                    .Where(s => s.UserId == userId && s.Key == key)
                    .FirstOrDefaultAsync();

                if (setting == null)
                {
                    setting = new Settings
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId.Value,
                        Key = key,
                        Value = value,
                        LastModified = DateTime.UtcNow
                    };
                    _dbContext.Settings.Add(setting);
                }
                else
                {
                    setting.Value = value;
                    setting.LastModified = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting {key} = {value} for user {userId}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetSettingsAsync(Guid? userId, Dictionary<string, string> settings)
        {
            try
            {
                foreach (var setting in settings)
                {
                    await SetSettingAsync(userId, setting.Key, setting.Value);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting multiple settings for user {userId}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetOllamaApiUrlAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, OLLAMA_API_URL_KEY, "http://localhost:11434");
        }

        /// <inheritdoc/>
        public async Task<string> GetLmStudioApiUrlAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, LM_STUDIO_API_URL_KEY, "http://localhost:1234");
        }

        /// <inheritdoc/>
        public async Task<string> GetOpenAiApiUrlAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, OPENAI_API_URL_KEY, "https://api.openai.com/v1");
        }

        /// <inheritdoc/>
        public async Task<string> GetOpenAiApiKeyAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, OPENAI_API_KEY_KEY, "");
        }

        /// <inheritdoc/>
        public async Task<string> GetClaudeApiUrlAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, CLAUDE_API_URL_KEY, "https://api.anthropic.com/v1");
        }

        /// <inheritdoc/>
        public async Task<string> GetClaudeApiKeyAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, CLAUDE_API_KEY_KEY, "");
        }

        /// <inheritdoc/>
        public async Task<bool> GetEnableStreamingAsync(Guid? userId)
        {
            var value = await GetSettingAsync(userId, "EnableStreaming", "true");
            return bool.TryParse(value, out bool result) ? result : true; // Default to true
        }

        /// <inheritdoc/>
        public async Task<string> GetNeo4jUriAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, NEO4J_URI_KEY, "http://localhost:7474");
        }

        /// <inheritdoc/>
        public async Task<int> GetNeo4jBoltPortAsync(Guid? userId)
        {
            var portStr = await GetSettingAsync(userId, NEO4J_BOLT_PORT_KEY, "7687");
            return int.TryParse(portStr, out int port) ? port : 7687;
        }

        /// <inheritdoc/>
        public async Task<int> GetNeo4jHttpPortAsync(Guid? userId)
        {
            var portStr = await GetSettingAsync(userId, NEO4J_HTTP_PORT_KEY, "7474");
            return int.TryParse(portStr, out int port) ? port : 7474;
        }

        /// <inheritdoc/>
        public async Task<bool> InitializeDefaultSettingsAsync(Guid userId)
        {
            try
            {
                _logger.LogInfo($"Initializing default settings for user {userId}");

                var defaultSettings = new Dictionary<string, string>
                {
                    { "DefaultLlmEngine", "ollama" },
                    { "DefaultLlmModel", "" },
                    { OLLAMA_API_URL_KEY, "http://localhost:11434" },
                    { LM_STUDIO_API_URL_KEY, "http://localhost:1234" },
                    { OPENAI_API_URL_KEY, "https://api.openai.com/v1" },
                    { OPENAI_API_KEY_KEY, "" },
                    { CLAUDE_API_URL_KEY, "https://api.anthropic.com/v1" },
                    { CLAUDE_API_KEY_KEY, "" },
                    { NEO4J_URI_KEY, "http://localhost:7474" },
                    { NEO4J_BOLT_PORT_KEY, "7687" },
                    { NEO4J_HTTP_PORT_KEY, "7474" },
                    { "EnableStreaming", "true" },
                    { "Theme", "dark" },
                    { "Language", "en" },
                    { "AutoSave", "true" },
                    { "ShowWelcomeMessage", "true" }
                };

                foreach (var setting in defaultSettings)
                {
                    // Only set if the setting doesn't already exist
                    var existingSetting = await _dbContext.Settings
                        .Where(s => s.UserId == userId && s.Key == setting.Key)
                        .FirstOrDefaultAsync();

                    if (existingSetting == null)
                    {
                        await SetSettingAsync(userId, setting.Key, setting.Value);
                    }
                }

                _logger.LogInfo($"Default settings initialized for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing default settings for user {userId}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetDefaultLlmEngineAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, "DefaultLlmEngine", "ollama");
        }

        /// <inheritdoc/>
        public async Task<string> GetDefaultLlmModelAsync(Guid? userId)
        {
            return await GetSettingAsync(userId, "DefaultLlmModel", "");
        }
    }
}
