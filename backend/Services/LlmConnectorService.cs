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
        Task<IEnumerable<string>> GetOllamaModelsAsync();

        /// <summary>
        /// Gets the name of the model currently loaded in LM Studio.
        /// </summary>
        Task<string> GetLmStudioModelAsync();

        /// <summary>
        /// Sends a prompt to the chosen engine+model and returns the completion.
        /// </summary>
        /// <param name="prompt">The prompt text to send to the model.</param>
        /// <param name="engine">The engine to use ("ollama" or "lmstudio").</param>
        /// <param name="model">The model name to use (optional for Ollama).</param>
        /// <returns>The generated completion text.</returns>
        Task<string> GenerateResponseAsync(string prompt, string engine = "ollama", string model = null);
    }

    public class LlmConnectorService : ILlmConnectorService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaApiUrl;
        private readonly string _lmStudioApiUrl;

        public LlmConnectorService(IConfiguration configuration)
        {
            _httpClient     = new HttpClient();
            _ollamaApiUrl   = configuration["AppSettings:OllamaApiUrl"];
            _lmStudioApiUrl = configuration["AppSettings:LmStudioApiUrl"];
        }

        public async Task<IEnumerable<string>> GetOllamaModelsAsync()
        {
            // Ollama returns a list of model objects; we map to their names
            var models = await _httpClient.GetFromJsonAsync<List<OllamaModel>>($"{_ollamaApiUrl}/v1/models");
            return models?.ConvertAll(m => m.Name) ?? Array.Empty<string>();
        }

        public async Task<string> GetLmStudioModelAsync()
        {
            // LM Studio exposes its loaded model; adjust endpoint if needed
            var result = await _httpClient.GetFromJsonAsync<LmStudioModelInfo>($"{_lmStudioApiUrl}/model");
            return result?.Name ?? throw new Exception("Unable to fetch LM Studio model");
        }

        public async Task<string> GenerateResponseAsync(string prompt, string engine = "ollama", string model = null)
        {
            engine = engine?.ToLowerInvariant();
            if (engine == "ollama")
            {
                // If no model passed, pick the first available one
                if (string.IsNullOrEmpty(model))
                {
                    var available = await GetOllamaModelsAsync();
                    model = available is null || !available.GetEnumerator().MoveNext()
                      ? throw new Exception("No Ollama models available")
                      : System.Linq.Enumerable.First(available);
                }

                var request = new
                {
                    prompt = prompt,
                    model  = model
                };
                var response = await _httpClient.PostAsJsonAsync($"{_ollamaApiUrl}/v1/completions", request);
                if (!response.IsSuccessStatusCode)
                    return $"Ollama API error: {response.StatusCode}";

                var result = await response.Content.ReadFromJsonAsync<OllamaCompletionResponse>();
                return result?.Completion ?? "No response from Ollama";
            }
            else if (engine == "lmstudio")
            {
                // LM Studio uses its single loaded model
                var request = new { prompt = prompt };
                var response = await _httpClient.PostAsJsonAsync($"{_lmStudioApiUrl}/generate", request);
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

        // DTOs for the various endpoints:
        private class OllamaModel
        {
            public string Name { get; set; }
            // other fields omitted
        }

        private class OllamaCompletionResponse
        {
            public string Completion { get; set; }
        }

        private class LmStudioModelInfo
        {
            public string Name { get; set; }
        }

        private class LmStudioGenerateResponse
        {
            public string Text { get; set; }
        }
    }
}
