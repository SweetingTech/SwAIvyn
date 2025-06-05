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
}
