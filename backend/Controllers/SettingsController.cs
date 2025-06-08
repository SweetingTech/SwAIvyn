using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for managing user and system settings
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IAiChatService _aiChatService;
        private readonly ISimpleLoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the SettingsController
        /// </summary>
        public SettingsController(
            ISettingsService settingsService,
            IAiChatService aiChatService,
            ISimpleLoggerService logger)
        {
            _settingsService = settingsService;
            _aiChatService = aiChatService;
            _logger = logger;
        }

        /// <summary>
        /// Gets all settings for a user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSettings([FromQuery] Guid? userId = null)
        {
            try
            {
                var settings = await _settingsService.GetAllSettingsAsync(userId);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting settings", ex);
                return StatusCode(500, "An error occurred while getting settings");
            }
        }

        /// <summary>
        /// Gets a specific setting for a user
        /// </summary>
        [HttpGet("{key}")]
        public async Task<IActionResult> GetSetting(string key, [FromQuery] Guid? userId = null)
        {
            try
            {
                var value = await _settingsService.GetSettingAsync(userId, key);
                if (value == null)
                {
                    return NotFound($"Setting '{key}' not found");
                }
                return Ok(new { key, value });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting setting {key}", ex);
                return StatusCode(500, $"An error occurred while getting setting {key}");
            }
        }

        /// <summary>
        /// Sets a specific setting for a user
        /// </summary>
        [HttpPut("{key}")]
        public async Task<IActionResult> SetSetting(string key, [FromBody] SetSettingRequest request)
        {
            try
            {
                var success = await _settingsService.SetSettingAsync(request.UserId, key, request.Value);
                if (success)
                {
                    return Ok(new { message = $"Setting '{key}' updated successfully" });
                }
                return StatusCode(500, $"Failed to update setting '{key}'");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting {key}", ex);
                return StatusCode(500, $"An error occurred while setting {key}");
            }
        }

        /// <summary>
        /// Sets multiple settings for a user
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> SetSettings([FromBody] SetSettingsRequest request)
        {
            try
            {
                var success = await _settingsService.SetSettingsAsync(request.UserId, request.Settings);
                if (success)
                {
                    return Ok(new { message = "Settings updated successfully" });
                }
                return StatusCode(500, "Failed to update settings");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error setting multiple settings", ex);
                return StatusCode(500, "An error occurred while setting multiple settings");
            }
        }

        /// <summary>
        /// Gets connection settings for a user
        /// </summary>
        [HttpGet("connections")]
        public async Task<IActionResult> GetConnectionSettings([FromQuery] Guid? userId = null)
        {
            try
            {
                var settings = new ConnectionSettings
                {
                    OllamaApiUrl     = await _settingsService.GetOllamaApiUrlAsync(userId),
                    LmStudioApiUrl   = await _settingsService.GetLmStudioApiUrlAsync(userId),
                    OpenAiApiUrl     = await _settingsService.GetOpenAiApiUrlAsync(userId),
                    OpenAiApiKey     = await _settingsService.GetOpenAiApiKeyAsync(userId),
                    ClaudeApiUrl     = await _settingsService.GetClaudeApiUrlAsync(userId),
                    ClaudeApiKey     = await _settingsService.GetClaudeApiKeyAsync(userId),
                    Neo4jUri         = await _settingsService.GetNeo4jUriAsync(userId),
                    Neo4jBoltPort    = await _settingsService.GetNeo4jBoltPortAsync(userId),
                    Neo4jHttpPort    = await _settingsService.GetNeo4jHttpPortAsync(userId),
                    EnableStreaming  = await _settingsService.GetEnableStreamingAsync(userId)
                };
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting connection settings", ex);
                return StatusCode(500, "An error occurred while getting connection settings");
            }
        }

        /// <summary>
        /// Updates connection settings for a user
        /// </summary>
        [HttpPut("connections")]
        public async Task<IActionResult> UpdateConnectionSettings([FromBody] UpdateConnectionSettingsRequest request)
        {
            try
            {
                var settings = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(request.OllamaApiUrl))
                    settings["OllamaApiUrl"] = request.OllamaApiUrl;

                if (!string.IsNullOrEmpty(request.LmStudioApiUrl))
                    settings["LmStudioApiUrl"] = request.LmStudioApiUrl;

                if (!string.IsNullOrEmpty(request.OpenAiApiUrl))
                    settings["OpenAiApiUrl"] = request.OpenAiApiUrl;

                if (!string.IsNullOrEmpty(request.OpenAiApiKey))
                    settings["OpenAiApiKey"] = request.OpenAiApiKey;

                if (!string.IsNullOrEmpty(request.ClaudeApiUrl))
                    settings["ClaudeApiUrl"] = request.ClaudeApiUrl;

                if (!string.IsNullOrEmpty(request.ClaudeApiKey))
                    settings["ClaudeApiKey"] = request.ClaudeApiKey;

                if (!string.IsNullOrEmpty(request.Neo4jUri))
                    settings["Neo4jUri"] = request.Neo4jUri;

                if (request.Neo4jBoltPort.HasValue)
                    settings["Neo4jBoltPort"] = request.Neo4jBoltPort.Value.ToString();

                if (request.Neo4jHttpPort.HasValue)
                    settings["Neo4jHttpPort"] = request.Neo4jHttpPort.Value.ToString();

                if (request.EnableStreaming.HasValue)
                    settings["EnableStreaming"] = request.EnableStreaming.Value.ToString();

                var success = await _settingsService.SetSettingsAsync(request.UserId, settings);
                if (success)
                {
                    return Ok(new { message = "Connection settings updated successfully" });
                }
                return StatusCode(500, "Failed to update connection settings");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating connection settings", ex);
                return StatusCode(500, "An error occurred while updating connection settings");
            }
        }

        /// <summary>
        /// Gets LLM settings for a user
        /// </summary>
        [HttpGet("llm")]
        public async Task<IActionResult> GetLlmSettings([FromQuery] Guid? userId = null)
        {
            try
            {
                var settings = await _aiChatService.GetCurrentLlmSettingsAsync(userId);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting LLM settings", ex);
                return StatusCode(500, "An error occurred while getting LLM settings");
            }
        }

        /// <summary>
        /// Updates LLM settings for a user
        /// </summary>
        [HttpPut("llm")]
        public async Task<IActionResult> UpdateLlmSettings([FromBody] UpdateLlmSettingsRequest request)
        {
            try
            {
                var success = await _aiChatService.SetDefaultLlmSettingsAsync(
                    request.UserId, request.Engine, request.Model);

                if (success)
                    return Ok(new { message = "LLM settings updated successfully" });

                return StatusCode(500, "Failed to update LLM settings");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating LLM settings", ex);
                return StatusCode(500, "An error occurred while updating LLM settings");
            }
        }

        /// <summary>
        /// Clears the corrupted Claude API key
        /// </summary>
        [HttpPost("clear-claude-key")]
        public async Task<IActionResult> ClearClaudeKey()
        {
            try
            {
                await _settingsService.SetSettingAsync(null, "ClaudeApiKey", string.Empty);
                _logger.LogInfo("Claude API key cleared successfully");
                return Ok(new { message = "Claude API key cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error clearing Claude API key", ex);
                return StatusCode(500, new { error = "Failed to clear Claude API key" });
            }
        }
    }

    /// <summary>
    /// Request model for setting a single setting
    /// </summary>
    public class SetSettingRequest
    {
        /// <summary>
        /// User ID (null for global settings)
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Setting value
        /// </summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// Request model for setting multiple settings
    /// </summary>
    public class SetSettingsRequest
    {
        /// <summary>
        /// User ID (null for global settings)
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Dictionary of settings
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }
    }

    /// <summary>
    /// Model for connection settings
    /// </summary>
    public class ConnectionSettings
    {
        /// <summary>
        /// Ollama API URL
        /// </summary>
        public string OllamaApiUrl { get; set; }

        /// <summary>
        /// LM Studio API URL
        /// </summary>
        public string LmStudioApiUrl { get; set; }

        /// <summary>
        /// OpenAI API URL
        /// </summary>
        public string OpenAiApiUrl { get; set; }

        /// <summary>
        /// OpenAI API Key
        /// </summary>
        public string OpenAiApiKey { get; set; }

        /// <summary>
        /// Claude API URL
        /// </summary>
        public string ClaudeApiUrl { get; set; }

        /// <summary>
        /// Claude API Key
        /// </summary>
        public string ClaudeApiKey { get; set; }

        /// <summary>
        /// Neo4j URI
        /// </summary>
        public string Neo4jUri { get; set; }

        /// <summary>
        /// Neo4j Bolt port
        /// </summary>
        public int Neo4jBoltPort { get; set; }

        /// <summary>
        /// Neo4j HTTP port
        /// </summary>
        public int Neo4jHttpPort { get; set; }

        /// <summary>
        /// Enable streaming responses
        /// </summary>
        public bool EnableStreaming { get; set; }
    }

    /// <summary>
    /// Request model for updating connection settings
    /// </summary>
    public class UpdateConnectionSettingsRequest
    {
        /// <summary>
        /// User ID (null for global settings)
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Ollama API URL
        /// </summary>
        public string? OllamaApiUrl { get; set; }

        /// <summary>
        /// LM Studio API URL
        /// </summary>
        public string? LmStudioApiUrl { get; set; }

        /// <summary>
        /// OpenAI API URL
        /// </summary>
        public string? OpenAiApiUrl { get; set; }

        /// <summary>
        /// OpenAI API Key
        /// </summary>
        public string? OpenAiApiKey { get; set; }

        /// <summary>
        /// Claude API URL
        /// </summary>
        public string? ClaudeApiUrl { get; set; }

        /// <summary>
        /// Claude API Key
        /// </summary>
        public string? ClaudeApiKey { get; set; }

        /// <summary>
        /// Neo4j URI
        /// </summary>
        public string? Neo4jUri { get; set; }

        /// <summary>
        /// Neo4j Bolt port
        /// </summary>
        public int? Neo4jBoltPort { get; set; }

        /// <summary>
        /// Neo4j HTTP port
        /// </summary>
        public int? Neo4jHttpPort { get; set; }

        /// <summary>
        /// Enable streaming responses
        /// </summary>
        public bool? EnableStreaming { get; set; }
    }

    /// <summary>
    /// Request model for updating LLM settings
    /// </summary>
    public class UpdateLlmSettingsRequest
    {
        /// <summary>
        /// User ID (null for global settings)
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// LLM engine (ollama, lmstudio, openai, or claude)
        /// </summary>
        public string? Engine { get; set; }

        /// <summary>
        /// LLM model name (for Ollama)
        /// </summary>
        public string? Model { get; set; }
    }
}
