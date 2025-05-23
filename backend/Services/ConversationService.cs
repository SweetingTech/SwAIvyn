using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for conversation management service
    /// </summary>
    public interface IConversationService
    {
        /// <summary>
        /// Creates a new conversation
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="folderId">Optional folder ID</param>
        /// <param name="title">Optional title (defaults to "Untitled")</param>
        /// <returns>The created conversation</returns>
        Task<Conversation> CreateConversationAsync(Guid userId, Guid? folderId = null, string title = "Untitled");

        /// <summary>
        /// Gets all conversations for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of conversations</returns>
        Task<List<Conversation>> GetConversationsAsync(Guid userId);

        /// <summary>
        /// Gets a conversation by ID
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <returns>The conversation or null if not found</returns>
        Task<Conversation> GetConversationAsync(Guid conversationId);

        /// <summary>
        /// Gets a conversation by ID for a specific user
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="userId">User ID</param>
        /// <returns>The conversation or null if not found</returns>
        Task<Conversation> GetConversationAsync(Guid conversationId, Guid userId);

        /// <summary>
        /// Updates a conversation's folder
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="folderId">New folder ID</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateConversationFolderAsync(Guid conversationId, Guid? folderId);

        /// <summary>
        /// Updates a conversation's title
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="title">New title</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateConversationTitleAsync(Guid conversationId, string title);

        /// <summary>
        /// Updates a conversation's last open time
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateLastOpenTimeAsync(Guid conversationId);

        /// <summary>
        /// Deletes a conversation
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteConversationAsync(Guid conversationId);

        /// <summary>
        /// Appends a message to a conversation
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="role">Message role (user, assistant, system)</param>
        /// <param name="content">Message content</param>
        /// <returns>The created chat index entry</returns>
        Task<ChatIndex> AppendMessageAsync(Guid conversationId, Guid userId, string role, string content);

        /// <summary>
        /// Gets the most recently opened conversation for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>The most recent conversation or null if none exists</returns>
        Task<Conversation> GetLastOpenConversationAsync(Guid userId);

        /// <summary>
        /// Sets the character context for a conversation
        /// </summary>
        /// <param name="conversationId">Conversation ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="characterId">Character ID (optional)</param>
        /// <param name="systemPrompt">Character system prompt</param>
        /// <returns>True if successful</returns>
        Task<bool> SetCharacterContextAsync(Guid conversationId, Guid userId, Guid? characterId, string systemPrompt);
    }

    /// <summary>
    /// Service for managing conversations and chat messages
    /// </summary>
    public class ConversationService : IConversationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ISimpleLoggerService _logger;
        private readonly string _sessionsDirectory;

        /// <summary>
        /// Initializes a new instance of the ConversationService
        /// </summary>
        /// <param name="dbContext">Database context</param>
        /// <param name="logger">Logger service</param>
        /// <param name="configuration">Application configuration</param>
        public ConversationService(
            ApplicationDbContext dbContext,
            ISimpleLoggerService logger,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _sessionsDirectory = configuration["AppSettings:SessionsDirectory"] ?? "../data/sessions";
        }

        /// <inheritdoc/>
        public async Task<Conversation> CreateConversationAsync(Guid userId, Guid? folderId = null, string title = "Untitled")
        {
            try
            {
                var conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FolderId = folderId,
                    Title = title,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow,
                    LastOpenUtc = DateTime.UtcNow,
                    Summary = "",
                    Status = "Active",
                    Tags = ""
                };

                _dbContext.Conversations.Add(conversation);
                await _dbContext.SaveChangesAsync();

                // Create the conversation directory
                var conversationDir = Path.Combine(_sessionsDirectory, conversation.Id.ToString());
                Directory.CreateDirectory(conversationDir);

                _logger.LogInfo($"Created conversation: {conversation.Id} for user: {userId}");
                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create conversation for user: {userId}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<Conversation>> GetConversationsAsync(Guid userId)
        {
            return await _dbContext.Conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LastOpenUtc)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<Conversation> GetConversationAsync(Guid conversationId)
        {
            return await _dbContext.Conversations.FindAsync(conversationId);
        }

        /// <inheritdoc/>
        public async Task<Conversation> GetConversationAsync(Guid conversationId, Guid userId)
        {
            return await _dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateConversationFolderAsync(Guid conversationId, Guid? folderId)
        {
            var conversation = await _dbContext.Conversations.FindAsync(conversationId);
            if (conversation == null)
                return false;

            conversation.FolderId = folderId;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateConversationTitleAsync(Guid conversationId, string title)
        {
            var conversation = await _dbContext.Conversations.FindAsync(conversationId);
            if (conversation == null)
                return false;

            conversation.Title = title;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateLastOpenTimeAsync(Guid conversationId)
        {
            var conversation = await _dbContext.Conversations.FindAsync(conversationId);
            if (conversation == null)
                return false;

            conversation.LastOpenUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteConversationAsync(Guid conversationId)
        {
            var conversation = await _dbContext.Conversations.FindAsync(conversationId);
            if (conversation == null)
                return false;

            _dbContext.Conversations.Remove(conversation);
            await _dbContext.SaveChangesAsync();

            // Delete the conversation directory
            var conversationDir = Path.Combine(_sessionsDirectory, conversationId.ToString());
            if (Directory.Exists(conversationDir))
            {
                Directory.Delete(conversationDir, true);
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<ChatIndex> AppendMessageAsync(Guid conversationId, Guid userId, string role, string content)
        {
            // Ensure the conversation exists
            var conversation = await _dbContext.Conversations.FindAsync(conversationId);
            if (conversation == null)
                throw new ArgumentException($"Conversation not found: {conversationId}");

            // Create the conversation directory if it doesn't exist
            var conversationDir = Path.Combine(_sessionsDirectory, conversationId.ToString());
            Directory.CreateDirectory(conversationDir);

            // Create a timestamp for the file
            var timestamp = DateTime.UtcNow;
            var fileName = $"{timestamp:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(conversationDir, fileName);

            // Create the message object
            var message = new
            {
                role,
                content,
                timestamp = timestamp.ToString("o")
            };

            // Write the message to the file
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(message));

            // Create a chat index entry
            var chatIndex = new ChatIndex
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                MessageId = Guid.NewGuid(),
                Content = content,
                ContentType = "text",
                Role = role,
                FilePath = Path.Combine("sessions", conversationId.ToString(), fileName),
                CreatedUtc = timestamp,
                Embedding = "",
                Metadata = "{}"
            };

            _dbContext.ChatIndices.Add(chatIndex);
            await _dbContext.SaveChangesAsync();

            // Update the conversation's last open time
            conversation.LastOpenUtc = timestamp;
            await _dbContext.SaveChangesAsync();

            return chatIndex;
        }

        /// <inheritdoc/>
        public async Task<Conversation> GetLastOpenConversationAsync(Guid userId)
        {
            return await _dbContext.Conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LastOpenUtc)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> SetCharacterContextAsync(Guid conversationId, Guid userId, Guid? characterId, string systemPrompt)
        {
            try
            {
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

                if (conversation == null)
                {
                    throw new ArgumentException($"Conversation not found: {conversationId}");
                }

                conversation.CharacterId = characterId;
                conversation.CharacterSystemPrompt = systemPrompt;
                conversation.UpdatedUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInfo($"Set character context for conversation {conversationId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set character context for conversation {conversationId}", ex);
                throw;
            }
        }
    }
}
