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
        /// Gets the list of available OpenAI models.
        /// </summary>
        Task<IEnumerable<string>> GetOpenAiModelsAsync(Guid? userId = null);

        /// <summary>
        /// Gets the list of available Claude models.
        /// </summary>
        Task<IEnumerable<string>> GetClaudeModelsAsync(Guid? userId = null);

        /// <summary>
        /// Sends structured messages to the chosen engine+model and returns the completion.
        /// </summary>
        /// <param name="messages">The messages array with role/content structure.</param>
        /// <param name="engine">The engine to use ("ollama" or "lmstudio").</param>
        /// <param name="model">The model name to use (optional for Ollama).</param>
        /// <param name="userId">User ID for user-specific settings.</param>
        /// <returns>The generated completion text.</returns>
        Task<string> GenerateResponseAsync(List<Dictionary<string, string>> messages, string engine = "ollama", string model = null, Guid? userId = null);

        /// <summary>
        /// Sends a prompt to the chosen engine+model and returns the completion. (Legacy method)
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

                // Ollama uses /api/tags endpoint, not /v1/models
                var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>($"{ollamaApiUrl}/api/tags");
                return response?.Models?.Select(m => m.Name) ?? new List<string>();
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

        public async Task<IEnumerable<string>> GetOpenAiModelsAsync(Guid? userId = null)
        {
            try
            {
                var apiUrl = _configurationService.GetOpenAiApiUrl();
                var apiKey = _configurationService.GetOpenAiApiKey();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/models");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<OpenAiModelsResponse>();
                return result?.Data?.Select(m => m.Id) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get OpenAI models: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<IEnumerable<string>> GetClaudeModelsAsync(Guid? userId = null)
        {
            try
            {
                var apiUrl = _configurationService.GetClaudeApiUrl();
                var apiKey = _configurationService.GetClaudeApiKey();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/models");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ClaudeModelsResponse>();
                return result?.Models ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get Claude models: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<string> GenerateResponseAsync(List<Dictionary<string, string>> messages, string engine = "ollama", string model = null, Guid? userId = null)
        {
            try
            {
                engine = engine?.ToLowerInvariant();
                if (engine == "ollama")
                {
                    // For Ollama, convert messages to a single prompt
                    var prompt = ConvertMessagesToPrompt(messages);
                    return await GenerateResponseAsync(prompt, engine, model, userId);
                }
                else if (engine == "lmstudio")
                {
                    // Get the LM Studio API URL from configuration
                    var lmStudioApiUrl = _configurationService.GetLmStudioApiUrl();
                    _logger.LogInfo($"Using LM Studio API URL: {lmStudioApiUrl}");

                    try
                    {
                        // Use the structured messages directly for LM Studio
                        var openAiRequest = new
                        {
                            model = model ?? "default",
                            messages = messages.Select(m => new { role = m["role"], content = m["content"] }).ToArray(),
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
                        // Fall back to the legacy method
                        var prompt = ConvertMessagesToPrompt(messages);
                        return await GenerateResponseAsync(prompt, engine, model, userId);
                    }

                    return "No response from LM Studio";
                }
                else if (engine == "openai")
                {
                    var apiUrl = _configurationService.GetOpenAiApiUrl();
                    var apiKey = _configurationService.GetOpenAiApiKey();
                    var openAiRequest = new
                    {
                        model = model ?? "gpt-3.5-turbo",
                        messages = messages.Select(m => new { role = m["role"], content = m["content"] }).ToArray(),
                        temperature = 0.7,
                        max_tokens = 1000
                    };
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/chat/completions");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = JsonContent.Create(openAiRequest);
                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<OpenAiCompletionResponse>();
                        if (result?.Choices?.Count > 0)
                            return result.Choices[0].Message.Content;
                    }
                    return "No response from OpenAI";
                }
                else if (engine == "claude")
                {
                    var apiUrl = _configurationService.GetClaudeApiUrl();
                    var apiKey = _configurationService.GetClaudeApiKey();
                    var claudeRequest = new
                    {
                        model = model ?? "claude-3-sonnet-20240229",
                        messages = messages.Select(m => new { role = m["role"], content = m["content"] }).ToArray(),
                        temperature = 0.7,
                        max_tokens = 1000
                    };
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/messages");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    request.Headers.Add("anthropic-version", "2023-06-01");
                    request.Content = JsonContent.Create(claudeRequest);
                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<ClaudeCompletionResponse>();
                        if (result?.Content?.Count > 0)
                            return result.Content[0].Text;
                    }
                    return "No response from Claude";
                }
                else
                {
                    return $"Unsupported engine '{engine}'";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating response: {ex.Message}");
                return $"Error: {ex.Message}";
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
                        model = model,
                        prompt = prompt,
                        stream = false
                    };
                    var response = await _httpClient.PostAsJsonAsync($"{ollamaApiUrl}/api/generate", request);
                    if (!response.IsSuccessStatusCode)
                        return $"Ollama API error: {response.StatusCode}";

                    var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
                    return result?.Response ?? "No response from Ollama";
                }
                else if (engine == "lmstudio")
                {
                    // Get the LM Studio API URL from configuration
                    var lmStudioApiUrl = _configurationService.GetLmStudioApiUrl();
                    _logger.LogInfo($"Using LM Studio API URL: {lmStudioApiUrl}");

                    try
                    {
                        // Convert the legacy prompt to structured messages for OpenAI compatibility
                        var messages = ConvertLegacyPromptToMessages(prompt);

                        // Try the OpenAI-compatible endpoint first
                        var openAiRequest = new
                        {
                            model = model ?? "default", // Use the provided model or "default"
                            messages = messages,
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
                else if (engine == "openai")
                {
                    var apiUrl = _configurationService.GetOpenAiApiUrl();
                    var apiKey = _configurationService.GetOpenAiApiKey();
                    var messages = ConvertLegacyPromptToMessages(prompt);
                    var openAiRequest = new { model = model ?? "gpt-3.5-turbo", messages, temperature = 0.7, max_tokens = 1000 };
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/chat/completions");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = JsonContent.Create(openAiRequest);
                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<OpenAiCompletionResponse>();
                        if (result?.Choices?.Count > 0)
                            return result.Choices[0].Message.Content;
                    }
                    return "No response from OpenAI";
                }
                else if (engine == "claude")
                {
                    var apiUrl = _configurationService.GetClaudeApiUrl();
                    var apiKey = _configurationService.GetClaudeApiKey();
                    var messages = ConvertLegacyPromptToMessages(prompt);
                    var claudeRequest = new { model = model ?? "claude-3-sonnet-20240229", messages, temperature = 0.7, max_tokens = 1000 };
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/messages");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    request.Headers.Add("anthropic-version", "2023-06-01");
                    request.Content = JsonContent.Create(claudeRequest);
                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<ClaudeCompletionResponse>();
                        if (result?.Content?.Count > 0)
                            return result.Content[0].Text;
                    }
                    return "No response from Claude";
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
        private class OllamaTagsResponse
        {
            public List<OllamaModel> Models { get; set; } = new List<OllamaModel>();
        }

        private class OllamaModel
        {
            public string Name { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public DateTime ModifiedAt { get; set; }
            public long Size { get; set; }
            // other fields omitted
        }

        private class OllamaGenerateResponse
        {
            public string Response { get; set; } = string.Empty;
            public bool Done { get; set; }
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

        private class OpenAiModelsResponse
        {
            public List<LmStudioModelData> Data { get; set; } = new List<LmStudioModelData>();
        }

        private class ClaudeModelsResponse
        {
            public List<string> Models { get; set; } = new List<string>();
        }

        private class ClaudeCompletionResponse
        {
            public List<ClaudeContent> Content { get; set; } = new List<ClaudeContent>();
        }

        private class ClaudeContent
        {
            public string Text { get; set; } = string.Empty;
        }

        /// <summary>
        /// Converts structured messages back to a single prompt string for Ollama.
        /// </summary>
        private string ConvertMessagesToPrompt(List<Dictionary<string, string>> messages)
        {
            var promptParts = new List<string>();

            foreach (var message in messages)
            {
                var role = message.GetValueOrDefault("role", "");
                var content = message.GetValueOrDefault("content", "");

                if (role == "system")
                {
                    promptParts.Add(content);
                }
                else if (role == "user")
                {
                    promptParts.Add($"User: {content}");
                }
                else if (role == "assistant")
                {
                    promptParts.Add($"Assistant: {content}");
                }
            }

            return string.Join("\n\n", promptParts) + "\n\nAssistant:";
        }

        /// <summary>
        /// Converts a legacy prompt string to structured messages for OpenAI-compatible API
        /// </summary>
        /// <param name="prompt">The full prompt string</param>
        /// <returns>Array of message objects for OpenAI-compatible API</returns>
        private object[] ConvertLegacyPromptToMessages(string prompt)
        {
            // Check if the prompt contains the pattern: "SystemPrompt\n\nUser: UserMessage\nAssistant:"
            // This is the format created by AiChatService when GLaDOS system prompt is found

            var userPattern = "\n\nUser: ";
            var assistantPattern = "\nAssistant:";

            var userIndex = prompt.IndexOf(userPattern);
            var assistantIndex = prompt.IndexOf(assistantPattern);

            if (userIndex > 0 && assistantIndex > userIndex)
            {
                // Extract system prompt (everything before "\n\nUser: ")
                var systemPrompt = prompt.Substring(0, userIndex).Trim();

                // Extract user message (between "\n\nUser: " and "\nAssistant:")
                var userMessage = prompt.Substring(userIndex + userPattern.Length,
                    assistantIndex - userIndex - userPattern.Length).Trim();

                _logger.LogInfo($"🔍 PARSED SYSTEM PROMPT: Length={systemPrompt.Length}");
                _logger.LogInfo($"🔍 PARSED USER MESSAGE: '{userMessage}'");

                return new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                };
            }
            else
            {
                // No system prompt detected, treat entire prompt as user message
                _logger.LogInfo("🔍 NO SYSTEM PROMPT DETECTED: Using entire prompt as user message");
                return new object[]
                {
                    new { role = "user", content = prompt }
                };
            }
        }
    }
}
