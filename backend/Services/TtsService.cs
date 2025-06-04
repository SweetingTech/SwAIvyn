using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for text-to-speech services.
    /// </summary>
    public interface ITtsService
    {
        /// <summary>
        /// Synthesizes speech for the given text using the specified voice.
        /// </summary>
        /// <param name="text">Text to speak.</param>
        /// <param name="voiceId">Optional voice ID.</param>
        /// <returns>Audio data bytes.</returns>
        Task<byte[]> SynthesizeAsync(string text, string? voiceId = null);
    }

    /// <summary>
    /// ElevenLabs implementation of text-to-speech service.
    /// </summary>
    public class ElevenLabsTtsService : ITtsService
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsProvider _settingsProvider;
        private readonly ISimpleLoggerService _logger;

        public ElevenLabsTtsService(HttpClient httpClient,
            ISettingsProvider settingsProvider,
            ISimpleLoggerService logger)
        {
            _httpClient = httpClient;
            _settingsProvider = settingsProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<byte[]> SynthesizeAsync(string text, string? voiceId = null)
        {
            var apiKey = _settingsProvider.GetElevenLabsApiKey();
            voiceId ??= _settingsProvider.GetElevenLabsVoiceId();

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("ElevenLabs API key is not configured.");
            if (string.IsNullOrWhiteSpace(voiceId))
                throw new InvalidOperationException("ElevenLabs voice ID is not configured.");

            var requestBody = JsonSerializer.Serialize(new { text });
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}")
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("xi-api-key", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"ElevenLabs API returned {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                throw new Exception($"ElevenLabs API error: {response.StatusCode}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}
