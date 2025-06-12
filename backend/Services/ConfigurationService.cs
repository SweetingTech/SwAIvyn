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
        Task<Dictionary<string, string>> GetAllEndpoints();

        Task<string> GetOllamaApiUrl();
        Task<string> GetLmStudioApiUrl();
        Task<string> GetOpenAiApiUrl(Guid? userId = null);
        Task<string> GetOpenAiApiKey(Guid? userId = null);        Task<string> GetClaudeApiUrl(Guid? userId = null);
        Task<string> GetClaudeApiKey(Guid? userId = null);
        Task<string> GetFishSpeechApiKey(Guid? userId = null);
        Task<string> GetNeo4jUri();
        Task<int> GetNeo4jBoltPort();
        Task<int> GetNeo4jHttpPort();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ISettingsService _settingsService;
        private readonly ISimpleLoggerService _logger;
        private readonly string _baseUrl;

        public ConfigurationService(
            IConfiguration configuration,
            ISettingsService settingsService,
            ISimpleLoggerService logger)
        {
            _configuration = configuration;
            _settingsService = settingsService;
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

        public async Task<Dictionary<string, string>> GetAllEndpoints()
        {
            try
            {
                var endpoints = new Dictionary<string, string>
                {
                    { "api", GetApiBaseUrl() },
                    { "chatHub", GetSignalRHubUrl("chat") },
                    { "voiceHub", GetSignalRHubUrl("voice") },
                    { "notificationHub", GetSignalRHubUrl("notification") },

                    // User-configurable endpoints
                    { "ollamaApi", await GetOllamaApiUrl() },
                    { "lmStudioApi", await GetLmStudioApiUrl() },
                    { "openAiApi", await GetOpenAiApiUrl() },
                    { "claudeApi", await GetClaudeApiUrl() },
                    { "neo4jHttp", await GetNeo4jUri() },
                    { "neo4jBolt", $"bolt://localhost:{await GetNeo4jBoltPort()}" }
                };

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
                    { "openAiApi", _configuration["AppSettings:OpenAiApiUrl"] ?? "https://api.openai.com/v1" },
                    { "claudeApi", _configuration["AppSettings:ClaudeApiUrl"] ?? "https://api.anthropic.com/v1" },
                    { "neo4jHttp", _configuration["AppSettings:Neo4jUri"] ?? "http://localhost:7474" },
                    { "neo4jBolt", $"bolt://localhost:{_configuration.GetValue<int>("AppSettings:Neo4jBoltPort", 7687)}" }
                };
            }
        }

        public async Task<string> GetOllamaApiUrl()
        {
            return await _settingsService.GetOllamaApiUrlAsync(null);
        }

        public async Task<string> GetLmStudioApiUrl()
        {
            return await _settingsService.GetLmStudioApiUrlAsync(null);
        }

        public async Task<string> GetOpenAiApiUrl(Guid? userId = null)
        {
            return await _settingsService.GetOpenAiApiUrlAsync(userId);
        }

        public async Task<string> GetOpenAiApiKey(Guid? userId = null)
        {
            return await _settingsService.GetOpenAiApiKeyAsync(userId);
        }

        public async Task<string> GetClaudeApiUrl(Guid? userId = null)
        {
            return await _settingsService.GetClaudeApiUrlAsync(userId);
        }        public async Task<string> GetClaudeApiKey(Guid? userId = null)
        {
            return await _settingsService.GetClaudeApiKeyAsync(userId);
        }

        public async Task<string> GetFishSpeechApiKey(Guid? userId = null)
        {
            return await _settingsService.GetFishSpeechApiKeyAsync(userId);
        }

        public async Task<string> GetNeo4jUri()
        {
            return await _settingsService.GetNeo4jUriAsync(null);
        }

        public async Task<int> GetNeo4jBoltPort()
        {
            return await _settingsService.GetNeo4jBoltPortAsync(null);
        }

        public async Task<int> GetNeo4jHttpPort()
        {
            return await _settingsService.GetNeo4jHttpPortAsync(null);
        }
    }
}
