using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SwAIvyn.Controllers
{
    // --- Data Transfer Objects (DTOs) for Chat Settings ---

    public class ChatSettingsDto
    {
        public string LlmEngine { get; set; }
        public string LlmModel { get; set; }
        public string TtsProvider { get; set; }
        public string TtsVoiceId { get; set; }
    }

    public class UpdateChatSettingsRequest
    {
        [Required]
        public string LlmEngine { get; set; }

        public string LlmModel { get; set; }

        public string TtsProvider { get; set; }

        public string TtsVoiceId { get; set; }
    }

    /// <summary>
    /// Controller for chat-related API endpoints.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ISimpleLoggerService _logger; // Optional: for logging
        private readonly ILlmConnectorService _llmConnectorService;

        // Constants for settings keys
        private const string ChatLlmEngineKey = "Chat.LlmEngine";
        private const string ChatLlmModelKey = "Chat.LlmModel";
        private const string ChatTtsProviderKey = "Chat.TtsProvider";
        private const string ChatTtsVoiceIdKey = "Chat.TtsVoiceId";

        public ChatController(ISettingsService settingsService, ISimpleLoggerService logger, ILlmConnectorService llmConnectorService)
        {
            _settingsService = settingsService;
            _logger = logger;
            _llmConnectorService = llmConnectorService;
        }

        /// <summary>
        /// Retrieves the chat configurations for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The chat configurations.</returns>
        [HttpGet("settings/{userId}")]
        public async Task<IActionResult> GetChatSettings(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest("Valid userId is required.");
            }

            try
            {
                var settingsDto = new ChatSettingsDto
                {
                    LlmEngine = await _settingsService.GetSettingAsync(userId, ChatLlmEngineKey, "ollama"), // Default to "ollama"
                    LlmModel = await _settingsService.GetSettingAsync(userId, ChatLlmModelKey, string.Empty),
                    TtsProvider = await _settingsService.GetSettingAsync(userId, ChatTtsProviderKey, "elevenlabs"), // Default to "elevenlabs"
                    TtsVoiceId = await _settingsService.GetSettingAsync(userId, ChatTtsVoiceIdKey, string.Empty)
                };
                return Ok(settingsDto);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error getting chat settings for user {userId}: {ex.Message}", ex);
                return StatusCode(500, "An error occurred while retrieving chat settings.");
            }
        }

        /// <summary>
        /// Updates the chat configurations for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="request">The chat configuration settings.</param>
        /// <returns>A success message or error.</returns>
        [HttpPut("settings/{userId}")]
        public async Task<IActionResult> UpdateChatSettings(Guid userId, [FromBody] UpdateChatSettingsRequest request)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest("Valid userId is required.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var engine = request.LlmEngine?.Trim()?.ToLowerInvariant();
                var model = request.LlmModel?.Trim();

                if (string.IsNullOrEmpty(engine))
                {
                    return BadRequest("LlmEngine is required.");
                }

                if (string.IsNullOrEmpty(model))
                {
                    switch (engine)
                    {
                        case "lmstudio":
                            model = await _llmConnectorService.GetLmStudioModelAsync(userId);
                            if (string.IsNullOrEmpty(model))
                            {
                                _logger?.LogInfo($"No LM Studio model reported as loaded for user {userId}; saving empty model.");
                                model = string.Empty;
                            }
                            break;
                        case "openai":
                        {
                            var models = await _llmConnectorService.GetOpenAiModelsAsync(userId);
                            model = models?.FirstOrDefault() ?? string.Empty;
                            break;
                        }
                        case "claude":
                        {
                            var models = await _llmConnectorService.GetClaudeModelsAsync(userId);
                            model = models?.FirstOrDefault() ?? string.Empty;
                            break;
                        }
                        case "ollama":
                            return BadRequest("LlmModel is required when engine is 'ollama'.");
                        default:
                            model = string.Empty;
                            break;
                    }
                }

                await _settingsService.SetSettingAsync(userId, ChatLlmEngineKey, engine);
                await _settingsService.SetSettingAsync(userId, ChatLlmModelKey, model ?? string.Empty);
                await _settingsService.SetSettingAsync(userId, ChatTtsProviderKey, request.TtsProvider ?? string.Empty);
                await _settingsService.SetSettingAsync(userId, ChatTtsVoiceIdKey, request.TtsVoiceId ?? string.Empty);

                _logger?.LogInfo($"Chat settings updated for user {userId}. Engine: {engine}, Model: {model}");
                return Ok(new { message = "Chat settings updated successfully." });
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error updating chat settings for user {userId}: {ex.Message}", ex);
                return StatusCode(500, "An error occurred while updating chat settings.");
            }
        }

        /// <summary>
        /// Retrieves the chat history for the user.
        /// </summary>
        /// <returns>Chat history data.</returns>
        [HttpGet]
        public IActionResult GetChatHistory()
        {
            // Placeholder for getting chat history
            return Ok(new { message = "Chat history retrieved successfully" });
        }

        /// <summary>
        /// Sends a chat message.
        /// </summary>
        /// <param name="message">The message object sent by the user.</param>
        /// <returns>Result of the send operation.</returns>
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] object message)
        {
            // Placeholder for sending a message
            // Consider using chat settings (LLM engine/model) retrieved for the user here
            return Ok(new { message = "Message sent successfully" });
        }
    }
}
