using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for folder management service
    /// </summary>
    public interface IFolderService
    {
        /// <summary>
        /// Creates a new folder
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="name">Folder name</param>
        /// <param name="parentId">Optional parent folder ID</param>
        /// <returns>The created folder</returns>
        Task<Folder> CreateFolderAsync(Guid userId, string name, Guid? parentId = null);

        /// <summary>
        /// Gets all folders for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of folders</returns>
        Task<List<Folder>> GetFoldersAsync(Guid userId);

        /// <summary>
        /// Gets a folder by ID
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>The folder or null if not found</returns>
        Task<Folder> GetFolderAsync(Guid folderId);

        /// <summary>
        /// Updates a folder's name
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="name">New name</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateFolderNameAsync(Guid folderId, string name);

        /// <summary>
        /// Updates a folder's parent
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="parentId">New parent ID</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateFolderParentAsync(Guid folderId, Guid? parentId);

        /// <summary>
        /// Deletes a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteFolderAsync(Guid folderId);

        /// <summary>
        /// Gets the root folder for a user, creating it if it doesn't exist
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>The root folder</returns>
        Task<Folder> GetOrCreateRootFolderAsync(Guid userId);
    }

    /// <summary>
    /// Service for managing folders
    /// </summary>
    public class FolderService : IFolderService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ISimpleLoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the FolderService
        /// </summary>
        /// <param name="dbContext">Database context</param>
        /// <param name="logger">Logger service</param>
        public FolderService(
            ApplicationDbContext dbContext,
            ISimpleLoggerService logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<Folder> CreateFolderAsync(Guid userId, string name, Guid? parentId = null)
        {
            try
            {
                var folder = new Folder
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = name,
                    ParentId = parentId,
                    CreatedUtc = DateTime.UtcNow
                };

                _dbContext.Folders.Add(folder);
                await _dbContext.SaveChangesAsync();

                _logger.LogInfo($"Created folder: {folder.Id} for user: {userId}");
                return folder;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create folder for user: {userId}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<Folder>> GetFoldersAsync(Guid userId)
        {
            return await _dbContext.Folders
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<Folder> GetFolderAsync(Guid folderId)
        {
            return await _dbContext.Folders.FindAsync(folderId);
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateFolderNameAsync(Guid folderId, string name)
        {
            var folder = await _dbContext.Folders.FindAsync(folderId);
            if (folder == null)
                return false;

            folder.Name = name;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateFolderParentAsync(Guid folderId, Guid? parentId)
        {
            var folder = await _dbContext.Folders.FindAsync(folderId);
            if (folder == null)
                return false;

            // Prevent circular references
            if (parentId.HasValue)
            {
                var parent = await _dbContext.Folders.FindAsync(parentId.Value);
                if (parent == null)
                    return false;

                // Check if the new parent is a descendant of this folder
                if (await IsDescendantAsync(folderId, parentId.Value))
                    return false;
            }

            folder.ParentId = parentId;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteFolderAsync(Guid folderId)
        {
            var folder = await _dbContext.Folders.FindAsync(folderId);
            if (folder == null)
                return false;

            _dbContext.Folders.Remove(folder);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<Folder> GetOrCreateRootFolderAsync(Guid userId)
        {
            var rootFolder = await _dbContext.Folders
                .Where(f => f.UserId == userId && f.ParentId == null)
                .FirstOrDefaultAsync();

            if (rootFolder == null)
            {
                rootFolder = await CreateFolderAsync(userId, "Root", null);
            }

            return rootFolder;
        }

        /// <summary>
        /// Checks if a folder is a descendant of another folder
        /// </summary>
        /// <param name="ancestorId">Potential ancestor folder ID</param>
        /// <param name="descendantId">Potential descendant folder ID</param>
        /// <returns>True if descendantId is a descendant of ancestorId</returns>
        private async Task<bool> IsDescendantAsync(Guid ancestorId, Guid descendantId)
        {
            var folder = await _dbContext.Folders.FindAsync(descendantId);
            if (folder == null || !folder.ParentId.HasValue)
                return false;

            if (folder.ParentId.Value == ancestorId)
                return true;

            return await IsDescendantAsync(ancestorId, folder.ParentId.Value);
        }
    }
}
