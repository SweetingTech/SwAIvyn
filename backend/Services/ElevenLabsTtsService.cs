using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// ElevenLabs text-to-speech service implementation.
    /// </summary>
    public class ElevenLabsTtsService : ITtsService
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsProvider _settingsProvider;
        private readonly ISimpleLoggerService _logger;

        public ElevenLabsTtsService(
            HttpClient httpClient,
            ISettingsProvider settingsProvider,
            ISimpleLoggerService logger)
        {
            _httpClient = httpClient;
            _settingsProvider = settingsProvider;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<byte[]> SynthesizeAsync(string text, string? voiceId = null)
        {
            try
            {
                var apiKey = _settingsProvider.GetElevenLabsApiKey();
                voiceId ??= _settingsProvider.GetElevenLabsVoiceId();

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceId))
                {
                    throw new InvalidOperationException("ElevenLabs API key or voice ID not configured.");
                }

                var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";
                var requestJson = JsonSerializer.Serialize(new { text });
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                request.Headers.Add("xi-api-key", apiKey);

                _logger.LogInfo($"[TTS REQUEST] POST {url} (text length {text.Length})");
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"ElevenLabs TTS failed: {(int)response.StatusCode} {response.StatusCode} - {err}");
                    throw new Exception($"ElevenLabs API error: {response.StatusCode}");
                }

                _logger.LogInfo("[TTS RESPONSE] Success");
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to synthesize speech", ex);
                throw;
            }
        }
    }
}
