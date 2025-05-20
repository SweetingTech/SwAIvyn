using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : ControllerBase
    {
        private readonly IConfigurationService _configService;
        private readonly ISimpleLoggerService _logger;

        public ConfigController(
            IConfigurationService configService,
            ISimpleLoggerService logger)
        {
            _configService = configService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetConfig()
        {
            try
            {
                var endpoints = _configService.GetAllEndpoints();
                return Ok(endpoints);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting configuration", ex);
                return StatusCode(500, "An error occurred while getting configuration");
            }
        }
    }
}
