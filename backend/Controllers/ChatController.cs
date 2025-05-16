using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetChatHistory()
        {
            // Placeholder for getting chat history
            return Ok(new { message = "Chat history retrieved successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] object message)
        {
            // Placeholder for sending a message
            return Ok(new { message = "Message sent successfully" });
        }
    }
}
