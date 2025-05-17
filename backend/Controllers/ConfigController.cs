using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : ControllerBase
    {
        private readonly IConfigurationService _configService;

        public ConfigController(IConfigurationService configService)
        {
            _configService = configService;
        }

        [HttpGet]
        public IActionResult GetConfig()
        {
            return Ok(_configService.GetAllEndpoints());
        }
    }
}
