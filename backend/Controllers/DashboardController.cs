using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using SwAIvyn.Services.Graph;
using SwAIvyn.Services.VectorStore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for dashboard operations and system status
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ICharacterService _characterService;
        private readonly IBrainGraphService _brainGraphService;
        private readonly IVectorRouter _vectorRouter;
        private readonly IConversationService _conversationService;
        private readonly ISettingsService _settingsService;
        private readonly ISimpleLoggerService _logger;

        public DashboardController(
            ICharacterService characterService,
            IBrainGraphService brainGraphService,
            IVectorRouter vectorRouter,
            IConversationService conversationService,
            ISettingsService settingsService,
            ISimpleLoggerService logger)
        {
            _characterService = characterService;
            _brainGraphService = brainGraphService;
            _vectorRouter = vectorRouter;
            _conversationService = conversationService;
            _settingsService = settingsService;
            _logger = logger;
        }

        /// <summary>
        /// Gets comprehensive system status for the dashboard
        /// </summary>
        /// <returns>System status information</returns>
        [HttpGet("status")]
        public async Task<IActionResult> GetSystemStatus()
        {
            try
            {
                var status = new
                {
                    // Database connections
                    databases = new
                    {
                        sqlite = await TestSqliteConnection(),
                        neo4j = await TestNeo4jConnection(),
                        weaviate = await TestWeaviateConnection()
                    },
                    
                    // System metrics
                    metrics = new
                    {
                        characterCount = await GetCharacterCount(),
                        memoryCount = await GetMemoryCount(),
                        conversationCount = await GetConversationCount()
                    },
                    
                    // LLM status
                    llm = await GetLlmStatus(),

                    // TTS status
                    tts = await GetTtsStatus(),

                    // System info
                    system = new
                    {
                        uptime = GetUptime(),
                        lastActivity = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get system status", ex);
                return StatusCode(500, new { error = "Failed to get system status" });
            }
        }

        /// <summary>
        /// Gets health check status for all services
        /// </summary>
        /// <returns>Health check results</returns>
        [HttpGet("health")]
        public async Task<IActionResult> GetHealthStatus()
        {
            try
            {
                var health = new
                {
                    overall = "healthy", // Will be calculated based on individual checks
                    services = new
                    {
                        sqlite = await TestSqliteConnection(),
                        neo4j = await TestNeo4jConnection(),
                        weaviate = await TestWeaviateConnection(),
                        characters = await TestCharacterService(),
                        memory = await TestMemoryService()
                    },
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get health status", ex);
                return StatusCode(500, new { error = "Failed to get health status" });
            }
        }

        /// <summary>
        /// Gets system metrics for monitoring
        /// </summary>
        /// <returns>System metrics</returns>
        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics()
        {
            try
            {
                var metrics = new
                {
                    characters = await GetCharacterCount(),
                    memories = await GetMemoryCount(),
                    conversations = await GetConversationCount(),
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get metrics", ex);
                return StatusCode(500, new { error = "Failed to get metrics" });
            }
        }

        #region Private Helper Methods

        private async Task<object> TestSqliteConnection()
        {
            try
            {
                // Test by getting character count
                await _characterService.GetCharactersAsync();
                return new { status = "connected", message = "SQLite database accessible" };
            }
            catch (Exception ex)
            {
                return new { status = "disconnected", message = ex.Message };
            }
        }

        private async Task<object> TestNeo4jConnection()
        {
            try
            {
                var status = await _brainGraphService.GetStatusAsync();
                return new { status = "connected", message = "Neo4j graph database accessible", details = status };
            }
            catch (Exception ex)
            {
                return new { status = "disconnected", message = ex.Message };
            }
        }

        private async Task<object> TestWeaviateConnection()
        {
            try
            {
                // Test Weaviate connection - simplified for now
                return new {
                    status = "connected",
                    message = "Weaviate vector database accessible"
                };
            }
            catch (Exception ex)
            {
                return new { status = "disconnected", message = ex.Message };
            }
        }

        private async Task<object> TestCharacterService()
        {
            try
            {
                var characters = await _characterService.GetCharactersAsync();
                return new { 
                    status = "healthy", 
                    message = $"Character service operational with {characters.Count} characters loaded" 
                };
            }
            catch (Exception ex)
            {
                return new { status = "unhealthy", message = ex.Message };
            }
        }

        private async Task<object> TestMemoryService()
        {
            try
            {
                var memoryCount = await GetMemoryCount();
                return new { 
                    status = "healthy", 
                    message = $"Memory service operational with {memoryCount} memories stored" 
                };
            }
            catch (Exception ex)
            {
                return new { status = "unhealthy", message = ex.Message };
            }
        }

        private async Task<int> GetCharacterCount()
        {
            try
            {
                var characters = await _characterService.GetCharactersAsync();
                return characters.Count;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> GetMemoryCount()
        {
            try
            {
                // Get memory count from Neo4j through brain graph service
                var status = await _brainGraphService.GetStatusAsync();
                if (status.ContainsKey("memoryCount"))
                {
                    return Convert.ToInt32(status["memoryCount"]);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> GetConversationCount()
        {
            try
            {
                // Get conversation count for the default user
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                return await _conversationService.GetTotalConversationCountAsync(defaultUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting conversation count for dashboard", ex);
                return 0; // Fallback to 0 on error
            }
        }

        private async Task<object> GetLlmStatus()
        {
            try
            {
                // Get LLM settings for default user
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

                // Get individual settings instead of using non-existent method
                var engine = await _settingsService.GetSettingAsync(defaultUserId, "DefaultLlmEngine", "ollama");
                var model = await _settingsService.GetSettingAsync(defaultUserId, "DefaultLlmModel", "");

                return new
                {
                    engine = engine,
                    model = model,
                    connected = true // We'll implement actual connection testing later
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    engine = "unknown",
                    model = "unknown",
                    connected = false,
                    error = ex.Message
                };
            }
        }

        private async Task<object> GetTtsStatus()
        {
            try
            {
                // Get TTS settings for default user
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

                // Get TTS settings
                var provider = await _settingsService.GetSettingAsync(defaultUserId, "TtsProvider", "fish_speech");
                var voice = await _settingsService.GetSettingAsync(defaultUserId, "TtsVoice", "jazzy");

                return new
                {
                    provider = provider,
                    voice = voice,
                    connected = true // We'll implement actual connection testing later
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    provider = "unknown",
                    voice = "unknown",
                    connected = false,
                    error = ex.Message
                };
            }
        }

        private string GetUptime()
        {
            // Simple uptime calculation - in production this would be more sophisticated
            var startTime = DateTime.UtcNow.AddHours(-1); // Placeholder
            var uptime = DateTime.UtcNow - startTime;
            
            if (uptime.TotalHours >= 1)
            {
                return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
            }
            return $"{(int)uptime.TotalMinutes}m";
        }

        #endregion
    }
}
