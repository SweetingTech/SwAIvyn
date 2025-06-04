using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SwAIvyn.Services;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller exposing ElevenLabs text-to-speech functionality and settings.
    /// </summary>
    [ApiController]
    [Route("api/tts")]
    public class TtsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TtsController> _logger;

        public TtsController(
            ISettingsService settingsService,
            IHttpClientFactory httpClientFactory,
            ILogger<TtsController> logger)
        {
            _settingsService = settingsService 
                ?? throw new ArgumentNullException(nameof(settingsService));
            _httpClientFactory = httpClientFactory 
                ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger 
                ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the user’s ElevenLabs TTS settings (API key and default voice).
        /// </summary>
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
                _logger.LogError(ex, "Error getting TTS settings for user {UserId}", userId);
                return StatusCode(500, "Failed to retrieve TTS settings");
            }
        }

        /// <summary>
        /// Updates the user’s ElevenLabs TTS settings (API key and/or default voice).
        /// </summary>
        [HttpPost("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateTtsSettingsRequest request)
        {
            if (request == null || request.UserId == null)
            {
                return BadRequest("UserId is required when updating TTS settings.");
            }

            try
            {
                var settings = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(request.ApiKey))
                    settings["ElevenLabsApiKey"] = request.ApiKey;

                if (!string.IsNullOrEmpty(request.VoiceId))
                    settings["ElevenLabsVoiceId"] = request.VoiceId;

                var success = await _settingsService.SetSettingsAsync(request.UserId, settings);
                if (success)
                    return Ok(new { message = "TTS settings updated successfully" });

                return StatusCode(500, "Failed to update TTS settings");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating TTS settings for user {UserId}", request.UserId);
                return StatusCode(500, "Failed to update TTS settings");
            }
        }

        /// <summary>
        /// Synthesizes speech from text. Uses the user’s configured ElevenLabs API key and voice.
        /// </summary>
        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] SynthesizeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest("Text is required for TTS synthesis.");
            }

            try
            {
                var apiKey = await _settingsService.GetElevenLabsApiKeyAsync(request.UserId);
                var voiceId = await _settingsService.GetElevenLabsVoiceIdAsync(request.UserId);

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceId))
                {
                    return BadRequest("Missing ElevenLabs API key or voice configuration.");
                }

                var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";
                var payload = JsonSerializer.Serialize(new { text = request.Text });
                var httpClient = _httpClientFactory.CreateClient();
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("xi-api-key", apiKey);

                var response = await httpClient.SendAsync(httpRequest);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("ElevenLabs API returned {StatusCode} for user {UserId}", response.StatusCode, request.UserId);
                    return StatusCode((int)response.StatusCode, "ElevenLabs API error");
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                return File(bytes, "audio/mpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synthesizing audio for user {UserId}", request.UserId);
                return StatusCode(500, "Error synthesizing audio");
            }
        }
    }

    /// <summary>
    /// Request model for updating TTS settings (ElevenLabs).
    /// </summary>
    public class UpdateTtsSettingsRequest
    {
        /// <summary>
        /// The user whose settings are being updated (required).
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// New ElevenLabs API key (leave null to keep existing).
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// New ElevenLabs voice ID (leave null to keep existing).
        /// </summary>
        public string VoiceId { get; set; }
    }

    /// <summary>
    /// Request model for performing TTS synthesis.
    /// </summary>
    public class SynthesizeRequest
    {
        /// <summary>
        /// The user for whom to look up default settings (optional).
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// The text to convert into speech (required).
        /// </summary>
        public string Text { get; set; }
    }
}
