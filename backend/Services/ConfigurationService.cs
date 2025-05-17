using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace SwAIvyn.Services
{
    public interface IConfigurationService
    {
        string GetApiBaseUrl();
        string GetSignalRHubUrl(string hubName);
        Dictionary<string, string> GetAllEndpoints();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;

        public ConfigurationService(IConfiguration configuration)
        {
            _configuration = configuration;
            
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
            return new Dictionary<string, string>
            {
                { "api", GetApiBaseUrl() },
                { "chatHub", GetSignalRHubUrl("chat") },
                { "voiceHub", GetSignalRHubUrl("voice") },
                { "notificationHub", GetSignalRHubUrl("notification") }
            };
        }
    }
}
