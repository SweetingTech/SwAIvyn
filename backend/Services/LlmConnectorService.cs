using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for connecting to local LLM engines (Ollama, LM Studio) and remote services (OpenAI, Claude).
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
        /// Lists available OpenAI models.
        /// </summary>
        Task<IEnumerable<string>> GetOpenAiModelsAsync(Guid? userId = null);

        /// <summary>
        /// Lists available Claude models.
        /// </summary>
        Task<IEnumerable<string>> GetClaudeModelsAsync(Guid? userId = null);

        /// <summary>
        /// Generates a chat‐style completion using structured messages for any engine (Ollama, LM Studio, OpenAI, Claude).
        /// Ollama will flatten messages into a single prompt automatically.
        /// </summary>
        Task<string> GenerateResponseAsync(
            List<Dictionary<string, string>> messages,
            string engine = "ollama",
            string model = null,
            Guid? userId = null);

        /// <summary>
        /// Sends a simple prompt (string) to either Ollama or LM Studio (legacy mode),
        /// or routes through OpenAI/Claude if selected.
        /// </summary>
        Task<string> GenerateResponseAsync(
            string prompt,
            string engine = "ollama",
            string model = null,
            Guid? userId = null);

        /// <summary>
        /// Generates a chat‐style completion specifically via OpenAI’s chat endpoint.
        /// </summary>
        Task<string> GenerateOpenAiResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null);

        /// <summary>
        /// Generates a chat‐style completion specifically via Claude’s chat endpoint.
        /// </summary>
        Task<string> GenerateClaudeResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null);
    }

    /// <summary>
    /// Concrete implementation of ILlmConnectorService, using HttpClient to talk to Ollama, LM Studio, OpenAI, and Claude.
    /// </summary>
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
            _configurationService = configurationService 
                ?? throw new ArgumentNullException(nameof(configurationService));
            _logger = logger 
                ?? throw new ArgumentNullException(nameof(logger));
        }

        //
        // === Ollama ===
        //

        public async Task<IEnumerable<string>> GetOllamaModelsAsync(Guid? userId = null)
        {
            try
            {
                var ollamaApiUrl = _configurationService.GetOllamaApiUrl().TrimEnd('/');
                _logger.LogInfo($"Using Ollama API URL: {ollamaApiUrl}");

                var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>(
                    $"{ollamaApiUrl}/api/tags");

                return response?.Models?.Select(m => m.Name) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get Ollama models: {ex.Message}");
                return new List<string>();
            }
        }

        //
        // === LM Studio ===
        //

        public async Task<string> GetLmStudioModelAsync(Guid? userId = null)
        {
            try
            {
                var lmStudioApiUrl = _configurationService.GetLmStudioApiUrl().TrimEnd('/');
                _logger.LogInfo($"Using LM Studio API URL: {lmStudioApiUrl}");

                // Attempt OpenAI-compatible endpoint first
                var openAiCompatibleResponse = await _httpClient.GetAsync($"{lmStudioApiUrl}/v1/models");
                if (openAiCompatibleResponse.IsSuccessStatusCode)
                {
                    var openAiModels = await openAiCompatibleResponse.Content
                        .ReadFromJsonAsync<OpenAiModelsResponse>();

                    var firstModel = openAiModels?.Data?.FirstOrDefault()?.Id;
                    if (!string.IsNullOrEmpty(firstModel))
                    {
                        return firstModel;
                    }
                }

                // Fallback to legacy /models endpoint if available
                var legacyResponse = await _httpClient.GetAsync($"{lmStudioApiUrl}/models");
                if (legacyResponse.IsSuccessStatusCode)
                {
                    var legacyData = await legacyResponse.Content
                        .ReadFromJsonAsync<LmStudioModelsResponse>();

                    var firstLegacyModel = legacyData?.Data?.FirstOrDefault()?.Name;
                    if (!string.IsNullOrEmpty(firstLegacyModel))
                    {
                        return firstLegacyModel;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get LM Studio model: {ex.Message}");
                return string.Empty;
            }
        }

        //
        // === OpenAI ===
        //

        public async Task<IEnumerable<string>> GetOpenAiModelsAsync(Guid? userId = null)
        {
            try
            {
                var apiUrl = _configurationService.GetOpenAiApiUrl().TrimEnd('/');
                var apiKey = _configurationService.GetOpenAiApiKey();
                _logger.LogInfo($"Using OpenAI API URL: {apiUrl}");

                var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"OpenAI models endpoint returned {response.StatusCode}");
                    return new List<string>();
                }

                var modelsResponse = await response.Content.ReadFromJsonAsync<OpenAiModelsResponse>();
                return modelsResponse?.Data?.Select(m => m.Id) ?? new List<string>();
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
                var apiUrl = _configurationService.GetClaudeApiUrl().TrimEnd('/');
                var apiKey = _configurationService.GetClaudeApiKey();
                _logger.LogInfo($"Using Claude API URL: {apiUrl}");

                var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Claude models endpoint returned {response.StatusCode}");
                    return new List<string>();
                }

                var claudeModels = await response.Content.ReadFromJsonAsync<ClaudeModelsResponse>();
                return claudeModels?.Models?.Select(m => m.Name) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get Claude models: {ex.Message}");
                return new List<string>();
            }
        }

        //
        // === Prompt‐based Generation ===
        //

        public async Task<string> GenerateResponseAsync(
            string prompt,
            string engine = "ollama",
            string model = null,
            Guid? userId = null)
        {
            engine = engine?.ToLowerInvariant() ?? "ollama";

            try
            {
                if (engine == "ollama")
                {
                    var ollamaApiUrl = _configurationService.GetOllamaApiUrl().TrimEnd('/');
                    _logger.LogInfo($"Using Ollama API URL: {ollamaApiUrl}");

                    if (string.IsNullOrEmpty(model))
                    {
                        var available = await GetOllamaModelsAsync(userId);
                        model = available.FirstOrDefault() 
                            ?? throw new Exception("No Ollama models available");
                    }

                    var requestPayload = new
                    {
                        model = model,
                        prompt = prompt,
                        stream = false
                    };

                    var response = await _httpClient.PostAsJsonAsync(
                        $"{ollamaApiUrl}/api/generate",
                        requestPayload);

                    if (!response.IsSuccessStatusCode)
                    {
                        return $"Ollama API error: {response.StatusCode}";
                    }

                    var result = await response.Content
                        .ReadFromJsonAsync<OllamaGenerateResponse>();
                    return result?.Response ?? "No response from Ollama";
                }
                else if (engine == "lmstudio")
                {
                    var lmStudioApiUrl = _configurationService.GetLmStudioApiUrl().TrimEnd('/');
                    _logger.LogInfo($"Using LM Studio API URL: {lmStudioApiUrl}");

                    // Try OpenAI-compatible route first
                    try
                    {
                        var messages = ConvertLegacyPromptToMessages(prompt);
                        var openAiRequest = new
                        {
                            model = model ?? "default",
                            messages = messages,
                            temperature = 0.7,
                            max_tokens = 1000
                        };

                        var openAiResponse = await _httpClient.PostAsJsonAsync(
                            $"{lmStudioApiUrl}/v1/chat/completions",
                            openAiRequest);

                        if (openAiResponse.IsSuccessStatusCode)
                        {
                            var openAiResult = await openAiResponse.Content
                                .ReadFromJsonAsync<OpenAiCompletionResponse>();

                            if (openAiResult?.Choices?.Count > 0)
                            {
                                return openAiResult.Choices[0].Message.Content;
                            }
                        }
                    }
                    catch (Exception oaEx)
                    {
                        _logger.LogWarning(
                            $"Failed to use LM Studio OpenAI-compatible endpoint: {oaEx.Message}");
                    }

                    // Fallback to legacy /generate
                    var legacyRequest = new { prompt = prompt };
                    var legacyResponse = await _httpClient.PostAsJsonAsync(
                        $"{lmStudioApiUrl}/generate",
                        legacyRequest);

                    if (!legacyResponse.IsSuccessStatusCode)
                    {
                        return $"LM Studio API error: {legacyResponse.StatusCode}";
                    }

                    var legacyResult = await legacyResponse.Content
                        .ReadFromJsonAsync<LmStudioGenerateResponse>();

                    return legacyResult?.Text ?? "No response from LM Studio";
                }
                else if (engine == "openai")
                {
                    // Convert prompt → messages and call OpenAI
                    var messages = ConvertLegacyPromptToMessages(prompt);
                    return await GenerateOpenAiResponseAsync(messages, model, userId);
                }
                else if (engine == "claude")
                {
                    var messages = ConvertLegacyPromptToMessages(prompt);
                    return await GenerateClaudeResponseAsync(messages, model, userId);
                }
                else
                {
                    return $"Unsupported engine '{engine}'";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating response ({engine}): {ex.Message}");
                return $"Error generating response: {ex.Message}";
            }
        }

        public async Task<string> GenerateResponseAsync(
            List<Dictionary<string, string>> messages,
            string engine = "ollama",
            string model = null,
            Guid? userId = null)
        {
            engine = engine?.ToLowerInvariant() ?? "ollama";

            try
            {
                if (engine == "ollama")
                {
                    var prompt = ConvertMessagesToPrompt(messages);
                    return await GenerateResponseAsync(prompt, engine, model, userId);
                }
                else if (engine == "lmstudio")
                {
                    var lmStudioApiUrl = _configurationService.GetLmStudioApiUrl().TrimEnd('/');
                    _logger.LogInfo($"Using LM Studio API URL (structured messages): {lmStudioApiUrl}");

                    // Try OpenAI-compatible endpoint first
                    try
                    {
                        var openAiRequest = new
                        {
                            model = model ?? "default",
                            messages = messages
                                .Select(m => new { role = m["role"], content = m["content"] })
                                .ToArray(),
                            temperature = 0.7,
                            max_tokens = 1000
                        };

                        var openAiResponse = await _httpClient.PostAsJsonAsync(
                            $"{lmStudioApiUrl}/v1/chat/completions",
                            openAiRequest);

                        if (openAiResponse.IsSuccessStatusCode)
                        {
                            var openAiResult = await openAiResponse.Content
                                .ReadFromJsonAsync<OpenAiCompletionResponse>();

                            if (openAiResult?.Choices?.Count > 0)
                            {
                                return openAiResult.Choices[0].Message.Content;
                            }
                        }
                    }
                    catch (Exception oaEx)
                    {
                        _logger.LogWarning(
                            $"Failed to use LM Studio OpenAI-compatible endpoint: {oaEx.Message}");
                    }

                    // Fallback to prompt-based
                    var fallbackPrompt = ConvertMessagesToPrompt(messages);
                    return await GenerateResponseAsync(fallbackPrompt, engine, model, userId);
                }
                else if (engine == "openai")
                {
                    return await GenerateOpenAiResponseAsync(messages, model, userId);
                }
                else if (engine == "claude")
                {
                    return await GenerateClaudeResponseAsync(messages, model, userId);
                }
                else
                {
                    return $"Unsupported engine '{engine}'";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating response ({engine}): {ex.Message}");
                return $"Error generating response: {ex.Message}";
            }
        }

        //
        // === OpenAI Chat ===
        //

        public async Task<string> GenerateOpenAiResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null)
        {
            try
            {
                var apiUrl = _configurationService.GetOpenAiApiUrl().TrimEnd('/');
                var apiKey = _configurationService.GetOpenAiApiKey();
                _logger.LogInfo($"Using OpenAI API URL: {apiUrl}");

                var openAiRequest = new
                {
                    model = model ?? "gpt-3.5-turbo",
                    messages = messages
                        .Select(m => new { role = m["role"], content = m["content"] })
                        .ToArray(),
                    temperature = 0.7,
                    max_tokens = 1000
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post, $"{apiUrl}/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = JsonContent.Create(openAiRequest);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return $"OpenAI API error: {response.StatusCode}";
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAiCompletionResponse>();
                return result?.Choices?.FirstOrDefault()?.Message.Content 
                    ?? "No response from OpenAI";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to call OpenAI: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        //
        // === Claude Chat ===
        //

        public async Task<string> GenerateClaudeResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null)
        {
            try
            {
                var apiUrl = _configurationService.GetClaudeApiUrl().TrimEnd('/');
                var apiKey = _configurationService.GetClaudeApiKey();
                _logger.LogInfo($"Using Claude API URL: {apiUrl}");

                var claudeRequest = new
                {
                    model = model ?? "claude-3-20240229",
                    messages = messages
                        .Select(m => new { role = m["role"], content = m["content"] })
                        .ToArray(),
                    max_tokens = 1000,
                    temperature = 0.7
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post, $"{apiUrl}/v1/messages");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = JsonContent.Create(claudeRequest);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return $"Claude API error: {response.StatusCode}";
                }

                var result = await response.Content.ReadFromJsonAsync<ClaudeCompletionResponse>();
                return result?.Content?.FirstOrDefault()?.Text 
                    ?? "No response from Claude";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to call Claude API: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        //
        // === Utility Methods ===
        //

        /// <summary>
        /// Converts structured messages into a single prompt string for Ollama or legacy LM Studio.
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
        /// Converts a flat prompt string into structured messages for chat‐style endpoints.
        /// If a “System…User: … Assistant:” pattern is detected, splits accordingly.
        /// Otherwise treats the entire prompt as a user message.
        /// </summary>
        private object[] ConvertLegacyPromptToMessages(string prompt)
        {
            const string userPattern = "\n\nUser: ";
            const string assistantPattern = "\nAssistant:";

            var userIndex = prompt.IndexOf(userPattern, StringComparison.Ordinal);
            var assistantIndex = prompt.IndexOf(assistantPattern, StringComparison.Ordinal);

            if (userIndex > 0 && assistantIndex > userIndex)
            {
                var systemPrompt = prompt.Substring(0, userIndex).Trim();
                var userMessage = prompt.Substring(
                    userIndex + userPattern.Length,
                    assistantIndex - userIndex - userPattern.Length
                ).Trim();

                _logger.LogInfo($"Parsed system prompt (length {systemPrompt.Length}) and user message.");

                return new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                };
            }
            else
            {
                _logger.LogInfo("No system prompt detected; using entire prompt as user message.");
                return new object[]
                {
                    new { role = "user", content = prompt }
                };
            }
        }

        //
        // === DTO Classes for Deserialization ===
        //

        private class OllamaTagsResponse
        {
            public List<OllamaModel> Models { get; set; } = new();
        }

        private class OllamaModel
        {
            public string Name { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public DateTime ModifiedAt { get; set; }
            public long Size { get; set; }
        }

        private class OllamaGenerateResponse
        {
            public string Response { get; set; } = string.Empty;
            public bool Done { get; set; }
        }

        private class LmStudioGenerateResponse
        {
            public string Text { get; set; } = string.Empty;
        }

        private class LmStudioModelsResponse
        {
            public List<LmStudioModelData> Data { get; set; } = new();
        }

        private class LmStudioModelData
        {
            public string Id { get; set; } = string.Empty;
            public string Object { get; set; } = string.Empty;
            public string OwnedBy { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private class OpenAiModelsResponse
        {
            public List<OpenAiModelData> Data { get; set; } = new();
        }

        private class OpenAiModelData
        {
            public string Id { get; set; } = string.Empty;
        }

        private class OpenAiCompletionResponse
        {
            public List<OpenAiChoice> Choices { get; set; } = new();
        }

        private class OpenAiChoice
        {
            public OpenAiMessage Message { get; set; } = new();
        }

        private class OpenAiMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        private class ClaudeModelsResponse
        {
            public List<ClaudeModel> Models { get; set; } = new();
        }

        private class ClaudeModel
        {
            public string Name { get; set; } = string.Empty;
        }

        private class ClaudeCompletionResponse
        {
            public List<ClaudeContent> Content { get; set; } = new();
        }

        private class ClaudeContent
        {
            public string Text { get; set; } = string.Empty;
        }
    }
}
