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
    /// Controller exposing text-to-speech functionality and settings for multiple providers.
    /// </summary>
    [ApiController]
    [Route("api/tts")]
    public class TtsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ElevenLabsTtsService _elevenLabsService;
        private readonly FishSpeechTtsService _fishSpeechService;
        private readonly ILogger<TtsController> _logger;

        public TtsController(
            ISettingsService settingsService,
            IHttpClientFactory httpClientFactory,
            ElevenLabsTtsService elevenLabsService,
            FishSpeechTtsService fishSpeechService,
            ILogger<TtsController> logger)
        {
            _settingsService = settingsService 
                ?? throw new ArgumentNullException(nameof(settingsService));
            _httpClientFactory = httpClientFactory 
                ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _elevenLabsService = elevenLabsService
                ?? throw new ArgumentNullException(nameof(elevenLabsService));
            _fishSpeechService = fishSpeechService
                ?? throw new ArgumentNullException(nameof(fishSpeechService));
            _logger = logger 
                ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the user's TTS settings and available providers.
        /// </summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings([FromQuery] Guid? userId = null)
        {            try            {
                var apiKey = await _settingsService.GetElevenLabsApiKeyAsync(userId);
                var voiceId = await _settingsService.GetElevenLabsVoiceIdAsync(userId);
                var fishSpeechApiKey = await _settingsService.GetFishSpeechApiKeyAsync(userId);
                var ttsProvider = await _settingsService.GetSettingAsync(userId, "TtsProvider") ?? "elevenlabs";
                
                // Check provider availability
                var providers = new List<object>();
                
                // Always add ElevenLabs
                providers.Add(new { 
                    id = "elevenlabs", 
                    name = "ElevenLabs", 
                    available = !string.IsNullOrEmpty(apiKey) 
                });
                
                // Check Fish Speech availability
                var fishSpeechAvailable = await _fishSpeechService.IsAvailableAsync();
                providers.Add(new { 
                    id = "fishspeech", 
                    name = "Fish Speech", 
                    available = fishSpeechAvailable 
                });

                return Ok(new { 
                    apiKey, 
                    voiceId, 
                    fishSpeechApiKey,
                    ttsProvider,
                    providers 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting TTS settings for user {UserId}", userId);
                return StatusCode(500, "Failed to retrieve TTS settings");
            }
        }        /// <summary>
        /// Updates the user's TTS settings (API key, voice, and provider).
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
                if (!string.IsNullOrEmpty(request.FishSpeechApiKey))
                    settings["FishSpeechApiKey"] = request.FishSpeechApiKey;
                if (!string.IsNullOrEmpty(request.TtsProvider))
                    settings["TtsProvider"] = request.TtsProvider;

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
        /// Synthesizes speech from text using the configured TTS provider.
        /// </summary>
        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] SynthesizeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest("Text is required for TTS synthesis.");
            }

            try            {
                // Get user's TTS provider preference
                var ttsProvider = await _settingsService.GetSettingAsync(request.UserId, "TtsProvider") ?? "elevenlabs";
                
                byte[] audioBytes;
                string contentType;                if (ttsProvider == "fishspeech")
                {
                    // Use Fish Speech
                    var voiceId = request.VoiceId ?? "default";
                    audioBytes = await _fishSpeechService.SynthesizeAsync(request.Text, voiceId, request.UserId);
                    contentType = "audio/wav";
                }
                else
                {
                    // Use ElevenLabs (default)
                    var apiKey = await _settingsService.GetElevenLabsApiKeyAsync(request.UserId);
                    var voiceId = request.VoiceId ?? await _settingsService.GetElevenLabsVoiceIdAsync(request.UserId);

                    if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceId))
                    {
                        return BadRequest("Missing ElevenLabs API key or voice configuration.");
                    }

                    audioBytes = await _elevenLabsService.SynthesizeAsync(request.Text, voiceId);
                    contentType = "audio/mpeg";
                }

                return File(audioBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synthesizing audio for user {UserId}", request.UserId);
                return StatusCode(500, "Error synthesizing audio");
            }
        }        /// <summary>
        /// Gets available voices for the specified TTS provider.
        /// </summary>
        [HttpGet("voices")]
        public async Task<IActionResult> GetVoices([FromQuery] string provider = "elevenlabs", [FromQuery] Guid? userId = null)
        {
            try
            {
                if (provider == "fishspeech")
                {
                    var voices = await _fishSpeechService.GetAvailableVoicesAsync();
                    return Ok(new { voices });
                }
                else
                {
                    // ElevenLabs voices (you can implement this if needed)
                    var defaultVoices = new[] { "Rachel", "Domi", "Bella", "Antoni" };
                    return Ok(new { voices = defaultVoices });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting voices for provider {Provider}", provider);
                return StatusCode(500, "Error getting voices");
            }
        }

        /// <summary>
        /// Uploads a custom voice for Fish Speech TTS.
        /// Requires both an audio file (.wav) and transcript (.txt).
        /// </summary>
        [HttpPost("voices/upload")]
        public async Task<IActionResult> UploadVoice([FromForm] UploadVoiceRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request is required.");
            }

            if (string.IsNullOrWhiteSpace(request.VoiceName))
            {
                return BadRequest("Voice name is required.");
            }

            if (request.AudioFile == null || request.AudioFile.Length == 0)
            {
                return BadRequest("Audio file is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Transcript))
            {
                return BadRequest("Transcript is required.");
            }

            try
            {
                // Validate audio file format
                if (!request.AudioFile.FileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Audio file must be in WAV format.");
                }

                // Sanitize voice name (remove invalid characters)
                var sanitizedVoiceName = SanitizeFileName(request.VoiceName);
                if (string.IsNullOrWhiteSpace(sanitizedVoiceName))
                {
                    return BadRequest("Voice name contains invalid characters.");
                }

                var result = await _fishSpeechService.UploadVoiceAsync(sanitizedVoiceName, request.AudioFile, request.Transcript);
                
                if (result)
                {
                    _logger.LogInformation("Voice '{VoiceName}' uploaded successfully", sanitizedVoiceName);
                    return Ok(new { message = "Voice uploaded successfully", voiceName = sanitizedVoiceName });
                }
                else
                {
                    return StatusCode(500, "Failed to upload voice");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading voice '{VoiceName}'", request.VoiceName);
                return StatusCode(500, "Error uploading voice");
            }
        }

        /// <summary>
        /// Deletes a custom voice from Fish Speech TTS.
        /// </summary>
        [HttpDelete("voices/{voiceName}")]
        public async Task<IActionResult> DeleteVoice(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName))
            {
                return BadRequest("Voice name is required.");
            }

            try
            {
                var result = await _fishSpeechService.DeleteVoiceAsync(voiceName);
                
                if (result)
                {
                    _logger.LogInformation("Voice '{VoiceName}' deleted successfully", voiceName);
                    return Ok(new { message = "Voice deleted successfully" });
                }
                else
                {
                    return NotFound("Voice not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting voice '{VoiceName}'", voiceName);
                return StatusCode(500, "Error deleting voice");
            }
        }

        /// <summary>
        /// Gets details about a specific voice.
        /// </summary>
        [HttpGet("voices/{voiceName}")]
        public async Task<IActionResult> GetVoiceDetails(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName))
            {
                return BadRequest("Voice name is required.");
            }

            try
            {
                var details = await _fishSpeechService.GetVoiceDetailsAsync(voiceName);
                
                if (details != null)
                {
                    return Ok(details);
                }
                else
                {
                    return NotFound("Voice not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting details for voice '{VoiceName}'", voiceName);
                return StatusCode(500, "Error getting voice details");
            }
        }

        /// <summary>
        /// Sanitizes a filename by removing invalid characters.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim();
        }
    }    /// <summary>
    /// Request model for updating TTS settings.
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
        public string? ApiKey { get; set; }

        /// <summary>
        /// New voice ID (leave null to keep existing).
        /// </summary>
        public string? VoiceId { get; set; }

        /// <summary>
        /// New Fish Speech API key (leave null to keep existing).
        /// </summary>
        public string? FishSpeechApiKey { get; set; }

        /// <summary>
        /// TTS provider preference ("elevenlabs" or "fishspeech").
        /// </summary>
        public string? TtsProvider { get; set; }
    }/// <summary>
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
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Override voice ID for this request (optional).
        /// </summary>
        public string? VoiceId { get; set; }
    }

    /// <summary>
    /// Request model for uploading a custom voice.
    /// </summary>
    public class UploadVoiceRequest
    {
        /// <summary>
        /// The name for the custom voice.
        /// </summary>
        public string VoiceName { get; set; } = string.Empty;

        /// <summary>
        /// The audio file (.wav format) for voice cloning.
        /// </summary>
        public IFormFile? AudioFile { get; set; }

        /// <summary>
        /// The transcript text corresponding to the audio file.
        /// </summary>
        public string Transcript { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for voice details.
    /// </summary>
    public class VoiceDetailsResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Transcript { get; set; } = string.Empty;
        public bool HasAudioFile { get; set; }
        public bool HasEmbedding { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? AudioFileSize { get; set; }
    }
}
