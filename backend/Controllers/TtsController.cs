using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for text-to-speech related endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TtsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ISimpleLoggerService _logger;

        public TtsController(ISettingsService settingsService, ISimpleLoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the TTS settings for a user
        /// </summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings([FromQuery] Guid? userId = null)
        {
            try
            {
                var apiKey = await _settingsService.GetTtsApiKeyAsync(userId);
                var voice = await _settingsService.GetTtsVoiceAsync(userId);
                return Ok(new { apiKey, voice });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting TTS settings", ex);
                return StatusCode(500, "Failed to get TTS settings");
            }
        }

        /// <summary>
        /// Updates the TTS settings for a user
        /// </summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateTtsSettingsRequest request)
        {
            try
            {
                var settings = new Dictionary<string, string>();
                if (request.ApiKey != null)
                    settings["TtsElevenLabsApiKey"] = request.ApiKey;
                if (request.Voice != null)
                    settings["TtsElevenLabsVoice"] = request.Voice;

                var success = await _settingsService.SetSettingsAsync(request.UserId, settings);
                if (success)
                    return Ok(new { message = "TTS settings updated successfully" });
                return StatusCode(500, "Failed to update TTS settings");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating TTS settings", ex);
                return StatusCode(500, "Failed to update TTS settings");
            }
        }
    }

    /// <summary>
    /// Request model for updating TTS settings
    /// </summary>
    public class UpdateTtsSettingsRequest
    {
        public Guid? UserId { get; set; }
        public string ApiKey { get; set; }
        public string Voice { get; set; }
    }
}
