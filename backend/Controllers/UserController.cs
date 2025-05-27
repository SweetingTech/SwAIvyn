using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;

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

        /// <summary>
        /// Gets the single default user, creating one if it doesn't exist.
        /// This is a single-user application.
        /// </summary>
        [HttpGet("default")]
        public async Task<IActionResult> GetDefaultUser()
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync();
            
            if (user == null)
            {
                // Create the default user if none exists
                user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    Username = "Default User",
                    PasswordHash = "", // No password needed for single-user app
                    PINCode = "",
                    RecoveryPhrase = "",
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { 
                id = user.Id, 
                username = user.Username,
                createdAt = user.CreatedAt,
                lastLogin = user.LastLogin 
            });
        }

        /// <summary>
        /// Gets the full profile of the default user
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync();
            
            if (user == null)
            {
                return NotFound("No user found.");
            }

            return Ok(new { 
                id = user.Id, 
                username = user.Username,
                createdAt = user.CreatedAt,
                lastLogin = user.LastLogin 
            });
        }

        /// <summary>
        /// Updates the default user's profile information
        /// </summary>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateUserProfile([FromBody] UpdateUserProfileRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync();
            
            if (user == null)
            {
                return NotFound("No user found.");
            }

            // Update user information
            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                user.Username = request.Username.Trim();
            }

            user.LastLogin = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync();

            return Ok(new { 
                id = user.Id, 
                username = user.Username,
                createdAt = user.CreatedAt,
                lastLogin = user.LastLogin 
            });
        }
    }

    /// <summary>
    /// Request model for updating user profile
    /// </summary>
    public class UpdateUserProfileRequest
    {
        public string Username { get; set; }
    }
}
