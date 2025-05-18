using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for managing conversations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ConversationController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly ISimpleLoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the ConversationController
        /// </summary>
        /// <param name="conversationService">Conversation service</param>
        /// <param name="logger">Logger service</param>
        public ConversationController(
            IConversationService conversationService,
            ISimpleLoggerService logger)
        {
            _conversationService = conversationService;
            _logger = logger;
        }

        /// <summary>
        /// Gets all conversations for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of conversations</returns>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetConversations(Guid userId)
        {
            try
            {
                var conversations = await _conversationService.GetConversationsAsync(userId);
                return Ok(conversations);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting conversations for user {userId}", ex);
                return StatusCode(500, "An error occurred while retrieving conversations");
            }
        }

        /// <summary>
        /// Gets a conversation by ID
        /// </summary>
        /// <param name="id">Conversation ID</param>
        /// <returns>The conversation</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetConversation(Guid id)
        {
            try
            {
                var conversation = await _conversationService.GetConversationAsync(id);
                if (conversation == null)
                    return NotFound();

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting conversation {id}", ex);
                return StatusCode(500, "An error occurred while retrieving the conversation");
            }
        }

        /// <summary>
        /// Creates a new conversation
        /// </summary>
        /// <param name="request">Create conversation request</param>
        /// <returns>The created conversation</returns>
        [HttpPost]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
        {
            try
            {
                var conversation = await _conversationService.CreateConversationAsync(
                    request.UserId, request.FolderId, request.Title);
                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating conversation for user {request.UserId}", ex);
                return StatusCode(500, "An error occurred while creating the conversation");
            }
        }

        /// <summary>
        /// Updates a conversation's title
        /// </summary>
        /// <param name="id">Conversation ID</param>
        /// <param name="request">Update title request</param>
        /// <returns>Success status</returns>
        [HttpPut("{id}/title")]
        public async Task<IActionResult> UpdateTitle(Guid id, [FromBody] UpdateTitleRequest request)
        {
            try
            {
                var success = await _conversationService.UpdateConversationTitleAsync(id, request.Title);
                if (!success)
                    return NotFound();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating conversation title {id}", ex);
                return StatusCode(500, "An error occurred while updating the conversation title");
            }
        }

        /// <summary>
        /// Updates a conversation's folder
        /// </summary>
        /// <param name="id">Conversation ID</param>
        /// <param name="request">Update folder request</param>
        /// <returns>Success status</returns>
        [HttpPut("{id}/folder")]
        public async Task<IActionResult> UpdateFolder(Guid id, [FromBody] UpdateFolderRequest request)
        {
            try
            {
                var success = await _conversationService.UpdateConversationFolderAsync(id, request.FolderId);
                if (!success)
                    return NotFound();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating conversation folder {id}", ex);
                return StatusCode(500, "An error occurred while updating the conversation folder");
            }
        }

        /// <summary>
        /// Updates a conversation's last open time
        /// </summary>
        /// <param name="id">Conversation ID</param>
        /// <returns>Success status</returns>
        [HttpPut("{id}/open")]
        public async Task<IActionResult> UpdateLastOpenTime(Guid id)
        {
            try
            {
                var success = await _conversationService.UpdateLastOpenTimeAsync(id);
                if (!success)
                    return NotFound();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating conversation last open time {id}", ex);
                return StatusCode(500, "An error occurred while updating the conversation last open time");
            }
        }

        /// <summary>
        /// Deletes a conversation
        /// </summary>
        /// <param name="id">Conversation ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConversation(Guid id)
        {
            try
            {
                var success = await _conversationService.DeleteConversationAsync(id);
                if (!success)
                    return NotFound();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting conversation {id}", ex);
                return StatusCode(500, "An error occurred while deleting the conversation");
            }
        }

        /// <summary>
        /// Gets the most recently opened conversation for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>The most recent conversation</returns>
        [HttpGet("recent/{userId}")]
        public async Task<IActionResult> GetRecentConversation(Guid userId)
        {
            try
            {
                var conversation = await _conversationService.GetLastOpenConversationAsync(userId);
                if (conversation == null)
                    return NotFound();

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting recent conversation for user {userId}", ex);
                return StatusCode(500, "An error occurred while retrieving the recent conversation");
            }
        }

        /// <summary>
        /// Appends a message to a conversation
        /// </summary>
        /// <param name="request">Append message request</param>
        /// <returns>The created chat index entry</returns>
        [HttpPost("message")]
        public async Task<IActionResult> AppendMessage([FromBody] AppendMessageRequest request)
        {
            try
            {
                var chatIndex = await _conversationService.AppendMessageAsync(
                    request.ConversationId, request.UserId, request.Role, request.Content);
                return Ok(chatIndex);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error appending message to conversation {request.ConversationId}", ex);
                return StatusCode(500, "An error occurred while appending the message");
            }
        }
    }

    /// <summary>
    /// Request to create a conversation
    /// </summary>
    public class CreateConversationRequest
    {
        /// <summary>
        /// Gets or sets the user ID
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the folder ID
        /// </summary>
        public Guid? FolderId { get; set; }

        /// <summary>
        /// Gets or sets the conversation title
        /// </summary>
        [Required]
        public string Title { get; set; }
    }

    /// <summary>
    /// Request to update a conversation's title
    /// </summary>
    public class UpdateTitleRequest
    {
        /// <summary>
        /// Gets or sets the new title
        /// </summary>
        [Required]
        public string Title { get; set; }
    }

    /// <summary>
    /// Request to update a conversation's folder
    /// </summary>
    public class UpdateFolderRequest
    {
        /// <summary>
        /// Gets or sets the new folder ID
        /// </summary>
        public Guid? FolderId { get; set; }
    }

    /// <summary>
    /// Request to append a message to a conversation
    /// </summary>
    public class AppendMessageRequest
    {
        /// <summary>
        /// Gets or sets the conversation ID
        /// </summary>
        [Required]
        public Guid ConversationId { get; set; }

        /// <summary>
        /// Gets or sets the user ID
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the message role (user, assistant, system)
        /// </summary>
        [Required]
        public string Role { get; set; }

        /// <summary>
        /// Gets or sets the message content
        /// </summary>
        [Required]
        public string Content { get; set; }
    }
}
