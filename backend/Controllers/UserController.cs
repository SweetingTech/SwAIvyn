using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;

namespace SwAIvyn.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public UserController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("default")]
        public async Task<IActionResult> GetDefaultUser()
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound("No default user found.");
            }
            return Ok(new { id = user.Id });
        }
    }
}
