using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
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
    {        private readonly IConversationService _conversationService;
        private readonly IAiChatService _aiChatService;
        private readonly ISimpleLoggerService _logger;
        private readonly ApplicationDbContext _dbContext;
        // REMOVED: IDefaultCharacterService - database-only approach

        /// <summary>
        /// Initializes a new instance of the ConversationController
        /// </summary>
        /// <param name="conversationService">Conversation service</param>
        /// <param name="aiChatService">AI chat service</param>
        /// <param name="logger">Logger service</param>
        /// <param name="dbContext">Database context</param>
        public ConversationController(
            IConversationService conversationService,
            IAiChatService aiChatService,
            ISimpleLoggerService logger,
            ApplicationDbContext dbContext)
        {
            _conversationService = conversationService;
            _aiChatService = aiChatService;
            _logger = logger;
            _dbContext = dbContext;
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
        }        /// <summary>
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
        /// Gets all messages for a conversation
        /// </summary>
        /// <param name="id">Conversation ID</param>
        /// <returns>List of messages</returns>
        [HttpGet("{id}/messages")]
        public async Task<IActionResult> GetMessages(Guid id)
        {
            try
            {
                var messages = await _conversationService.GetMessagesAsync(id);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting messages for conversation {id}", ex);
                return StatusCode(500, "An error occurred while retrieving messages");
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

        /// <summary>
        /// Sends a user message and gets an AI response
        /// </summary>
        /// <param name="request">Chat request</param>
        /// <returns>The AI response</returns>
        [HttpPost("chat")]
        public async Task<IActionResult> SendChatMessage([FromBody] ChatRequest request)
        {
            try
            {
                _logger.LogInfo($"[CHAT] Received chat message for conversation {request.ConversationId}, user {request.UserId}, characterId: '{request.CharacterId ?? "null"}'");

                // Load character system prompt if characterId is provided
                if (!string.IsNullOrEmpty(request.CharacterId))
                {
                    _logger.LogInfo($"[CHAT] Loading character '{request.CharacterId}' into conversation {request.ConversationId}");
                    await LoadCharacterIntoConversationAsync(request.ConversationId, request.CharacterId, request.UserId);
                }

                // Generate and store the AI response
                var aiResponse = await _aiChatService.GenerateAndStoreResponseAsync(
                    request.ConversationId, request.UserId, request.Message);

                _logger.LogInfo($"[CHAT] Successfully generated AI response for conversation {request.ConversationId}");
                return Ok(new { response = aiResponse });
            }
            catch (ArgumentException ex)
            {
                _logger.LogError($"[CHAT] Argument error for conversation {request.ConversationId}: {ex.Message}", ex);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing chat message for conversation {request.ConversationId}", ex);
                return StatusCode(500, "An error occurred while processing the chat message");
            }
        }

        /// <summary>
        /// Sets the character context for a conversation
        /// </summary>
        /// <param name="request">Character context request</param>
        /// <returns>Success response</returns>
        [HttpPost("character-context")]
        public async Task<IActionResult> SetCharacterContext([FromBody] SetCharacterContextRequest request)
        {
            try
            {
                await _conversationService.SetCharacterContextAsync(
                    request.ConversationId, request.UserId, request.CharacterId, request.SystemPrompt);

                return Ok(new { success = true });
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting character context for conversation {request.ConversationId}", ex);
                return StatusCode(500, "An error occurred while setting character context");
            }
        }

        /// <summary>
        /// Loads a character's system prompt into a conversation
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="characterId">Character ID</param>
        /// <param name="userId">User ID</param>
        /// <returns>Task</returns>
        private async Task LoadCharacterIntoConversationAsync(Guid conversationId, string characterId, Guid userId)
        {
            try
            {
                _logger.LogInfo($"[CHARACTER_LOAD] Starting character load - ConversationId: {conversationId}, CharacterId: '{characterId}', UserId: {userId}");

                AvatarInfo? character = null;
                string systemPrompt = "";

                // Handle default character case - load first available character from database
                if (characterId.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInfo($"[CHARACTER_LOAD] Processing 'default' character selection - loading first available character");

                    character = await _dbContext.Avatars
                        .Where(a => a.UserId == userId)
                        .OrderBy(a => a.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (character != null)
                    {
                        _logger.LogInfo($"[CHARACTER_LOAD] ✅ Default character loaded from database - Name: '{character.Name}', Id: {character.Id}");
                        systemPrompt = character.SystemPrompt ?? "";

                        // If no pre-generated prompt exists, generate one as fallback
                        if (string.IsNullOrEmpty(systemPrompt))
                        {
                            systemPrompt = GenerateSystemPromptFromCharacter(character);
                            _logger.LogWarning($"[CHARACTER_LOAD] Character '{character.Name}' has no pre-generated system prompt, using fallback generation");
                        }
                        else
                        {
                            _logger.LogInfo($"[CHARACTER_LOAD] Using pre-generated system prompt (length: {systemPrompt.Length} chars)");
                        }
                    }
                    else
                    {
                        _logger.LogError($"[CHARACTER_LOAD] ❌ No characters found in database for user {userId}");
                        throw new ArgumentException("No characters available for this user");
                    }
                }
                else
                {
                    // Handle specific character GUID
                    _logger.LogInfo($"[CHARACTER_LOAD] Parsing character GUID: '{characterId}'");
                    if (Guid.TryParse(characterId, out var characterGuid))
                    {
                        _logger.LogInfo($"[CHARACTER_LOAD] Loading character from database with GUID: {characterGuid}");
                        character = await _dbContext.Avatars.FirstOrDefaultAsync(a => a.Id == characterGuid && a.UserId == userId);

                        if (character != null)
                        {
                            _logger.LogInfo($"[CHARACTER_LOAD] ✅ Character loaded from database - Name: '{character.Name}', Id: {character.Id}");
                            systemPrompt = character.SystemPrompt ?? "";

                            // If no pre-generated prompt exists, generate one as fallback
                            if (string.IsNullOrEmpty(systemPrompt))
                            {
                                systemPrompt = GenerateSystemPromptFromCharacter(character);
                                _logger.LogWarning($"[CHARACTER_LOAD] Character '{character.Name}' has no pre-generated system prompt, using fallback generation");
                            }
                            else
                            {
                                _logger.LogInfo($"[CHARACTER_LOAD] Using pre-generated system prompt (length: {systemPrompt.Length} chars)");
                            }
                        }
                        else
                        {
                            _logger.LogError($"[CHARACTER_LOAD] ❌ No character found in database with GUID: {characterGuid} for user {userId}");
                            throw new ArgumentException($"Character with ID {characterId} not found");
                        }
                    }
                    else
                    {
                        _logger.LogError($"[CHARACTER_LOAD] ❌ Invalid character GUID format: '{characterId}'");
                        throw new ArgumentException($"Invalid character ID format: {characterId}");
                    }
                }

                // Set character context
                _logger.LogInfo($"[CHARACTER_LOAD] Setting character context for '{character.Name}'");

                // Ensure systemPrompt is not null before setting context
                if (string.IsNullOrEmpty(systemPrompt))
                {
                    systemPrompt = GenerateSystemPromptFromCharacter(character);
                    _logger.LogWarning($"[CHARACTER_LOAD] System prompt was null/empty for '{character.Name}', using generated fallback");
                }

                await _conversationService.SetCharacterContextAsync(conversationId, userId, character.Id, systemPrompt);

                _logger.LogInfo($"[CHARACTER_LOAD] ✅ Successfully loaded character '{character.Name}' into conversation {conversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[CHARACTER_LOAD] ❌ Error loading character '{characterId}' into conversation {conversationId}: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Generates a system prompt from character data
        /// </summary>
        /// <param name="character">Character data</param>
        /// <returns>System prompt string</returns>
        private string GenerateSystemPromptFromCharacter(SwAIvyn.Data.Entities.AvatarInfo character)
        {
            var prompt = $@"You are roleplaying as {character.Name}.

Personality: {character.Personality}

Description: {character.Description}

{(!string.IsNullOrEmpty(character.Scenario) ? $"Scenario: {character.Scenario}" : "")}

{(!string.IsNullOrEmpty(character.FirstMessage) ? $"Start the conversation with: {character.FirstMessage}" : "")}

{(!string.IsNullOrEmpty(character.MessageExample) ? $"Example dialogue: {character.MessageExample}" : "")}

You must respond as this character at all times. Stay in character and respond according to their personality and background.";

            return prompt.Trim();
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
        public required string Title { get; set; }
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
        public required string Title { get; set; }
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
        public required string Role { get; set; }

        /// <summary>
        /// Gets or sets the message content
        /// </summary>
        [Required]
        public required string Content { get; set; }
    }

    /// <summary>
    /// Request to send a chat message and get an AI response
    /// </summary>
    public class ChatRequest
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
        /// Gets or sets the message content
        /// </summary>
        [Required]
        public required string Message { get; set; }

        /// <summary>
        /// Gets or sets the character ID (optional)
        /// </summary>
        public string? CharacterId { get; set; }
    }

    /// <summary>
    /// Request to set character context for a conversation
    /// </summary>
    public class SetCharacterContextRequest
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
        /// Gets or sets the character ID (optional)
        /// </summary>
        public Guid? CharacterId { get; set; }

        /// <summary>
        /// Gets or sets the system prompt for the character
        /// </summary>
        [Required]
        public required string SystemPrompt { get; set; }
    }
}
