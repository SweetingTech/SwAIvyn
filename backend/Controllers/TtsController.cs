using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for text-to-speech (TTS) settings and synthesis.
    /// </summary>
    [ApiController]
    [Route("api/tts")]
    public class TtsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ITtsService _ttsService;
        private readonly ISimpleLoggerService _logger;

        public TtsController(
            ISettingsService settingsService,
            ITtsService ttsService,
            ISimpleLoggerService logger)
        {
            _settingsService = settingsService 
                ?? throw new ArgumentNullException(nameof(settingsService));
            _ttsService = ttsService 
                ?? throw new ArgumentNullException(nameof(ttsService));
            _logger = logger 
                ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the user’s ElevenLabs TTS settings (API key and default voice).
        /// </summary>
        /// <param name="userId">Optional user ID (omit for global settings).</param>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings([FromQuery] Guid? userId = null)
        {
            try
            {
                var apiKey = await _settingsService.GetElevenLabsApiKeyAsync(userId);
                var voiceId = await _settingsService.GetElevenLabsVoiceIdAsync(userId);
                return Ok(new { apiKey, voiceId });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting TTS settings", ex);
                return StatusCode(500, "Failed to get TTS settings");
            }
        }

        /// <summary>
        /// Updates the user’s ElevenLabs TTS settings (API key and/or default voice).
        /// </summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateTtsSettingsRequest request)
        {
            if (request == null || request.UserId == null)
            {
                return BadRequest("UserId is required when updating TTS settings.");
            }

            try
            {
                var settings = new Dictionary<string, string>();

                if (request.ApiKey != null)
                    settings["ElevenLabsApiKey"] = request.ApiKey;

                if (request.VoiceId != null)
                    settings["ElevenLabsVoiceId"] = request.VoiceId;

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

        /// <summary>
        /// Synthesizes speech from text. If VoiceId is omitted in the request,
        /// the user’s default ElevenLabs voice (from settings) will be used.
        /// </summary>
        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] TtsRequest request)
        {
            if (string.IsNullOrEmpty(request.Text))
            {
                return BadRequest("Text is required for TTS synthesis.");
            }

            try
            {
                // Determine which voice ID to use:
                var voiceId = request.VoiceId;
                if (string.IsNullOrEmpty(voiceId) && request.UserId.HasValue)
                {
                    voiceId = await _settingsService.GetElevenLabsVoiceIdAsync(request.UserId);
                }
                
                // Fetch API key: prefer request.ApiKey, otherwise settings
                var apiKey = request.ApiKey;
                if (string.IsNullOrEmpty(apiKey) && request.UserId.HasValue)
                {
                    apiKey = await _settingsService.GetElevenLabsApiKeyAsync(request.UserId);
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    return BadRequest("No ElevenLabs API key provided or found in settings.");
                }

                // Delegate to ITtsService to return an MP3 (byte[])
                var audioBytes = await _ttsService.SynthesizeAsync(
                    text: request.Text,
                    apiKey: apiKey,
                    voiceId: voiceId);

                if (audioBytes == null || audioBytes.Length == 0)
                {
                    return StatusCode(500, "TTS synthesis returned no audio.");
                }

                return File(audioBytes, "audio/mpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during TTS synthesis", ex);
                return StatusCode(500, $"Failed to synthesize speech: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Request model for updating TTS settings (ElevenLabs).
    /// </summary>
    public class UpdateTtsSettingsRequest
    {
        /// <summary>
        /// Required: the user whose settings are being updated.
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// New ElevenLabs API key (optional if not changing).
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// New default ElevenLabs voice ID (optional if not changing).
        /// </summary>
        public string? VoiceId { get; set; }
    }

    /// <summary>
    /// Request model for performing TTS synthesis.
    /// </summary>
    public class TtsRequest
    {
        /// <summary>
        /// The user for whom to look up default settings (optional).
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Optional override for ElevenLabs API key. If omitted, the controller will pull from user settings.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Optional override for ElevenLabs voice ID. If omitted, the controller will pull from user settings.
        /// </summary>
        public string? VoiceId { get; set; }

        /// <summary>
        /// The text to convert into speech. (Required)
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }
}
