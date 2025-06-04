using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    public interface IConfigurationService
    {
        string GetApiBaseUrl();
        string GetSignalRHubUrl(string hubName);
        Dictionary<string, string> GetAllEndpoints();
        string GetOllamaApiUrl();
        string GetLmStudioApiUrl();
        string GetOpenAiApiUrl();
        string GetOpenAiApiKey();
        string GetClaudeApiUrl();
        string GetClaudeApiKey();
        string GetNeo4jUri();
        int GetNeo4jBoltPort();
        int GetNeo4jHttpPort();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ISettingsProvider _settingsProvider;
        private readonly ISimpleLoggerService _logger;
        private readonly string _baseUrl;

        public ConfigurationService(
            IConfiguration configuration,
            ISettingsProvider settingsProvider,
            ISimpleLoggerService logger)
        {
            _configuration = configuration;
            _settingsProvider = settingsProvider;
            _logger = logger;

            // Get the base URL from configuration or use default
            _baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
        }

        public string GetApiBaseUrl()
        {
            return _baseUrl;
        }

        public string GetSignalRHubUrl(string hubName)
        {
            return $"{_baseUrl}/hubs/{hubName}";
        }

        public Dictionary<string, string> GetAllEndpoints()
        {
            try
            {
                var endpoints = new Dictionary<string, string>
                {
                    { "api", GetApiBaseUrl() },
                    { "chatHub", GetSignalRHubUrl("chat") },
                    { "voiceHub", GetSignalRHubUrl("voice") },
                    { "notificationHub", GetSignalRHubUrl("notification") }
                };

                // Add user-configurable endpoints
                endpoints["ollamaApi"] = GetOllamaApiUrl();
                endpoints["lmStudioApi"] = GetLmStudioApiUrl();
                endpoints["openAiApi"] = GetOpenAiApiUrl();
                endpoints["claudeApi"] = GetClaudeApiUrl();
                endpoints["neo4jHttp"] = GetNeo4jUri();
                endpoints["neo4jBolt"] = $"bolt://localhost:{GetNeo4jBoltPort()}";

                return endpoints;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting all endpoints", ex);

                // Fallback to default values
                return new Dictionary<string, string>
                {
                    { "api", GetApiBaseUrl() },
                    { "chatHub", GetSignalRHubUrl("chat") },
                    { "voiceHub", GetSignalRHubUrl("voice") },
                    { "notificationHub", GetSignalRHubUrl("notification") },
                    { "ollamaApi", _configuration["AppSettings:OllamaApiUrl"] ?? "http://localhost:11434" },
                    { "lmStudioApi", _configuration["AppSettings:LmStudioApiUrl"] ?? "http://localhost:1234" },
                    { "neo4jHttp", _configuration["AppSettings:Neo4jUri"] ?? "http://localhost:7474" },
                    { "neo4jBolt", $"bolt://localhost:{_configuration.GetValue<int>("AppSettings:Neo4jBoltPort", 7687)}" }
                };
            }
        }

        public string GetOllamaApiUrl()
        {
            return _settingsProvider.GetOllamaApiUrl();
        }

        public string GetLmStudioApiUrl()
        {
            return _settingsProvider.GetLmStudioApiUrl();
        }

        public string GetOpenAiApiUrl()
        {
            return _settingsProvider.GetOpenAiApiUrl();
        }

        public string GetOpenAiApiKey()
        {
            return _settingsProvider.GetOpenAiApiKey();
        }

        public string GetClaudeApiUrl()
        {
            return _settingsProvider.GetClaudeApiUrl();
        }

        public string GetClaudeApiKey()
        {
            return _settingsProvider.GetClaudeApiKey();
        }

        public string GetNeo4jUri()
        {
            return _settingsProvider.GetNeo4jUri();
        }

        public int GetNeo4jBoltPort()
        {
            return _settingsProvider.GetNeo4jBoltPort();
        }

        public int GetNeo4jHttpPort()
        {
            return _settingsProvider.GetNeo4jHttpPort();
        }
    }
}
