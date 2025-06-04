using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for the settings provider
    /// </summary>
    public interface ISettingsProvider
    {
        /// <summary>
        /// Gets a setting value from configuration
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <param name="defaultValue">Default value if setting not found</param>
        /// <returns>Setting value</returns>
        string GetSetting(string key, string defaultValue = null);

        /// <summary>
        /// Gets the Ollama API URL from configuration
        /// </summary>
        /// <returns>Ollama API URL</returns>
        string GetOllamaApiUrl();

        /// <summary>
        /// Gets the LM Studio API URL from configuration
        /// </summary>
        /// <returns>LM Studio API URL</returns>
        string GetLmStudioApiUrl();

        /// <summary>
        /// Gets the Neo4j URI from configuration
        /// </summary>
        /// <returns>Neo4j URI</returns>
        string GetNeo4jUri();

        /// <summary>
        /// Gets the Neo4j Bolt port from configuration
        /// </summary>
        /// <returns>Neo4j Bolt port</returns>
        int GetNeo4jBoltPort();

        /// <summary>
        /// Gets the Neo4j HTTP port from configuration
        /// </summary>
        /// <returns>Neo4j HTTP port</returns>
        int GetNeo4jHttpPort();

        /// <summary>
        /// Gets the OpenAI API key
        /// </summary>
        string GetOpenAiApiKey();

        /// <summary>
        /// Gets the OpenAI API base URL
        /// </summary>
        string GetOpenAiApiUrl();

        /// <summary>
        /// Gets the Claude API key
        /// </summary>
        string GetClaudeApiKey();

        /// <summary>
        /// Gets the Claude API base URL
        /// </summary>
        string GetClaudeApiUrl();
    }

    /// <summary>
    /// Provider for application settings from configuration
    /// </summary>
    public class SettingsProvider : ISettingsProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ISimpleLoggerService _logger;

        // Setting keys
        private const string OLLAMA_API_URL_KEY = "OllamaApiUrl";
        private const string LM_STUDIO_API_URL_KEY = "LmStudioApiUrl";
        private const string NEO4J_URI_KEY = "Neo4jUri";
        private const string NEO4J_BOLT_PORT_KEY = "Neo4jBoltPort";
        private const string NEO4J_HTTP_PORT_KEY = "Neo4jHttpPort";
        private const string OPENAI_API_KEY = "OpenAiApiKey";
        private const string OPENAI_API_URL = "OpenAiApiUrl";
        private const string CLAUDE_API_KEY = "ClaudeApiKey";
        private const string CLAUDE_API_URL = "ClaudeApiUrl";

        /// <summary>
        /// Initializes a new instance of the SettingsProvider
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger service</param>
        public SettingsProvider(
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string GetSetting(string key, string defaultValue = null)
        {
            try
            {
                // Get from configuration
                var configValue = _configuration[$"AppSettings:{key}"];
                return configValue ?? defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting setting {key}", ex);
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public string GetOllamaApiUrl()
        {
            return GetSetting(OLLAMA_API_URL_KEY, "http://localhost:11434");
        }

        /// <inheritdoc/>
        public string GetLmStudioApiUrl()
        {
            return GetSetting(LM_STUDIO_API_URL_KEY, "http://localhost:1234");
        }

        /// <inheritdoc/>
        public string GetNeo4jUri()
        {
            return GetSetting(NEO4J_URI_KEY, "http://localhost:7474");
        }

        /// <inheritdoc/>
        public int GetNeo4jBoltPort()
        {
            var portStr = GetSetting(NEO4J_BOLT_PORT_KEY, "7687");
            return int.TryParse(portStr, out int port) ? port : 7687;
        }

        /// <inheritdoc/>
        public int GetNeo4jHttpPort()
        {
            var portStr = GetSetting(NEO4J_HTTP_PORT_KEY, "7474");
            return int.TryParse(portStr, out int port) ? port : 7474;
        }

        /// <inheritdoc/>
        public string GetOpenAiApiKey()
        {
            return GetSetting(OPENAI_API_KEY, string.Empty);
        }

        /// <inheritdoc/>
        public string GetOpenAiApiUrl()
        {
            return GetSetting(OPENAI_API_URL, "https://api.openai.com");
        }

        /// <inheritdoc/>
        public string GetClaudeApiKey()
        {
            return GetSetting(CLAUDE_API_KEY, string.Empty);
        }

        /// <inheritdoc/>
        public string GetClaudeApiUrl()
        {
            return GetSetting(CLAUDE_API_URL, "https://api.anthropic.com");
        }
    }
}
