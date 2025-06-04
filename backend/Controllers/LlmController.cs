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
        private readonly ISettingsService _settingsService;
        private readonly IAiChatService _aiChatService;
        private readonly ISimpleLoggerService _logger;

        public LlmController(
            ILlmConnectorService llmConnectorService,
            ISettingsService settingsService,
            IAiChatService aiChatService,
            ISimpleLoggerService logger)
        {
            _llmConnectorService = llmConnectorService;
            _settingsService = settingsService;
            _aiChatService = aiChatService;
            _logger = logger;
        }

        /// <summary>
        /// Gets available Ollama models
        /// </summary>
        /// <param name="userId">User ID (optional)</param>
        /// <returns>List of available Ollama models</returns>
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
        /// Gets the current LM Studio model
        /// </summary>
        /// <param name="userId">User ID (optional)</param>
        /// <returns>Current LM Studio model name</returns>
        [HttpGet("lmstudio/model")]
        public async Task<IActionResult> GetLmStudioModel([FromQuery] Guid? userId = null)
        {
            try
            {
                var model = await _llmConnectorService.GetLmStudioModelAsync(userId);
                return Ok(new { model = model });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting LM Studio model", ex);
                return StatusCode(500, "An error occurred while getting LM Studio model");
            }
        }

        /// <summary>
        /// Tests connection to Ollama
        /// </summary>
        /// <param name="userId">User ID (optional)</param>
        /// <returns>Connection status</returns>
        [HttpGet("ollama/test")]
        public async Task<IActionResult> TestOllamaConnection([FromQuery] Guid? userId = null)
        {
            try
            {
                var models = await _llmConnectorService.GetOllamaModelsAsync(userId);
                var modelCount = models?.Count() ?? 0;
                return Ok(new {
                    connected = true,
                    modelCount = modelCount,
                    message = $"Connected successfully. Found {modelCount} models."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing Ollama connection", ex);
                return Ok(new {
                    connected = false,
                    modelCount = 0,
                    message = $"Connection failed: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Tests connection to LM Studio
        /// </summary>
        /// <param name="userId">User ID (optional)</param>
        /// <returns>Connection status</returns>
        [HttpGet("lmstudio/test")]
        public async Task<IActionResult> TestLmStudioConnection([FromQuery] Guid? userId = null)
        {
            try
            {
                var model = await _llmConnectorService.GetLmStudioModelAsync(userId);
                return Ok(new {
                    connected = true,
                    model = model,
                    message = $"Connected successfully. Current model: {model}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error testing LM Studio connection", ex);
                return Ok(new {
                    connected = false,
                    model = "Unknown",
                    message = $"Connection failed: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets all available models from all engines
        /// </summary>
        /// <param name="userId">User ID (optional)</param>
        /// <returns>Models grouped by engine</returns>
        [HttpGet("models")]
        public async Task<IActionResult> GetAllModels([FromQuery] Guid? userId = null)
        {
            try
            {
                var result = new Dictionary<string, object>();

                // Get Ollama models
                try
                {
                    var ollamaModels = await _llmConnectorService.GetOllamaModelsAsync(userId);
                    result["ollama"] = new {
                        available = true,
                        models = ollamaModels?.ToList() ?? new List<string>()
                    };
                }
                catch (Exception ex)
                {
                    result["ollama"] = new {
                        available = false,
                        error = ex.Message,
                        models = new List<string>()
                    };
                }

                // Get LM Studio model
                try
                {
                    var lmStudioModel = await _llmConnectorService.GetLmStudioModelAsync(userId);
                    result["lmstudio"] = new {
                        available = true,
                        models = new List<string> { lmStudioModel }
                    };
                }
                catch (Exception ex)
                {
                    result["lmstudio"] = new {
                        available = false,
                        error = ex.Message,
                        models = new List<string>()
                    };
                }

                // Get OpenAI models
                try
                {
                    var openAiModels = await _llmConnectorService.GetOpenAiModelsAsync(userId);
                    result["openai"] = new {
                        available = true,
                        models = openAiModels?.ToList() ?? new List<string>()
                    };
                }
                catch (Exception ex)
                {
                    result["openai"] = new {
                        available = false,
                        error = ex.Message,
                        models = new List<string>()
                    };
                }

                // Get Claude models
                try
                {
                    var claudeModels = await _llmConnectorService.GetClaudeModelsAsync(userId);
                    result["claude"] = new {
                        available = true,
                        models = claudeModels?.ToList() ?? new List<string>()
                    };
                }
                catch (Exception ex)
                {
                    result["claude"] = new {
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

        /// <summary>
        /// Gets available engines
        /// </summary>
        /// <returns>List of available engines</returns>
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
                        description = "OpenAI models",
                        defaultUrl = "https://api.openai.com/v1"
                    },
                    new {
                        id = "claude",
                        name = "Claude",
                        description = "Anthropic Claude models",
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

        /// <summary>
        /// Gets current LLM settings
        /// </summary>
        /// <param name="userId">User ID (optional)</param>
        /// <returns>Current LLM settings</returns>
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
        /// Sets the default LLM engine and model
        /// </summary>
        /// <param name="request">LLM settings request</param>
        /// <returns>Success status</returns>
        [HttpPost("settings")]
        public async Task<IActionResult> SetLlmSettings([FromBody] SetLlmSettingsRequest request)
        {
            try
            {
                var success = await _aiChatService.SetDefaultLlmSettingsAsync(request.UserId, request.Engine, request.Model);
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

        /// <summary>
        /// Generates a response using the specified engine and model
        /// </summary>
        /// <param name="request">Generation request</param>
        /// <returns>AI response</returns>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateResponse([FromBody] GenerateRequest request)
        {
            try
            {
                var response = await _llmConnectorService.GenerateResponseAsync(
                    request.Prompt,
                    request.Engine ?? "ollama",
                    request.Model,
                    request.UserId);

                return Ok(new { response = response });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating response", ex);
                return StatusCode(500, $"An error occurred while generating response: {ex.Message}");
            }
        }

        [HttpPost("openai/generate")]
        public async Task<IActionResult> GenerateOpenAi([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                var response = await _llmConnectorService.GenerateOpenAiResponseAsync(request.Messages, request.Model, request.UserId);
                return Ok(new { response });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating OpenAI response", ex);
                return StatusCode(500, $"An error occurred while generating response: {ex.Message}");
            }
        }

        [HttpPost("claude/generate")]
        public async Task<IActionResult> GenerateClaude([FromBody] GenerateMessagesRequest request)
        {
            try
            {
                var response = await _llmConnectorService.GenerateClaudeResponseAsync(request.Messages, request.Model, request.UserId);
                return Ok(new { response });
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
    /// Request model for generating responses
    /// </summary>
    public class GenerateRequest
    {
        public string Prompt { get; set; }
        public string Engine { get; set; }
        public string Model { get; set; }
        public Guid? UserId { get; set; }
    }

    /// <summary>
    /// Request model for generating responses with message format
    /// </summary>
    public class GenerateMessagesRequest
    {
        public List<Dictionary<string, string>> Messages { get; set; }
        public string Model { get; set; }
        public Guid? UserId { get; set; }
    }
}
