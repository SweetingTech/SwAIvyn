using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
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
        /// Lists the models available from LM Studio.
        /// </summary>
        Task<IEnumerable<string>> GetLmStudioModelsAsync(Guid? userId = null);

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

        /// <summary>
        /// Generates a streaming chat completion via OpenAI's chat endpoint.
        /// </summary>
        IAsyncEnumerable<string> GenerateOpenAiStreamingResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null);

        /// <summary>
        /// Generates a streaming chat completion via Claude's chat endpoint.
        /// </summary>
        IAsyncEnumerable<string> GenerateClaudeStreamingResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null);

        /// <summary>
        /// Generates a streaming chat completion via Ollama's chat endpoint.
        /// </summary>
        IAsyncEnumerable<string> GenerateOllamaStreamingResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null);

        /// <summary>
        /// Generates a streaming chat completion via LM Studio's OpenAI-compatible endpoint.
        /// </summary>
        IAsyncEnumerable<string> GenerateLmStudioStreamingResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null);

        /// <summary>
        /// Generates a chat completion with function calling support via OpenAI.
        /// </summary>
        Task<LlmFunctionResponse> GenerateOpenAiWithFunctionsAsync(
            List<Dictionary<string, string>> messages,
            List<LlmFunction> functions,
            string model = null,
            Guid? userId = null);

        /// <summary>
        /// Generates a chat completion with tool use support via Claude.
        /// </summary>
        Task<LlmFunctionResponse> GenerateClaudeWithToolsAsync(
            List<Dictionary<string, string>> messages,
            List<LlmFunction> tools,
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

        private static string NormalizeBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            url = url.TrimEnd('/');
            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - 3);
            }

            return url;
        }

        public LlmConnectorService(
            HttpClient httpClient,
            IConfigurationService configurationService,
            ISimpleLoggerService logger)
        {
            _httpClient = httpClient;
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
                var ollamaApiUrl = (await _configurationService.GetOllamaApiUrl()).TrimEnd('/');
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
                var lmStudioApiUrl = (await _configurationService.GetLmStudioApiUrl()).TrimEnd('/');
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

                // Fallback to legacy /models endpoint
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

        public async Task<IEnumerable<string>> GetLmStudioModelsAsync(Guid? userId = null)
        {
            try
            {
                var lmStudioApiUrl = (await _configurationService.GetLmStudioApiUrl()).TrimEnd('/');
                _logger.LogInfo($"Using LM Studio API URL: {lmStudioApiUrl}");

                var results = new List<string>();

                // Try OpenAI-compatible endpoint first
                var openAiResponse = await _httpClient.GetAsync($"{lmStudioApiUrl}/v1/models");
                if (openAiResponse.IsSuccessStatusCode)
                {
                    var openAiModels = await openAiResponse.Content.ReadFromJsonAsync<OpenAiModelsResponse>();
                    if (openAiModels?.Data != null && openAiModels.Data.Count > 0)
                    {
                        results.AddRange(openAiModels.Data.Select(m => m.Id));
                    }
                }

                // If no models found, fallback to legacy /models endpoint
                if (results.Count == 0)
                {
                    var legacyResponse = await _httpClient.GetAsync($"{lmStudioApiUrl}/models");
                    if (legacyResponse.IsSuccessStatusCode)
                    {
                        var legacyData = await legacyResponse.Content.ReadFromJsonAsync<LmStudioModelsResponse>();
                        if (legacyData?.Data != null && legacyData.Data.Count > 0)
                        {
                            results.AddRange(legacyData.Data.Select(m => m.Name));
                        }
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get LM Studio models: {ex.Message}");
                return new List<string>();
            }
        }

        //
        // === OpenAI ===
        //

        public async Task<IEnumerable<string>> GetOpenAiModelsAsync(Guid? userId = null)
        {
            try
            {
                // Define the recommended models first (using actual OpenAI model names)
                var recommendedModels = new List<string>
                {
                    "gpt-4o-mini",           // GPT-4o Mini (default - cost-effective)
                    "gpt-4o",                // GPT-4o (multimodal, real-time)
                    "gpt-4-turbo",           // GPT-4 Turbo
                    "gpt-4",                 // GPT-4
                    "gpt-3.5-turbo"          // GPT-3.5 Turbo (legacy)
                };

                var apiUrl = NormalizeBaseUrl(await _configurationService.GetOpenAiApiUrl(userId));
                var apiKey = await _configurationService.GetOpenAiApiKey(userId);
                _logger.LogInfo($"Using OpenAI API URL: {apiUrl}");

                // Try to fetch models from API
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/v1/models");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var modelsResponse = await response.Content.ReadFromJsonAsync<OpenAiModelsResponse>();
                        var apiModels = modelsResponse?.Data?.Select(m => m.Id) ?? new List<string>();

                        // Combine recommended models with API models, removing duplicates
                        var allModels = recommendedModels.Concat(apiModels).Distinct().ToList();
                        _logger.LogInfo($"Returning {allModels.Count} OpenAI models (recommended + API)");
                        return allModels;
                    }
                }
                catch (Exception apiEx)
                {
                    _logger.LogWarning($"Failed to fetch OpenAI models from API: {apiEx.Message}");
                }

                // Fallback to recommended models only
                _logger.LogInfo($"Returning {recommendedModels.Count} recommended OpenAI models");
                return recommendedModels;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get OpenAI models: {ex.Message}");
                return new List<string>();
            }
        }

        //
        // === Claude ===
        //

        public async Task<IEnumerable<string>> GetClaudeModelsAsync(Guid? userId = null)
        {
            try
            {
                // Claude doesn't have a models endpoint like OpenAI
                // Return the predefined available models (using actual Claude model names)
                var availableModels = new List<string>
                {
                    "claude-3-5-sonnet-20241022",    // Claude 3.5 Sonnet (default - recommended balance)
                    "claude-3-5-haiku-20241022",     // Claude 3.5 Haiku (fastest/cheapest)
                    "claude-3-opus-20240229",        // Claude 3 Opus (most powerful)
                    "claude-3-sonnet-20240229",      // Claude 3 Sonnet
                    "claude-3-haiku-20240307"        // Claude 3 Haiku
                };

                _logger.LogInfo($"Returning {availableModels.Count} predefined Claude models");
                return availableModels;
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
                    var ollamaApiUrl = (await _configurationService.GetOllamaApiUrl()).TrimEnd('/');
                    _logger.LogInfo($"Using Ollama API URL: {ollamaApiUrl}");

                    if (string.IsNullOrEmpty(model))
                    {
                        var available = await GetOllamaModelsAsync(userId);
                        model = available.FirstOrDefault()
                            ?? throw new Exception("No Ollama models available");
                    }

                    // Convert prompt to messages format for chat endpoint
                    var messages = ConvertLegacyPromptToMessages(prompt);
                    var requestPayload = new
                    {
                        model = model ?? "llama2",
                        messages = messages.Select(m => new { role = m["role"], content = m["content"] }).ToArray(),
                        stream = false
                    };

                    var response = await ExecuteWithRetryAsync(() =>
                        _httpClient.PostAsJsonAsync($"{ollamaApiUrl}/api/chat", requestPayload));

                    if (!response.IsSuccessStatusCode)
                    {
                        return $"Ollama API error: {response.StatusCode}";
                    }

                    var result = await response.Content
                        .ReadFromJsonAsync<OllamaChatResponse>();
                    return result?.Message?.Content ?? "No response from Ollama";
                }
                else if (engine == "lmstudio")
                {
                    var lmStudioApiUrl = (await _configurationService.GetLmStudioApiUrl()).TrimEnd('/');
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

                        var openAiResponse = await ExecuteWithRetryAsync(() =>
                            _httpClient.PostAsJsonAsync($"{lmStudioApiUrl}/v1/chat/completions", openAiRequest));

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
                    var legacyResponse = await ExecuteWithRetryAsync(() =>
                        _httpClient.PostAsJsonAsync($"{lmStudioApiUrl}/generate", legacyRequest));

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
                    var ollamaApiUrl = (await _configurationService.GetOllamaApiUrl()).TrimEnd('/');
                    _logger.LogInfo($"Using Ollama API URL (structured messages): {ollamaApiUrl}");

                    if (string.IsNullOrEmpty(model))
                    {
                        var available = await GetOllamaModelsAsync(userId);
                        model = available.FirstOrDefault()
                            ?? throw new Exception("No Ollama models available");
                    }

                    var requestPayload = new
                    {
                        model = model,
                        messages = messages.Select(m => new { role = m["role"], content = m["content"] }).ToArray(),
                        stream = false
                    };

                    var response = await ExecuteWithRetryAsync(() =>
                        _httpClient.PostAsJsonAsync($"{ollamaApiUrl}/api/chat", requestPayload));

                    if (!response.IsSuccessStatusCode)
                    {
                        return $"Ollama API error: {response.StatusCode}";
                    }

                    var result = await response.Content
                        .ReadFromJsonAsync<OllamaChatResponse>();
                    return result?.Message?.Content ?? "No response from Ollama";
                }
                else if (engine == "lmstudio")
                {
                var lmStudioApiUrl = (await _configurationService.GetLmStudioApiUrl()).TrimEnd('/');
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

                        var openAiResponse = await ExecuteWithRetryAsync(() =>
                            _httpClient.PostAsJsonAsync($"{lmStudioApiUrl}/v1/chat/completions", openAiRequest));

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
                var apiUrl = NormalizeBaseUrl(await _configurationService.GetOpenAiApiUrl(userId));
                var apiKey = await _configurationService.GetOpenAiApiKey(userId);
                _logger.LogInfo($"Using OpenAI API URL: {apiUrl}");

                var openAiRequest = new
                {
                    model = model ?? "gpt-4o-mini",
                    messages = messages
                        .Select(m => new { role = m["role"], content = m["content"] })
                        .ToArray(),
                    temperature = 0.7,
                    max_tokens = 1000,
                    stream = false
                };

                var response = await ExecuteWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/chat/completions");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = JsonContent.Create(openAiRequest);
                    return _httpClient.SendAsync(request);
                });
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

        public async IAsyncEnumerable<string> GenerateOpenAiStreamingResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null)
        {
            var apiUrl = NormalizeBaseUrl(await _configurationService.GetOpenAiApiUrl(userId));
            var apiKey = await _configurationService.GetOpenAiApiKey(userId);
            _logger.LogInfo($"Using OpenAI streaming API URL: {apiUrl}");

            var openAiRequest = new
            {
                model = model ?? "gpt-4o-mini",
                messages = messages
                    .Select(m => new { role = m["role"], content = m["content"] })
                    .ToArray(),
                temperature = 0.7,
                max_tokens = 1000,
                stream = true
            };

            HttpResponseMessage? response = null;
            try
            {
                response = await ExecuteWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/chat/completions");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = JsonContent.Create(openAiRequest);
                    return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                });

                if (!response.IsSuccessStatusCode)
                {
                    yield return $"OpenAI API error: {response.StatusCode}";
                    yield break;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);

                        if (data == "[DONE]")
                            break;

                        string? token = null;
                        try
                        {
                            using var doc = JsonDocument.Parse(data);
                            var choices = doc.RootElement.GetProperty("choices");
                            if (choices.GetArrayLength() > 0)
                            {
                                var delta = choices[0].GetProperty("delta");
                                if (delta.TryGetProperty("content", out var content))
                                {
                                    token = content.GetString();
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            // Skip malformed JSON chunks
                            continue;
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            yield return token;
                        }
                    }
                }
            }
            finally
            {
                response?.Dispose();
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
                var apiUrl = NormalizeBaseUrl(await _configurationService.GetClaudeApiUrl(userId));
                var apiKey = await _configurationService.GetClaudeApiKey(userId);
                _logger.LogInfo($"Using Claude API URL: {apiUrl}");

                var claudeRequest = new
                {
                    model = model ?? "claude-3-5-sonnet-20241022",
                    messages = messages
                        .Select(m => new { role = m["role"], content = m["content"] })
                        .ToArray(),
                    max_tokens = 1000,
                    temperature = 0.7,
                    stream = false
                };

                var response = await ExecuteWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/messages");
                    request.Headers.Add("x-api-key", apiKey);
                    request.Headers.Add("anthropic-version", "2023-06-01");
                    request.Content = JsonContent.Create(claudeRequest);
                    return _httpClient.SendAsync(request);
                });
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

        public async IAsyncEnumerable<string> GenerateClaudeStreamingResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null)
        {
            var apiUrl = NormalizeBaseUrl(await _configurationService.GetClaudeApiUrl(userId));
            var apiKey = await _configurationService.GetClaudeApiKey(userId);
            _logger.LogInfo($"Using Claude streaming API URL: {apiUrl}");

            var claudeRequest = new
            {
                model = model ?? "claude-3-5-sonnet-20241022",
                messages = messages
                    .Select(m => new { role = m["role"], content = m["content"] })
                    .ToArray(),
                max_tokens = 1000,
                temperature = 0.7,
                stream = true
            };

            HttpResponseMessage? response = null;
            try
            {
                response = await ExecuteWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/messages");
                    request.Headers.Add("x-api-key", apiKey);
                    request.Headers.Add("anthropic-version", "2023-06-01");
                    request.Content = JsonContent.Create(claudeRequest);
                    return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                });

                if (!response.IsSuccessStatusCode)
                {
                    yield return $"Claude API error: {response.StatusCode}";
                    yield break;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);

                        if (data == "[DONE]")
                            break;

                        string? token = null;
                        bool shouldBreak = false;

                        try
                        {
                            using var doc = JsonDocument.Parse(data);
                            if (doc.RootElement.TryGetProperty("type", out var typeProperty))
                            {
                                var eventType = typeProperty.GetString();

                                // Handle different Claude streaming event types
                                if (eventType == "content_block_delta")
                                {
                                    if (doc.RootElement.TryGetProperty("delta", out var delta) &&
                                        delta.TryGetProperty("text", out var text))
                                    {
                                        token = text.GetString();
                                    }
                                }
                                else if (eventType == "message_delta")
                                {
                                    // Handle message-level deltas if needed
                                    continue;
                                }
                                else if (eventType == "message_stop")
                                {
                                    // End of message
                                    shouldBreak = true;
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            // Skip malformed JSON chunks
                            continue;
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            yield return token;
                        }

                        if (shouldBreak)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                response?.Dispose();
            }
        }

        public async IAsyncEnumerable<string> GenerateOllamaStreamingResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null)
        {
            var ollamaApiUrl = (await _configurationService.GetOllamaApiUrl()).TrimEnd('/');
            _logger.LogInfo($"Using Ollama streaming API URL: {ollamaApiUrl}");

            if (string.IsNullOrEmpty(model))
            {
                var available = await GetOllamaModelsAsync(userId);
                model = available.FirstOrDefault() ?? "llama2";
            }

            var ollamaRequest = new
            {
                model = model,
                messages = messages
                    .Select(m => new { role = m["role"], content = m["content"] })
                    .ToArray(),
                stream = true
            };

            HttpResponseMessage? response = null;
            Stream? stream = null;
            StreamReader? reader = null;

            try
            {
                response = await ExecuteWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{ollamaApiUrl}/api/chat");
                    request.Content = JsonContent.Create(ollamaRequest);
                    return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                });

                if (!response.IsSuccessStatusCode)
                {
                    yield return $"Ollama API error: {response.StatusCode}";
                    yield break;
                }

                stream = await response.Content.ReadAsStreamAsync();
                reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string? token = null;
                        bool isDone = false;

                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            if (doc.RootElement.TryGetProperty("message", out var message) &&
                                message.TryGetProperty("content", out var content))
                            {
                                token = content.GetString();
                            }

                            // Check if done
                            if (doc.RootElement.TryGetProperty("done", out var done))
                            {
                                isDone = done.GetBoolean();
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            // Skip malformed JSON chunks
                            continue;
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            yield return token;
                        }

                        if (isDone)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                reader?.Dispose();
                stream?.Dispose();
                response?.Dispose();
            }
        }

        public async IAsyncEnumerable<string> GenerateLmStudioStreamingResponseAsync(
            List<Dictionary<string, string>> messages,
            string model = null,
            Guid? userId = null)
        {
            var lmStudioApiUrl = (await _configurationService.GetLmStudioApiUrl()).TrimEnd('/');
            _logger.LogInfo($"Using LM Studio streaming API URL: {lmStudioApiUrl}");

            var lmStudioRequest = new
            {
                model = model ?? "default",
                messages = messages
                    .Select(m => new { role = m["role"], content = m["content"] })
                    .ToArray(),
                temperature = 0.7,
                max_tokens = 1000,
                stream = true
            };

            HttpResponseMessage? response = null;
            Stream? stream = null;
            StreamReader? reader = null;

            try
            {
                response = await ExecuteWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{lmStudioApiUrl}/v1/chat/completions");
                    request.Content = JsonContent.Create(lmStudioRequest);
                    return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                });

                if (!response.IsSuccessStatusCode)
                {
                    yield return $"LM Studio API error: {response.StatusCode}";
                    yield break;
                }

                stream = await response.Content.ReadAsStreamAsync();
                reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);

                        if (data == "[DONE]")
                            break;

                        string? token = null;
                        try
                        {
                            using var doc = JsonDocument.Parse(data);
                            var choices = doc.RootElement.GetProperty("choices");
                            if (choices.GetArrayLength() > 0)
                            {
                                var delta = choices[0].GetProperty("delta");
                                if (delta.TryGetProperty("content", out var content))
                                {
                                    token = content.GetString();
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            // Skip malformed JSON chunks
                            continue;
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            yield return token;
                        }
                    }
                }
            }
            finally
            {
                reader?.Dispose();
                stream?.Dispose();
                response?.Dispose();
            }
        }

        public async Task<LlmFunctionResponse> GenerateOpenAiWithFunctionsAsync(
            List<Dictionary<string, string>> messages,
            List<LlmFunction> functions,
            string model = null,
            Guid? userId = null)
        {
            try
            {
                var apiUrl = NormalizeBaseUrl(await _configurationService.GetOpenAiApiUrl(userId));
                var apiKey = await _configurationService.GetOpenAiApiKey(userId);
                _logger.LogInfo($"Using OpenAI function calling API URL: {apiUrl}");

                var openAiFunctions = functions.Select(f => new
                {
                    name = f.Name,
                    description = f.Description,
                    parameters = f.Parameters
                }).ToArray();

                var openAiRequest = new
                {
                    model = model ?? "gpt-4o-mini",
                    messages = messages
                        .Select(m => new { role = m["role"], content = m["content"] })
                        .ToArray(),
                    temperature = 0.7,
                    max_tokens = 1000,
                    functions = openAiFunctions,
                    function_call = "auto"
                };

                var response = await ExecuteWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/chat/completions");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = JsonContent.Create(openAiRequest);
                    return _httpClient.SendAsync(request);
                });
                if (!response.IsSuccessStatusCode)
                {
                    return new LlmFunctionResponse
                    {
                        Content = $"OpenAI API error: {response.StatusCode}"
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAiFunctionCompletionResponse>();
                var choice = result?.Choices?.FirstOrDefault();

                if (choice?.Message?.FunctionCall != null)
                {
                    // Function call response
                    var functionCall = choice.Message.FunctionCall;
                    var arguments = new Dictionary<string, object>();

                    if (!string.IsNullOrEmpty(functionCall.Arguments))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(functionCall.Arguments);
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                arguments[prop.Name] = prop.Value.ToString();
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            _logger.LogError($"Failed to parse function arguments: {ex.Message}");
                        }
                    }

                    return new LlmFunctionResponse
                    {
                        FunctionCalls = new List<LlmFunctionCall>
                        {
                            new LlmFunctionCall
                            {
                                Name = functionCall.Name,
                                Arguments = arguments
                            }
                        },
                        RequiresFunctionExecution = true
                    };
                }
                else
                {
                    // Regular text response
                    return new LlmFunctionResponse
                    {
                        Content = choice?.Message?.Content ?? "No response from OpenAI"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to call OpenAI with functions: {ex.Message}");
                return new LlmFunctionResponse
                {
                    Content = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<LlmFunctionResponse> GenerateClaudeWithToolsAsync(
            List<Dictionary<string, string>> messages,
            List<LlmFunction> tools,
            string model = null,
            Guid? userId = null)
        {
            try
            {
                var apiUrl = NormalizeBaseUrl(await _configurationService.GetClaudeApiUrl(userId));
                var apiKey = await _configurationService.GetClaudeApiKey(userId);
                _logger.LogInfo($"Using Claude tool use API URL: {apiUrl}");

                var claudeTools = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    input_schema = t.Parameters
                }).ToArray();

                var claudeRequest = new
                {
                    model = model ?? "claude-3-5-sonnet-20241022",
                    messages = messages
                        .Select(m => new { role = m["role"], content = m["content"] })
                        .ToArray(),
                    max_tokens = 1000,
                    temperature = 0.7,
                    tools = claudeTools
                };

                var response = await ExecuteWithRetryAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v1/messages");
                    request.Headers.Add("x-api-key", apiKey);
                    request.Headers.Add("anthropic-version", "2023-06-01");
                    request.Content = JsonContent.Create(claudeRequest);
                    return _httpClient.SendAsync(request);
                });
                if (!response.IsSuccessStatusCode)
                {
                    return new LlmFunctionResponse
                    {
                        Content = $"Claude API error: {response.StatusCode}"
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<ClaudeToolCompletionResponse>();
                var content = result?.Content?.FirstOrDefault();

                if (content?.Type == "tool_use")
                {
                    // Tool use response
                    var arguments = new Dictionary<string, object>();

                    if (content.Input != null)
                    {
                        foreach (var prop in content.Input)
                        {
                            arguments[prop.Key] = prop.Value;
                        }
                    }

                    return new LlmFunctionResponse
                    {
                        FunctionCalls = new List<LlmFunctionCall>
                        {
                            new LlmFunctionCall
                            {
                                Name = content.Name ?? string.Empty,
                                Arguments = arguments
                            }
                        },
                        RequiresFunctionExecution = true
                    };
                }
                else
                {
                    // Regular text response
                    return new LlmFunctionResponse
                    {
                        Content = content?.Text ?? "No response from Claude"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to call Claude with tools: {ex.Message}");
                return new LlmFunctionResponse
                {
                    Content = $"Error: {ex.Message}"
                };
            }
        }

        //
        // === Utility Methods ===
        //

        /// <summary>
        /// Executes an HTTP request with retry logic and exponential backoff.
        /// </summary>
        private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
            Func<Task<HttpResponseMessage>> httpCall,
            RetryConfig? retryConfig = null)
        {
            var config = retryConfig ?? new RetryConfig();
            var attempt = 0;
            var delay = config.BaseDelay;

            while (attempt <= config.MaxRetries)
            {
                try
                {
                    var response = await httpCall();

                    // Success or non-retryable error
                    if (response.IsSuccessStatusCode ||
                        (!IsRetryableStatusCode(response.StatusCode) && attempt > 0))
                    {
                        return response;
                    }

                    // If this is the last attempt, return the response anyway
                    if (attempt == config.MaxRetries)
                    {
                        return response;
                    }

                    _logger.LogWarning($"HTTP request failed with {response.StatusCode}, retrying in {delay.TotalSeconds}s (attempt {attempt + 1}/{config.MaxRetries + 1})");
                }
                catch (Exception ex) when (attempt < config.MaxRetries)
                {
                    _logger.LogWarning($"HTTP request threw exception: {ex.Message}, retrying in {delay.TotalSeconds}s (attempt {attempt + 1}/{config.MaxRetries + 1})");
                }

                // Wait before retry
                await Task.Delay(delay);

                // Exponential backoff
                delay = TimeSpan.FromMilliseconds(Math.Min(
                    delay.TotalMilliseconds * config.BackoffMultiplier,
                    config.MaxDelay.TotalMilliseconds));

                attempt++;
            }

            // This should never be reached, but just in case
            throw new InvalidOperationException("Retry logic failed unexpectedly");
        }

        /// <summary>
        /// Determines if an HTTP status code is retryable.
        /// </summary>
        private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.TooManyRequests ||  // 429
                   statusCode == HttpStatusCode.InternalServerError ||  // 500
                   statusCode == HttpStatusCode.BadGateway ||  // 502
                   statusCode == HttpStatusCode.ServiceUnavailable ||  // 503
                   statusCode == HttpStatusCode.GatewayTimeout;  // 504
        }

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
        private List<Dictionary<string, string>> ConvertLegacyPromptToMessages(string prompt)
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

                return new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "role", "system" }, { "content", systemPrompt } },
                    new Dictionary<string, string> { { "role", "user" }, { "content", userMessage } }
                };
            }
            else
            {
                _logger.LogInfo("No system prompt detected; using entire prompt as user message.");
                return new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "role", "user" }, { "content", prompt } }
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

        private class OllamaChatResponse
        {
            public OllamaMessage Message { get; set; } = new();
            public bool Done { get; set; }
        }

        private class OllamaMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
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
            public OpenAiFunctionCall? FunctionCall { get; set; }
        }

        private class OpenAiFunctionCall
        {
            public string Name { get; set; } = string.Empty;
            public string Arguments { get; set; } = string.Empty;
        }

        private class OpenAiFunctionCompletionResponse
        {
            public List<OpenAiFunctionChoice> Choices { get; set; } = new();
        }

        private class OpenAiFunctionChoice
        {
            public OpenAiMessage Message { get; set; } = new();
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
            public string Type { get; set; } = string.Empty;
            public string? Name { get; set; }
            public Dictionary<string, object>? Input { get; set; }
        }

        private class ClaudeToolCompletionResponse
        {
            public List<ClaudeContent> Content { get; set; } = new();
        }
    }

    //
    // === Function Calling DTOs ===
    //

    /// <summary>
    /// Represents a function that can be called by the LLM.
    /// </summary>
    public class LlmFunction
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public Func<Dictionary<string, object>, Task<string>>? Handler { get; set; }
    }

    /// <summary>
    /// Represents a function call request from the LLM.
    /// </summary>
    public class LlmFunctionCall
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Arguments { get; set; } = new();
    }

    /// <summary>
    /// Represents the response from an LLM that may include function calls.
    /// </summary>
    public class LlmFunctionResponse
    {
        public string? Content { get; set; }
        public List<LlmFunctionCall> FunctionCalls { get; set; } = new();
        public bool RequiresFunctionExecution { get; set; }
    }

    /// <summary>
    /// Configuration for retry logic.
    /// </summary>
    public class RetryConfig
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
        public double BackoffMultiplier { get; set; } = 2.0;
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    }
}
