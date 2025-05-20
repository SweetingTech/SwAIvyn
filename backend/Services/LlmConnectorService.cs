using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for connecting to local LLM engines such as Ollama and LM Studio.
    /// </summary>
    public interface ILlmConnectorService
    {
        /// <summary>
        /// Lists the Ollama models that are currently available on the local Ollama server.
        /// </summary>
        Task<IEnumerable<string>> GetOllamaModelsAsync(Guid? userId = null);

        /// <summary>
        /// Gets the name of the model currently loaded in LM Studio.
        /// </summary>
        Task<string> GetLmStudioModelAsync(Guid? userId = null);

        /// <summary>
        /// Sends a prompt to the chosen engine+model and returns the completion.
        /// </summary>
        /// <param name="prompt">The prompt text to send to the model.</param>
        /// <param name="engine">The engine to use ("ollama" or "lmstudio").</param>
        /// <param name="model">The model name to use (optional for Ollama).</param>
        /// <param name="userId">User ID for user-specific settings.</param>
        /// <returns>The generated completion text.</returns>
        Task<string> GenerateResponseAsync(string prompt, string engine = "ollama", string model = null, Guid? userId = null);
    }

    public class LlmConnectorService : ILlmConnectorService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configurationService;
        private readonly ISimpleLoggerService _logger;

        public LlmConnectorService(
            IConfigurationService configurationService,
            ISimpleLoggerService logger)
        {
            _httpClient = new HttpClient();
            _configurationService = configurationService;
            _logger = logger;
        }

        public async Task<IEnumerable<string>> GetOllamaModelsAsync(Guid? userId = null)
        {
            try
            {
                // Get the Ollama API URL from configuration
                var ollamaApiUrl = _configurationService.GetOllamaApiUrl();
                _logger.LogInfo($"Using Ollama API URL: {ollamaApiUrl}");

                // Ollama returns a list of model objects; we map to their names
                var models = await _httpClient.GetFromJsonAsync<List<OllamaModel>>($"{ollamaApiUrl}/v1/models");
                return models?.ConvertAll(m => m.Name) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get Ollama models: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<string> GetLmStudioModelAsync(Guid? userId = null)
        {
            try
            {
                // Get the LM Studio API URL from configuration
                var lmStudioApiUrl = _configurationService.GetLmStudioApiUrl();
                _logger.LogInfo($"Using LM Studio API URL: {lmStudioApiUrl}");

                // LM Studio uses OpenAI-compatible API
                try
                {
                    // Try the v1/models endpoint (OpenAI compatible)
                    var models = await _httpClient.GetFromJsonAsync<LmStudioModelsResponse>($"{lmStudioApiUrl}/v1/models");
                    if (models?.Data?.Count > 0)
                    {
                        return models.Data[0].Id;
                    }
                }
                catch (Exception modelEx)
                {
                    _logger.LogWarning($"Failed to get LM Studio models from /v1/models: {modelEx.Message}");
                    // Fall back to the old endpoint
                }

                // Fall back to the old endpoint if the OpenAI-compatible one fails
                var result = await _httpClient.GetFromJsonAsync<LmStudioModelInfo>($"{lmStudioApiUrl}/model");
                return result?.Name ?? throw new Exception("Unable to fetch LM Studio model");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get LM Studio model: {ex.Message}");
                return "Unknown Model";
            }
        }

        public async Task<string> GenerateResponseAsync(string prompt, string engine = "ollama", string model = null, Guid? userId = null)
        {
            try
            {
                engine = engine?.ToLowerInvariant();
                if (engine == "ollama")
                {
                    // Get the Ollama API URL from configuration
                    var ollamaApiUrl = _configurationService.GetOllamaApiUrl();
                    _logger.LogInfo($"Using Ollama API URL: {ollamaApiUrl}");

                    // If no model passed, pick the first available one
                    if (string.IsNullOrEmpty(model))
                    {
                        var available = await GetOllamaModelsAsync(userId);
                        model = available is null || !available.GetEnumerator().MoveNext()
                          ? throw new Exception("No Ollama models available")
                          : System.Linq.Enumerable.First(available);
                    }

                    var request = new
                    {
                        prompt = prompt,
                        model = model
                    };
                    var response = await _httpClient.PostAsJsonAsync($"{ollamaApiUrl}/v1/completions", request);
                    if (!response.IsSuccessStatusCode)
                        return $"Ollama API error: {response.StatusCode}";

                    var result = await response.Content.ReadFromJsonAsync<OllamaCompletionResponse>();
                    return result?.Completion ?? "No response from Ollama";
                }
                else if (engine == "lmstudio")
                {
                    // Get the LM Studio API URL from configuration
                    var lmStudioApiUrl = _configurationService.GetLmStudioApiUrl();
                    _logger.LogInfo($"Using LM Studio API URL: {lmStudioApiUrl}");

                    try
                    {
                        // Try the OpenAI-compatible endpoint first
                        var openAiRequest = new
                        {
                            model = model ?? "default", // Use the provided model or "default"
                            messages = new[]
                            {
                                new { role = "user", content = prompt }
                            },
                            temperature = 0.7,
                            max_tokens = 1000
                        };

                        var openAiResponse = await _httpClient.PostAsJsonAsync($"{lmStudioApiUrl}/v1/chat/completions", openAiRequest);
                        if (openAiResponse.IsSuccessStatusCode)
                        {
                            var openAiResult = await openAiResponse.Content.ReadFromJsonAsync<OpenAiCompletionResponse>();
                            if (openAiResult?.Choices?.Count > 0)
                            {
                                return openAiResult.Choices[0].Message.Content;
                            }
                        }
                    }
                    catch (Exception openAiEx)
                    {
                        _logger.LogWarning($"Failed to use OpenAI-compatible endpoint: {openAiEx.Message}");
                        // Fall back to the old endpoint
                    }

                    // Fall back to the old endpoint
                    _logger.LogInfo("Falling back to legacy LM Studio endpoint");
                    var request = new { prompt = prompt };
                    var response = await _httpClient.PostAsJsonAsync($"{lmStudioApiUrl}/generate", request);
                    if (!response.IsSuccessStatusCode)
                        return $"LM Studio API error: {response.StatusCode}";

                    var result = await response.Content.ReadFromJsonAsync<LmStudioGenerateResponse>();
                    return result?.Text ?? "No response from LM Studio";
                }
                else
                {
                    return $"Unsupported engine '{engine}'";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to generate response: {ex.Message}");
                return $"Error generating response: {ex.Message}";
            }
        }

        // DTOs for the various endpoints:
        private class OllamaModel
        {
            public string Name { get; set; } = string.Empty;
            // other fields omitted
        }

        private class OllamaCompletionResponse
        {
            public string Completion { get; set; } = string.Empty;
        }

        private class LmStudioModelInfo
        {
            public string Name { get; set; } = string.Empty;
        }

        private class LmStudioGenerateResponse
        {
            public string Text { get; set; } = string.Empty;
        }

        private class LmStudioModelsResponse
        {
            public List<LmStudioModelData> Data { get; set; } = new List<LmStudioModelData>();
        }

        private class LmStudioModelData
        {
            public string Id { get; set; } = string.Empty;
            public string Object { get; set; } = string.Empty;
            public string OwnedBy { get; set; } = string.Empty;
        }

        private class OpenAiCompletionResponse
        {
            public List<OpenAiChoice> Choices { get; set; } = new List<OpenAiChoice>();
        }

        private class OpenAiChoice
        {
            public OpenAiMessage Message { get; set; } = new OpenAiMessage();
        }

        private class OpenAiMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }
    }
}
