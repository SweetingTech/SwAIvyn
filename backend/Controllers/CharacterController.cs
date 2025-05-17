using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for managing AI character profiles.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CharacterController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public CharacterController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Gets all character profiles for a user.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of character profiles</returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetCharacters(Guid userId)
        {
            var characters = await _dbContext.Avatars
                .Where(c => c.UserId == userId)
                .ToListAsync();

            return Ok(characters);
        }

        /// <summary>
        /// Creates a new character profile.
        /// </summary>
        /// <param name="request">Character creation request</param>
        /// <returns>Created character profile</returns>
        [HttpPost]
        public async Task<IActionResult> CreateCharacter([FromBody] CreateCharacterRequest request)
        {
            var character = new AvatarInfo
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Name = request.Name,
                ImagePath = request.ImagePath,
                Personality = request.Personality,
                VoiceSettings = request.VoiceSettings,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            _dbContext.Avatars.Add(character);
            await _dbContext.SaveChangesAsync();

            return Ok(character);
        }

        /// <summary>
        /// Updates an existing character profile.
        /// </summary>
        /// <param name="id">Character ID</param>
        /// <param name="request">Character update request</param>
        /// <returns>Updated character profile</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCharacter(Guid id, [FromBody] UpdateCharacterRequest request)
        {
            var character = await _dbContext.Avatars.FindAsync(id);
            if (character == null)
            {
                return NotFound();
            }

            character.Name = request.Name;
            character.ImagePath = request.ImagePath;
            character.Personality = request.Personality;
            character.VoiceSettings = request.VoiceSettings;
            character.LastModified = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(character);
        }

        /// <summary>
        /// Deletes a character profile.
        /// </summary>
        /// <param name="id">Character ID</param>
        /// <returns>Action result</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCharacter(Guid id)
        {
            var character = await _dbContext.Avatars.FindAsync(id);
            if (character == null)
            {
                return NotFound();
            }

            _dbContext.Avatars.Remove(character);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }

    /// <summary>
    /// Request model for creating a character profile.
    /// </summary>
    public class CreateCharacterRequest
    {
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public string Personality { get; set; }
        public string VoiceSettings { get; set; }
    }

    /// <summary>
    /// Request model for updating a character profile.
    /// </summary>
    public class UpdateCharacterRequest
    {
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public string Personality { get; set; }
        public string VoiceSettings { get; set; }
    }
}
