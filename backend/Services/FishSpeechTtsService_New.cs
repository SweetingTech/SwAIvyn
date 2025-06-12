using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwAIvyn.Configuration;
using SwAIvyn.Controllers;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Fish Speech text-to-speech service implementation.
    /// </summary>
    public class FishSpeechTtsService : ITtsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FishSpeechTtsService> _logger;
        private readonly FishSpeechOptions _options;
        private readonly string _fishSpeechBaseUrl;
        private readonly IConfigurationService _configurationService;

        public FishSpeechTtsService(
            HttpClient httpClient,
            ILogger<FishSpeechTtsService> logger,
            IOptions<FishSpeechOptions> options,
            IConfigurationService configurationService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _options = options.Value;
            _fishSpeechBaseUrl = _options.BaseUrl;
            _configurationService = configurationService;
            
            // Configure HTTP client timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }

        /// <summary>
        /// Add authorization header if Fish Speech API key is available
        /// </summary>
        private async Task AddAuthenticationAsync(HttpRequestMessage request, Guid? userId = null)
        {
            try
            {
                var apiKey = await _configurationService.GetFishSpeechApiKey(userId);
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                    _logger.LogInformation("Added Fish Speech API key authentication to request");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add Fish Speech API key authentication");
                // Continue without authentication - fallback to local service
            }
        }

        /// <inheritdoc />
        public async Task<byte[]> SynthesizeAsync(string text, string? voiceId = null)
        {
            return await SynthesizeAsync(text, voiceId, null);
        }

        /// <summary>
        /// Synthesize speech with optional user-specific authentication
        /// </summary>
        public async Task<byte[]> SynthesizeAsync(string text, string? voiceId = null, Guid? userId = null)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text cannot be null or empty", nameof(text));
            }

            if (text.Length > _options.MaxTextLength)
            {
                throw new ArgumentException($"Text length ({text.Length}) exceeds maximum allowed length ({_options.MaxTextLength})", nameof(text));
            }

            try
            {
                string endpoint;
                HttpContent content;

                if (string.IsNullOrEmpty(voiceId) || voiceId == "default")
                {
                    // Use default Fish Speech TTS
                    endpoint = $"{_fishSpeechBaseUrl}/tts";
                    var formParams = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("text", text)
                    });
                    content = formParams;
                }
                else
                {
                    // Use voice cloning with specific voice
                    endpoint = $"{_fishSpeechBaseUrl}/tts/clone";
                    var formParams = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("text", text),
                        new KeyValuePair<string, string>("voice_name", voiceId)
                    });
                    content = formParams;
                }

                _logger.LogInformation($"[Fish Speech TTS REQUEST] POST {endpoint} (text length {text.Length})");

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Content = content;
                
                // Add authentication if API key is available
                await AddAuthenticationAsync(request, userId);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Fish Speech TTS failed: {(int)response.StatusCode} {response.StatusCode} - {errorContent}");
                    throw new Exception($"Fish Speech API error: {response.StatusCode}");
                }

                _logger.LogInformation("[Fish Speech TTS RESPONSE] Success");
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fish Speech TTS synthesis failed");
                throw;
            }
        }

        /// <summary>
        /// Get available voices from Fish Speech API
        /// </summary>
        public async Task<string[]> GetAvailableVoicesAsync(Guid? userId = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{_fishSpeechBaseUrl}/voices");
                await AddAuthenticationAsync(request, userId);
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get Fish Speech voices: {response.StatusCode}");
                    return new[] { "default" };
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var voicesResponse = JsonSerializer.Deserialize<VoicesResponse>(jsonContent);
                
                return voicesResponse?.voices ?? new[] { "default" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Fish Speech voices");
                return new[] { "default" };
            }
        }

        /// <summary>
        /// Check if Fish Speech API is available
        /// </summary>
        public async Task<bool> IsAvailableAsync(Guid? userId = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{_fishSpeechBaseUrl}/voices");
                await AddAuthenticationAsync(request, userId);
                
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Upload a custom voice to Fish Speech
        /// </summary>
        public async Task<bool> UploadVoiceAsync(string voiceName, IFormFile audioFile, string transcript)
        {
            try
            {
                // Determine the voices directory path
                var voicesDirectory = GetVoicesDirectory();
                
                if (!Directory.Exists(voicesDirectory))
                {
                    Directory.CreateDirectory(voicesDirectory);
                    _logger.LogInformation("Created voices directory: {Directory}", voicesDirectory);
                }

                var audioFilePath = Path.Combine(voicesDirectory, $"{voiceName}.wav");
                var transcriptFilePath = Path.Combine(voicesDirectory, $"{voiceName}.txt");

                // Save the audio file
                using (var stream = new FileStream(audioFilePath, FileMode.Create))
                {
                    await audioFile.CopyToAsync(stream);
                }

                // Save the transcript file
                await File.WriteAllTextAsync(transcriptFilePath, transcript, System.Text.Encoding.UTF8);

                _logger.LogInformation("Voice '{VoiceName}' uploaded successfully. Audio: {AudioPath}, Transcript: {TranscriptPath}", 
                    voiceName, audioFilePath, transcriptFilePath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading voice '{VoiceName}'", voiceName);
                return false;
            }
        }

        /// <summary>
        /// Delete a custom voice from Fish Speech
        /// </summary>
        public async Task<bool> DeleteVoiceAsync(string voiceName)
        {
            try
            {
                var voicesDirectory = GetVoicesDirectory();
                var audioFilePath = Path.Combine(voicesDirectory, $"{voiceName}.wav");
                var transcriptFilePath = Path.Combine(voicesDirectory, $"{voiceName}.txt");
                var embeddingFilePath = Path.Combine(voicesDirectory, $"{voiceName}.pt");

                bool deleted = false;

                // Delete audio file if exists
                if (File.Exists(audioFilePath))
                {
                    File.Delete(audioFilePath);
                    deleted = true;
                    _logger.LogInformation("Deleted audio file: {AudioPath}", audioFilePath);
                }

                // Delete transcript file if exists
                if (File.Exists(transcriptFilePath))
                {
                    File.Delete(transcriptFilePath);
                    deleted = true;
                    _logger.LogInformation("Deleted transcript file: {TranscriptPath}", transcriptFilePath);
                }

                // Delete embedding file if exists
                if (File.Exists(embeddingFilePath))
                {
                    File.Delete(embeddingFilePath);
                    deleted = true;
                    _logger.LogInformation("Deleted embedding file: {EmbeddingPath}", embeddingFilePath);
                }

                return deleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting voice '{VoiceName}'", voiceName);
                return false;
            }
        }

        /// <summary>
        /// Get details about a specific voice
        /// </summary>
        public async Task<VoiceDetailsResponse?> GetVoiceDetailsAsync(string voiceName)
        {
            try
            {
                var voicesDirectory = GetVoicesDirectory();
                var audioFilePath = Path.Combine(voicesDirectory, $"{voiceName}.wav");
                var transcriptFilePath = Path.Combine(voicesDirectory, $"{voiceName}.txt");
                var embeddingFilePath = Path.Combine(voicesDirectory, $"{voiceName}.pt");

                var hasAudioFile = File.Exists(audioFilePath);
                var hasTranscriptFile = File.Exists(transcriptFilePath);
                var hasEmbedding = File.Exists(embeddingFilePath);

                if (!hasAudioFile && !hasTranscriptFile)
                {
                    return null; // Voice doesn't exist
                }

                var transcript = hasTranscriptFile ? await File.ReadAllTextAsync(transcriptFilePath) : "";
                var audioFileSize = hasAudioFile ? new FileInfo(audioFilePath).Length : (long?)null;
                var createdAt = hasAudioFile ? File.GetCreationTime(audioFilePath) : (DateTime?)null;

                return new VoiceDetailsResponse
                {
                    Name = voiceName,
                    Transcript = transcript,
                    HasAudioFile = hasAudioFile,
                    HasEmbedding = hasEmbedding,
                    CreatedAt = createdAt,
                    AudioFileSize = audioFileSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting details for voice '{VoiceName}'", voiceName);
                return null;
            }
        }

        /// <summary>
        /// Get the path to the voices directory
        /// </summary>
        private string GetVoicesDirectory()
        {
            // Use the model path from options, fallback to relative path
            var basePath = !string.IsNullOrEmpty(_options.ModelPath) 
                ? _options.ModelPath 
                : Path.Combine(Directory.GetCurrentDirectory(), "..", "speech", "TTS", "openaudio-s1-mini");
                
            return Path.Combine(basePath, "voices");
        }

        private class VoicesResponse
        {
            public string[] voices { get; set; } = new string[0];
        }
    }
}
