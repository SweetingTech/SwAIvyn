using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TtsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TtsController> _logger;

        public TtsController(ISettingsService settingsService, IHttpClientFactory httpClientFactory, ILogger<TtsController> logger)
        {
            _settingsService = settingsService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings([FromQuery] Guid? userId = null)
        {
            var apiKey = await _settingsService.GetSettingAsync(userId, "ElevenLabsApiKey", string.Empty);
            var voiceId = await _settingsService.GetSettingAsync(userId, "ElevenLabsVoiceId", string.Empty);
            return Ok(new { apiKey, voiceId });
        }

        public class UpdateTtsSettingsRequest
        {
            public Guid? UserId { get; set; }
            public string ApiKey { get; set; }
            public string VoiceId { get; set; }
        }

        [HttpPost("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateTtsSettingsRequest request)
        {
            var settings = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(request.ApiKey))
            {
                settings["ElevenLabsApiKey"] = request.ApiKey;
            }
            if (!string.IsNullOrEmpty(request.VoiceId))
            {
                settings["ElevenLabsVoiceId"] = request.VoiceId;
            }

            var success = await _settingsService.SetSettingsAsync(request.UserId, settings);
            if (success)
            {
                return Ok(new { message = "TTS settings updated" });
            }
            return StatusCode(500, "Failed to update TTS settings");
        }

        public class SynthesizeRequest
        {
            public Guid? UserId { get; set; }
            public string Text { get; set; }
        }

        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] SynthesizeRequest request)
        {
            var apiKey = await _settingsService.GetSettingAsync(request.UserId, "ElevenLabsApiKey", string.Empty);
            var voiceId = await _settingsService.GetSettingAsync(request.UserId, "ElevenLabsVoiceId", string.Empty);

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceId))
            {
                return BadRequest("Missing ElevenLabs configuration");
            }

            var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";
            var payload = JsonSerializer.Serialize(new { text = request.Text });
            var httpClient = _httpClientFactory.CreateClient();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            httpRequest.Headers.Add("xi-api-key", apiKey);

            try
            {
                var response = await httpClient.SendAsync(httpRequest);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"ElevenLabs API error: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode, "ElevenLabs API error");
                }
                var bytes = await response.Content.ReadAsByteArrayAsync();
                return File(bytes, "audio/mpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ElevenLabs API");
                return StatusCode(500, "Error synthesizing audio");
            }
        }
    }
}
