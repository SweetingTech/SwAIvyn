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
        }        /// <summary>
        /// Gets the single default user, creating one if it doesn't exist.
        /// This is a single-user application.
        /// </summary>
        [HttpGet("default")]
        public async Task<IActionResult> GetDefaultUser()
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync();

            if (user == null)
            {
                // Create the default user if none exists - use the same fixed GUID as Program.cs
                user = new AppUser
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
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
        /// Gets a user by ID. Since this is a single-user application,
        /// this will return the default user regardless of the ID provided.
        /// </summary>
        /// <param name="userId">User ID (ignored in single-user mode)</param>
        /// <returns>The default user</returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUser(Guid userId)
        {
            // In single-user mode, always return the default user
            var user = await _dbContext.Users.FirstOrDefaultAsync();

            if (user == null)
            {
                // Create the default user if none exists
                user = new AppUser
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
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

            // Validate lastSelectedCharacterId - clear if character doesn't exist
            if (user.LastSelectedCharacterId.HasValue)
            {
                var characterExists = await _dbContext.Avatars
                    .AnyAsync(a => a.Id == user.LastSelectedCharacterId.Value && a.UserId == user.Id);

                if (!characterExists)
                {
                    Console.WriteLine($"User {user.Id} has invalid lastSelectedCharacterId {user.LastSelectedCharacterId}, clearing it");
                    user.LastSelectedCharacterId = null;
                    await _dbContext.SaveChangesAsync();
                }
            }

            return Ok(new {
                id = user.Id,
                username = user.Username,
                createdAt = user.CreatedAt,
                lastLogin = user.LastLogin,
                lastSelectedCharacterId = user.LastSelectedCharacterId
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
        /// Updates a user by ID (in single-user mode, updates the default user)
        /// </summary>
        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound("No user found.");
            }

            // Update last selected character if provided
            if (request.LastSelectedCharacterId.HasValue)
            {
                // Validate that the character exists for this user
                var characterExists = await _dbContext.Avatars
                    .AnyAsync(a => a.Id == request.LastSelectedCharacterId.Value && a.UserId == user.Id);

                if (characterExists)
                {
                    user.LastSelectedCharacterId = request.LastSelectedCharacterId.Value;
                }
                else
                {
                    Console.WriteLine($"Attempted to set invalid character ID {request.LastSelectedCharacterId.Value} for user {user.Id}");
                    return BadRequest($"Character with ID {request.LastSelectedCharacterId.Value} not found for this user");
                }
            }

            // Update username if provided
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
                lastLogin = user.LastLogin,
                lastSelectedCharacterId = user.LastSelectedCharacterId
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
    /// Request model for updating user data
    /// </summary>
    public class UpdateUserRequest
    {
        public Guid? LastSelectedCharacterId { get; set; }
        public string Username { get; set; }
    }

    /// <summary>
    /// Request model for updating user profile
    /// </summary>
    public class UpdateUserProfileRequest
    {
        public string Username { get; set; }
    }
}
