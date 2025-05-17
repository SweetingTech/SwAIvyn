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
    /// Controller for managing chat conversations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ConversationController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public ConversationController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Gets all conversations for a user.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of conversations</returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetConversations(Guid userId)
        {
            var conversations = await _dbContext.Conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LastUpdated)
                .ToListAsync();

            return Ok(conversations);
        }

        /// <summary>
        /// Creates a new conversation.
        /// </summary>
        /// <param name="request">Conversation creation request</param>
        /// <returns>Created conversation</returns>
        [HttpPost]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
        {
            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Title = request.Title,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            _dbContext.Conversations.Add(conversation);
            await _dbContext.SaveChangesAsync();

            return Ok(conversation);
        }

        /// <summary>
        /// Deletes a conversation.
        /// </summary>
        /// <param name="id">Conversation ID</param>
        /// <returns>Action result</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConversation(Guid id)
        {
            var conversation = await _dbContext.Conversations.FindAsync(id);
            if (conversation == null)
            {
                return NotFound();
            }

            _dbContext.Conversations.Remove(conversation);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }

    /// <summary>
    /// Request model for creating a conversation.
    /// </summary>
    public class CreateConversationRequest
    {
        public Guid UserId { get; set; }
        public string Title { get; set; }
    }
}
