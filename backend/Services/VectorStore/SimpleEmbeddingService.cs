using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwAIvyn.Services.VectorStore
{
    /// <summary>
    /// Simple embedding service that uses a local LLM server for embeddings
    /// </summary>
    public class SimpleEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configurationService;
        private readonly ISimpleLoggerService _logger;
        private readonly IConfiguration _configuration;
        private readonly int _dimensions;

        /// <summary>
        /// Initializes a new instance of the SimpleEmbeddingService
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="configurationService">Configuration service</param>
        /// <param name="logger">Logger service</param>
        public SimpleEmbeddingService(
            IConfiguration configuration,
            IConfigurationService configurationService,
            ISimpleLoggerService logger)
        {
            _httpClient = new HttpClient();
            _configurationService = configurationService;
            _logger = logger;
            _configuration = configuration;

            _dimensions = configuration.GetValue<int>("AppSettings:VectorDimensions", 768);
        }

        /// <inheritdoc/>
        public int Dimensions => _dimensions;

        /// <inheritdoc/>
        public async Task<float[]> EmbedTextAsync(string text)
        {
            try
            {
                // For now, we'll use a simple hash-based embedding for testing
                // In a real implementation, this would call an embedding model
                if (string.IsNullOrEmpty(text))
                {
                    return new float[_dimensions];
                }

                // Create a deterministic embedding based on the text hash
                var embedding = new float[_dimensions];
                var hash = text.GetHashCode();
                var random = new Random(hash);

                for (int i = 0; i < _dimensions; i++)
                {
                    embedding[i] = (float)(random.NextDouble() * 2 - 1); // Values between -1 and 1
                }

                // Normalize the embedding
                var magnitude = 0.0f;
                for (int i = 0; i < _dimensions; i++)
                {
                    magnitude += embedding[i] * embedding[i];
                }
                magnitude = (float)Math.Sqrt(magnitude);

                for (int i = 0; i < _dimensions; i++)
                {
                    embedding[i] /= magnitude;
                }

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to generate embedding", ex);
                return new float[_dimensions]; // Return zero vector on error
            }
        }

        /// <summary>
        /// In a real implementation, this would call an embedding model API
        /// </summary>
        private async Task<float[]> CallEmbeddingApiAsync(string text)
        {
            try
            {
                // Get the Ollama API URL from configuration
                var ollamaApiUrl = _configurationService.GetOllamaApiUrl();
                var embeddingEndpoint = $"{ollamaApiUrl}/api/embeddings";
                _logger.LogInfo($"Using embedding endpoint: {embeddingEndpoint}");

                var request = new
                {
                    model = "all-minilm",
                    prompt = text
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(embeddingEndpoint, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson);

                return responseObj?.Embedding ?? new float[_dimensions];
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to call embedding API", ex);
                return new float[_dimensions]; // Return zero vector on error
            }
        }

        private class EmbeddingResponse
        {
            public float[] Embedding { get; set; }
        }
    }
}
