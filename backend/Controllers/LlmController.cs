using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LlmController : ControllerBase
    {
        private readonly ILlmConnectorService _llmConnectorService;
        private readonly IAiChatService _aiChatService;
        private readonly ISimpleLoggerService _logger;

        public LlmController(
            ILlmConnectorService llmConnectorService,
            IAiChatService aiChatService,
            ISimpleLoggerService logger)
        {
            _llmConnectorService = llmConnectorService;
            _aiChatService = aiChatService;
            _logger = logger;
        }

        //
        // === Ollama Endpoints ===
        //

        /// <summary>
        /// Gets available Ollama models.
        /// </summary>
        [HttpGet("ollama/models")]
        public async Task<IActionResult> GetOllamaModels([FromQuery] Guid? userId = null)
        {
            try
            {
                var models = await _llmConnectorService.GetOllamaModelsAsync(userId);
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting Ollama models", ex);
                return StatusCode(500, "An error occurred while getting Ollama models");
            }
        }

        /// <summary>
        /// Tests connection to Ollama by fetching its model list.
        /// </summary>
        [HttpGet("ollama/test")]
        public async Task<IActionResult> TestOllamaConnection([FromQuery] Guid? userId = null)
        {
            try
            {
                var models = await _llmConnectorService.GetOllamaModelsAsync(userId);
                var modelCount = models?.Count() ?? 0;
                return Ok(new
                {
                    connected = true,
                    modelCount,
                    message = $"Connected successfully. Found {modelCount} models."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing Ollama connection", ex);
                return Ok(new
                {
                    connected = false,
                    modelCount = 0,
                    message = $"Connection failed: {ex.Message}"
                });
            }
        }

        //
        // === LM Studio Endpoints ===
        //

        /// <summary>
        /// Gets the current LM Studio model name.
        /// </summary>
        [HttpGet("lmstudio/model")]
        public async Task<IActionResult> GetLmStudioModel([FromQuery] Guid? userId = null)
        {
            try
            {
                var model = await _llmConnectorService.GetLmStudioModelAsync(userId);
                return Ok(new { model });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting LM Studio model", ex);
                return StatusCode(500, "An error occurred while getting LM Studio model");
            }
        }

        /// <summary>
        /// Gets the list of models available in LM Studio.
        /// </summary>
        [HttpGet("lmstudio/models")]
        public async Task<IActionResult> GetLmStudioModels([FromQuery] Guid? userId = null)
        {
            try
            {
                var models = await _llmConnectorService.GetLmStudioModelsAsync(userId);
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting LM Studio models", ex);
                return StatusCode(500, "An error occurred while getting LM Studio models");
            }
        }

        /// <summary>
        /// Tests connection to LM Studio by fetching the current model.
        /// </summary>
        [HttpGet("lmstudio/test")]
        public async Task<IActionResult> TestLmStudioConnection([FromQuery] Guid? userId = null)
        {
            try
            {
                var model = await _llmConnectorService.GetLmStudioModelAsync(userId);
                return Ok(new
                {
                    connected = true,
                    model,
                    message = $"Connected successfully. Current model: {model}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing LM Studio connection", ex);
                return Ok(new
                {
                    connected = false,
                    model = "Unknown",
                    message = $"Connection failed: {ex.Message}"
                });
            }
        }

        //
        // === OpenAI Endpoints ===
        //

        /// <summary>
        /// Gets available OpenAI models.
        /// </summary>
        [HttpGet("openai/models")]
        public async Task<IActionResult> GetOpenAiModels([FromQuery] Guid? userId = null)
        {
            try
            {
                var models = await _llmConnectorService.GetOpenAiModelsAsync(userId);
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting OpenAI models", ex);
                return StatusCode(500, "An error occurred while getting OpenAI models");
            }
        }

        /// <summary>
        /// Tests connection to OpenAI by fetching its model list.
        /// </summary>
        [HttpGet("openai/test")]
        public async Task<IActionResult> TestOpenAiConnection([FromQuery] Guid? userId = null)
        {
            try
            {
                var models = await _llmConnectorService.GetOpenAiModelsAsync(userId);
                var count = models?.Count() ?? 0;
                return Ok(new { connected = true, modelCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing OpenAI connection", ex);
                return Ok(new { connected = false, message = $"Connection failed: {ex.Message}" });
            }
        }

        //
        // === Claude Endpoints ===
        //

        /// <summary>
        /// Gets available Claude models.
        /// </summary>
        [HttpGet("claude/models")]
        public async Task<IActionResult> GetClaudeModels([FromQuery] Guid? userId = null)
        {
            try
            {
                var models = await _llmConnectorService.GetClaudeModelsAsync(userId);
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting Claude models", ex);
                return StatusCode(500, "An error occurred while getting Claude models");
            }
        }

        /// <summary>
        /// Tests connection to Claude by fetching its model list.
        /// </summary>
        [HttpGet("claude/test")]
        public async Task<IActionResult> TestClaudeConnection([FromQuery] Guid? userId = null)
        {
            try
            {
                var models = await _llmConnectorService.GetClaudeModelsAsync(userId);
                var count = models?.Count() ?? 0;
                return Ok(new { connected = true, modelCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing Claude connection", ex);
                return Ok(new { connected = false, message = $"Connection failed: {ex.Message}" });
            }
        }

        //
        // === Aggregate Endpoint: Get All Models ===
        //

        /// <summary>
        /// Gets all available models from Ollama, LM Studio, OpenAI, and Claude.
        /// </summary>
        [HttpGet("models")]
        public async Task<IActionResult> GetAllModels([FromQuery] Guid? userId = null)
        {
            try
            {
                var result = new Dictionary<string, object>();

                // 1) Ollama
                try
                {
                    var ollamaModels = await _llmConnectorService.GetOllamaModelsAsync(userId);
                    result["ollama"] = new
                    {
                        available = true,
                        models = ollamaModels?.ToList() ?? new List<string>()
                    };
                }
                catch (Exception ex)
                {
                    result["ollama"] = new
                    {
                        available = false,
                        error = ex.Message,
                        models = new List<string>()
                    };
                }

                // 2) LM Studio
                try
                {
                    var lmStudioModel = await _llmConnectorService.GetLmStudioModelAsync(userId);
                    result["lmstudio"] = new
                    {
                        available = true,
                        models = new List<string> { lmStudioModel }
                    };
                }
                catch (Exception ex)
                {
                    result["lmstudio"] = new
                    {
                        available = false,
                        error = ex.Message,
                        models = new List<string>()
                    };
                }

                // 3) OpenAI
                try
                {
                    var openAiModels = await _llmConnectorService.GetOpenAiModelsAsync(userId);
                    result["openai"] = new
                    {
                        available = true,
                        models = openAiModels?.ToList() ?? new List<string>()
                    };
                }
                catch (Exception ex)
                {
                    result["openai"] = new
                    {
                        available = false,
                        error = ex.Message,
                        models = new List<string>()
                    };
                }

                // 4) Claude
                try
                {
                    var claudeModels = await _llmConnectorService.GetClaudeModelsAsync(userId);
                    result["claude"] = new
                    {
                        available = true,
                        models = claudeModels?.ToList() ?? new List<string>()
                    };
                }
                catch (Exception ex)
                {
                    result["claude"] = new
                    {
                        available = false,
                        error = ex.Message,
                        models = new List<string>()
                    };
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting all models", ex);
                return StatusCode(500, "An error occurred while getting models");
            }
        }

        //
        // === Engines Endpoint ===
        //

        /// <summary>
        /// Gets a list of available LLM engines.
        /// </summary>
        [HttpGet("engines")]
        public IActionResult GetEngines()
        {
            try
            {
                var engines = new[]
                {
                    new {
                        id = "ollama",
                        name = "Ollama",
                        description = "Local AI models via Ollama",
                        defaultUrl = "http://localhost:11434"
                    },
                    new {
                        id = "lmstudio",
                        name = "LM Studio",
                        description = "Local AI models via LM Studio",
                        defaultUrl = "http://localhost:1234"
                    },
                    new {
                        id = "openai",
                        name = "OpenAI",
                        description = "Remote AI models via OpenAI",
                        defaultUrl = "https://api.openai.com/v1"
                    },
                    new {
                        id = "claude",
                        name = "Claude",
                        description = "Remote AI models via Anthropic Claude",
                        defaultUrl = "https://api.anthropic.com/v1"
                    }
                };

                return Ok(engines);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting engines", ex);
                return StatusCode(500, "An error occurred while getting engines");
            }
        }

        //
        // === LLM Settings (Get/Set) ===
        //

        /// <summary>
        /// Gets current LLM settings for a user (or global if userId is null).
        /// </summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetLlmSettings([FromQuery] Guid? userId = null)
        {
            try
            {
                var settings = await _aiChatService.GetCurrentLlmSettingsAsync(userId);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting LLM settings", ex);
                return StatusCode(500, "An error occurred while getting LLM settings");
            }
        }

        /// <summary>
        /// Sets the default LLM engine and model for a user.
        /// </summary>
        [HttpPost("settings")]
        public async Task<IActionResult> SetLlmSettings([FromBody] SetLlmSettingsRequest request)
        {
            try
            {
                var success = await _aiChatService.SetDefaultLlmSettingsAsync(
                    request.UserId,
                    request.Engine,
                    request.Model);

                if (success)
                {
                    return Ok(new { message = "LLM settings updated successfully" });
                }

                return StatusCode(500, "Failed to update LLM settings");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error setting LLM settings", ex);
                return StatusCode(500, $"An error occurred while setting LLM settings: {ex.Message}");
            }
        }

        //
        // === Text‐Based Generation Endpoints ===
        //

        /// <summary>
        /// Generates a response from a single‐prompt LLM (Ollama, LM Studio, etc.).
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateResponse([FromBody] GenerateRequest request)
        {
            try
            {
                var engine = string.IsNullOrEmpty(request.Engine) ? "ollama" : request.Engine;
                var response = await _llmConnectorService.GenerateResponseAsync(
                    request.Prompt,
                    engine,
                    request.Model,
                    request.UserId);

                return Ok(new { response });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating response", ex);
                return StatusCode(500, $"An error occurred while generating response: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a chat‐style response using the OpenAI chat endpoint.
        /// </summary>
        [HttpPost("openai/generate")]
        public async Task<IActionResult> GenerateOpenAi([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                var result = await _llmConnectorService.GenerateOpenAiResponseAsync(
                    messages,
                    request.Model,
                    request.UserId);

                return Ok(new { response = result });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating OpenAI response", ex);
                return StatusCode(500, $"An error occurred while generating response: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a streaming chat‐style response using the OpenAI chat endpoint.
        /// </summary>
        [HttpPost("openai/generate/stream")]
        public async Task GenerateOpenAiStream([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                Response.Headers["Content-Type"] = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                await foreach (var token in _llmConnectorService.GenerateOpenAiStreamingResponseAsync(
                    messages,
                    request.Model,
                    request.UserId))
                {
                    await Response.WriteAsync($"data: {token}\n\n");
                    await Response.Body.FlushAsync();
                }

                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating OpenAI streaming response", ex);
                await Response.WriteAsync($"data: Error: {ex.Message}\n\n");
                await Response.Body.FlushAsync();
            }
        }

        /// <summary>
        /// Generates a chat‐style response using the Claude chat endpoint.
        /// </summary>
        [HttpPost("claude/generate")]
        public async Task<IActionResult> GenerateClaude([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                var result = await _llmConnectorService.GenerateClaudeResponseAsync(
                    messages,
                    request.Model,
                    request.UserId);

                return Ok(new { response = result });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating Claude response", ex);
                return StatusCode(500, $"An error occurred while generating response: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a streaming chat‐style response using the Claude chat endpoint.
        /// </summary>
        [HttpPost("claude/generate/stream")]
        public async Task GenerateClaudeStream([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                Response.Headers["Content-Type"] = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                await foreach (var token in _llmConnectorService.GenerateClaudeStreamingResponseAsync(
                    messages,
                    request.Model,
                    request.UserId))
                {
                    await Response.WriteAsync($"data: {token}\n\n");
                    await Response.Body.FlushAsync();
                }

                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating Claude streaming response", ex);
                await Response.WriteAsync($"data: Error: {ex.Message}\n\n");
                await Response.Body.FlushAsync();
            }
        }

        /// <summary>
        /// Generates a streaming chat‐style response using the Ollama chat endpoint.
        /// </summary>
        [HttpPost("ollama/generate/stream")]
        public async Task GenerateOllamaStream([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                Response.Headers["Content-Type"] = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                await foreach (var token in _llmConnectorService.GenerateOllamaStreamingResponseAsync(
                    messages,
                    request.Model,
                    request.UserId))
                {
                    await Response.WriteAsync($"data: {token}\n\n");
                    await Response.Body.FlushAsync();
                }

                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating Ollama streaming response", ex);
                await Response.WriteAsync($"data: Error: {ex.Message}\n\n");
                await Response.Body.FlushAsync();
            }
        }

        /// <summary>
        /// Generates a streaming chat‐style response using the LM Studio OpenAI-compatible endpoint.
        /// </summary>
        [HttpPost("lmstudio/generate/stream")]
        public async Task GenerateLmStudioStream([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                Response.Headers["Content-Type"] = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                await foreach (var token in _llmConnectorService.GenerateLmStudioStreamingResponseAsync(
                    messages,
                    request.Model,
                    request.UserId))
                {
                    await Response.WriteAsync($"data: {token}\n\n");
                    await Response.Body.FlushAsync();
                }

                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating LM Studio streaming response", ex);
                await Response.WriteAsync($"data: Error: {ex.Message}\n\n");
                await Response.Body.FlushAsync();
            }
        }

        /// <summary>
        /// Generates a response with function calling support using OpenAI.
        /// </summary>
        [HttpPost("openai/generate/functions")]
        public async Task<IActionResult> GenerateOpenAiWithFunctions([FromBody] GenerateFunctionsRequest request)
        {
            try
            {
                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                var result = await _llmConnectorService.GenerateOpenAiWithFunctionsAsync(
                    messages,
                    request.Functions,
                    request.Model,
                    request.UserId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating OpenAI response with functions", ex);
                return StatusCode(500, $"An error occurred while generating response: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a response with tool use support using Claude.
        /// </summary>
        [HttpPost("claude/generate/tools")]
        public async Task<IActionResult> GenerateClaudeWithTools([FromBody] GenerateFunctionsRequest request)
        {
            try
            {
                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                var result = await _llmConnectorService.GenerateClaudeWithToolsAsync(
                    messages,
                    request.Functions,
                    request.Model,
                    request.UserId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating Claude response with tools", ex);
                return StatusCode(500, $"An error occurred while generating response: {ex.Message}");
            }
        }

        /// <summary>
        /// Test endpoint for function calling with a simple example function.
        /// </summary>
        [HttpPost("test/functions")]
        public async Task<IActionResult> TestFunctionCalling([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                // Define a simple test function
                var functions = new List<SwAIvyn.Services.LlmFunction>
                {
                    new SwAIvyn.Services.LlmFunction
                    {
                        Name = "get_current_time",
                        Description = "Get the current date and time",
                        Parameters = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object>(),
                            ["required"] = new string[0]
                        },
                        Handler = async (args) => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                };

                var result = await _llmConnectorService.GenerateOpenAiWithFunctionsAsync(
                    messages,
                    functions,
                    request.Model,
                    request.UserId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing function calling", ex);
                return StatusCode(500, $"An error occurred while testing function calling: {ex.Message}");
            }
        }

        /// <summary>
        /// Test endpoint for Claude tool use with a simple example tool.
        /// </summary>
        [HttpPost("test/claude-tools")]
        public async Task<IActionResult> TestClaudeToolUse([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                var messages = request.Messages
                    .Select(m => new Dictionary<string, string>
                    {
                        { "role", m.Role },
                        { "content", m.Content }
                    })
                    .ToList();

                // Define a simple test tool for Claude
                var tools = new List<SwAIvyn.Services.LlmFunction>
                {
                    new SwAIvyn.Services.LlmFunction
                    {
                        Name = "get_current_time",
                        Description = "Get the current date and time",
                        Parameters = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object>(),
                            ["required"] = new string[0]
                        },
                        Handler = async (args) => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                };

                var result = await _llmConnectorService.GenerateClaudeWithToolsAsync(
                    messages,
                    tools,
                    request.Model,
                    request.UserId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing Claude tool use", ex);
                return StatusCode(500, $"An error occurred while testing Claude tool use: {ex.Message}");
            }
        }

        /// <summary>
        /// Simple test endpoint for Ollama streaming - just send "Hello, how are you?"
        /// </summary>
        [HttpGet("test/ollama-stream")]
        public async Task TestOllamaStreamSimple()
        {
            try
            {
                Response.Headers["Content-Type"] = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                var messages = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "role", "user" }, { "content", "Hello, how are you?" } }
                };

                await foreach (var token in _llmConnectorService.GenerateOllamaStreamingResponseAsync(messages))
                {
                    await Response.WriteAsync($"data: {token}\n\n");
                    await Response.Body.FlushAsync();
                }

                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing Ollama streaming", ex);
                await Response.WriteAsync($"data: Error: {ex.Message}\n\n");
                await Response.Body.FlushAsync();
            }
        }

        /// <summary>
        /// Simple test endpoint for LM Studio streaming - just send "Hello, how are you?"
        /// </summary>
        [HttpGet("test/lmstudio-stream")]
        public async Task TestLmStudioStreamSimple()
        {
            try
            {
                Response.Headers["Content-Type"] = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                var messages = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "role", "user" }, { "content", "Hello, how are you?" } }
                };

                await foreach (var token in _llmConnectorService.GenerateLmStudioStreamingResponseAsync(messages))
                {
                    await Response.WriteAsync($"data: {token}\n\n");
                    await Response.Body.FlushAsync();
                }

                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing LM Studio streaming", ex);
                await Response.WriteAsync($"data: Error: {ex.Message}\n\n");
                await Response.Body.FlushAsync();
            }
        }
    }

    /// <summary>
    /// Request model for setting LLM settings
    /// </summary>
    public class SetLlmSettingsRequest
    {
        public Guid? UserId { get; set; }
        public string Engine { get; set; }
        public string Model { get; set; }
    }

    /// <summary>
    /// Request model for single‐prompt generation
    /// </summary>
    public class GenerateRequest
    {
        public string Prompt { get; set; }
        public string Engine { get; set; }
        public string Model { get; set; }
        public Guid? UserId { get; set; }
    }

    /// <summary>
    /// Request model for chat‐style generation
    /// </summary>
    public class GenerateMessagesRequest
    {
        public Guid? UserId { get; set; }
        public string Model { get; set; }
        public List<MessageDto> Messages { get; set; } = new();
    }

    public class MessageDto
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    /// <summary>
    /// Request model for function calling generation
    /// </summary>
    public class GenerateFunctionsRequest
    {
        public Guid? UserId { get; set; }
        public string Model { get; set; }
        public List<MessageDto> Messages { get; set; } = new();
        public List<SwAIvyn.Services.LlmFunction> Functions { get; set; } = new();
    }
}
