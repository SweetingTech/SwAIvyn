using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModulesController : ControllerBase
    {
        private readonly IModuleService _moduleService;
        private readonly ISimpleLoggerService _logger;

        public ModulesController(IModuleService moduleService, ISimpleLoggerService logger)
        {
            _moduleService = moduleService;
            _logger = logger;
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetModulesForUser(Guid? userId)
        {
            try
            {
                if (userId == null)
                {
                    // Use the default/only user ID if none provided or handle as appropriate
                    // For this app, it seems '00000000-0000-0000-0000-000000000001' is used as a default
                    // However, the frontend sends the actual user ID.
                    // Let's assume a userId must be provided for now, or fall back to a default if that's the design.
                    // For now, let's proceed with the userId passed.
                }
                _logger.LogInfo($"Fetching modules for user: {userId}");
                var modules = await _moduleService.GetModulesAsync(userId);
                return Ok(modules);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting modules for user {userId}: {ex.Message}", ex);
                return StatusCode(500, $"An error occurred while getting modules: {ex.Message}");
            }
        }
    }
}
