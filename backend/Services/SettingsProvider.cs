using System;
using Microsoft.Extensions.Configuration;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for retrieving application settings from configuration.
    /// </summary>
    public interface ISettingsProvider
    {
        /// <summary>
        /// Gets a raw setting value from configuration (falls back to default if missing).
        /// </summary>
        /// <param name="key">The configuration key (under "AppSettings").</param>
        /// <param name="defaultValue">Value to return if the key is not found.</param>
        /// <returns>The configured value or <paramref name="defaultValue"/>.</returns>
        string GetSetting(string key, string defaultValue = null);

        /// <summary>
        /// Gets the Ollama API URL from configuration.
        /// Default: http://localhost:11434
        /// </summary>
        string GetOllamaApiUrl();

        /// <summary>
        /// Gets the LM Studio API URL from configuration.
        /// Default: http://localhost:1234
        /// </summary>
        string GetLmStudioApiUrl();

        /// <summary>
        /// Gets the OpenAI API base URL from configuration.
        /// Default: https://api.openai.com/v1
        /// </summary>
        string GetOpenAiApiUrl();

        /// <summary>
        /// Gets the OpenAI API key from configuration.
        /// </summary>
        string GetOpenAiApiKey();

        /// <summary>
        /// Gets the Claude API URL from configuration.
        /// Default: https://api.anthropic.com/v1
        /// </summary>
        string GetClaudeApiUrl();

        /// <summary>
        /// Gets the Claude API key from configuration.
        /// </summary>
        string GetClaudeApiKey();

        /// <summary>
        /// Gets the Neo4j Bolt URI (host + port) or just the URI string if you prefer.
        /// Default host: bolt://localhost
        /// Default port: 7687
        /// </summary>
        string GetNeo4jUri();

        /// <summary>
        /// Gets the Neo4j Bolt port from configuration.
        /// Default: 7687
        /// </summary>
        int GetNeo4jBoltPort();

        /// <summary>
        /// Gets the Neo4j HTTP port from configuration.
        /// Default: 7474
        /// </summary>
        int GetNeo4jHttpPort();

        /// <summary>
        /// Gets the ElevenLabs API key from configuration.
        /// </summary>
        string GetElevenLabsApiKey();

        /// <summary>
        /// Gets the default ElevenLabs voice ID from configuration.
        /// </summary>
        string GetElevenLabsVoiceId();
    }

    /// <summary>
    /// Default implementation of ISettingsProvider, pulling from IConfiguration.
    /// </summary>
    public class SettingsProvider : ISettingsProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ISimpleLoggerService _logger;

        // --------------------------
        // Configuration keys (AppSettings)
        // --------------------------

        private const string OLLAMA_API_URL_KEY       = "OllamaApiUrl";
        private const string LM_STUDIO_API_URL_KEY    = "LmStudioApiUrl";

        private const string OPENAI_API_URL_KEY       = "OpenAiApiUrl";
        private const string OPENAI_API_KEY_KEY       = "OpenAiApiKey";

        private const string CLAUDE_API_URL_KEY       = "ClaudeApiUrl";
        private const string CLAUDE_API_KEY_KEY       = "ClaudeApiKey";

        private const string NEO4J_URI_KEY            = "Neo4jUri";
        private const string NEO4J_BOLT_PORT_KEY      = "Neo4jBoltPort";
        private const string NEO4J_HTTP_PORT_KEY      = "Neo4jHttpPort";

        private const string ELEVENLABS_API_KEY_KEY   = "ElevenLabsApiKey";
        private const string ELEVENLABS_VOICE_ID_KEY  = "ElevenLabsVoiceId";

        /// <summary>
        /// Constructs a new SettingsProvider.
        /// </summary>
        /// <param name="configuration">An IConfiguration containing an "AppSettings" section.</param>
        /// <param name="logger">A simple logger to capture any exceptions if a key is missing.</param>
        public SettingsProvider(
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public string GetSetting(string key, string defaultValue = null)
        {
            try
            {
                // We expect user to keep all keys under "AppSettings" in appsettings.json (or whatever source).
                var configValue = _configuration[$"AppSettings:{key}"];
                return string.IsNullOrEmpty(configValue) ? defaultValue : configValue;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting setting '{key}' from configuration", ex);
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public string GetOllamaApiUrl()
        {
            // Fallback to localhost port 11434 if nothing is configured
            return GetSetting(OLLAMA_API_URL_KEY, "http://localhost:11434");
        }

        /// <inheritdoc/>
        public string GetLmStudioApiUrl()
        {
            return GetSetting(LM_STUDIO_API_URL_KEY, "http://localhost:1234");
        }

        /// <inheritdoc/>
        public string GetOpenAiApiUrl()
        {
            return GetSetting(OPENAI_API_URL_KEY, "https://api.openai.com/v1");
        }

        /// <inheritdoc/>
        public string GetOpenAiApiKey()
        {
            return GetSetting(OPENAI_API_KEY_KEY, string.Empty);
        }

        /// <inheritdoc/>
        public string GetClaudeApiUrl()
        {
            return GetSetting(CLAUDE_API_URL_KEY, "https://api.anthropic.com/v1");
        }

        /// <inheritdoc/>
        public string GetClaudeApiKey()
        {
            return GetSetting(CLAUDE_API_KEY_KEY, string.Empty);
        }

        /// <inheritdoc/>
        public string
