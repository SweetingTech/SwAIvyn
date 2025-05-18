using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for managing folders
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class FolderController : ControllerBase
    {
        private readonly IFolderService _folderService;
        private readonly ISimpleLoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the FolderController
        /// </summary>
        /// <param name="folderService">Folder service</param>
        /// <param name="logger">Logger service</param>
        public FolderController(
            IFolderService folderService,
            ISimpleLoggerService logger)
        {
            _folderService = folderService;
            _logger = logger;
        }

        /// <summary>
        /// Gets all folders for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of folders</returns>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetFolders(Guid userId)
        {
            try
            {
                var folders = await _folderService.GetFoldersAsync(userId);
                return Ok(folders);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting folders for user {userId}", ex);
                return StatusCode(500, "An error occurred while retrieving folders");
            }
        }

        /// <summary>
        /// Gets a folder by ID
        /// </summary>
        /// <param name="id">Folder ID</param>
        /// <returns>The folder</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFolder(Guid id)
        {
            try
            {
                var folder = await _folderService.GetFolderAsync(id);
                if (folder == null)
                    return NotFound();

                return Ok(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting folder {id}", ex);
                return StatusCode(500, "An error occurred while retrieving the folder");
            }
        }

        /// <summary>
        /// Creates a new folder
        /// </summary>
        /// <param name="request">Create folder request</param>
        /// <returns>The created folder</returns>
        [HttpPost]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
        {
            try
            {
                var folder = await _folderService.CreateFolderAsync(request.UserId, request.Name, request.ParentId);
                return Ok(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating folder for user {request.UserId}", ex);
                return StatusCode(500, "An error occurred while creating the folder");
            }
        }

        /// <summary>
        /// Updates a folder's name
        /// </summary>
        /// <param name="id">Folder ID</param>
        /// <param name="request">Update folder name request</param>
        /// <returns>Success status</returns>
        [HttpPut("{id}/name")]
        public async Task<IActionResult> UpdateFolderName(Guid id, [FromBody] UpdateFolderNameRequest request)
        {
            try
            {
                var success = await _folderService.UpdateFolderNameAsync(id, request.Name);
                if (!success)
                    return NotFound();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating folder name {id}", ex);
                return StatusCode(500, "An error occurred while updating the folder name");
            }
        }

        /// <summary>
        /// Updates a folder's parent
        /// </summary>
        /// <param name="id">Folder ID</param>
        /// <param name="request">Update folder parent request</param>
        /// <returns>Success status</returns>
        [HttpPut("{id}/parent")]
        public async Task<IActionResult> UpdateFolderParent(Guid id, [FromBody] UpdateFolderParentRequest request)
        {
            try
            {
                var success = await _folderService.UpdateFolderParentAsync(id, request.ParentId);
                if (!success)
                    return NotFound();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating folder parent {id}", ex);
                return StatusCode(500, "An error occurred while updating the folder parent");
            }
        }

        /// <summary>
        /// Deletes a folder
        /// </summary>
        /// <param name="id">Folder ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFolder(Guid id)
        {
            try
            {
                var success = await _folderService.DeleteFolderAsync(id);
                if (!success)
                    return NotFound();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting folder {id}", ex);
                return StatusCode(500, "An error occurred while deleting the folder");
            }
        }

        /// <summary>
        /// Gets or creates the root folder for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>The root folder</returns>
        [HttpGet("root/{userId}")]
        public async Task<IActionResult> GetRootFolder(Guid userId)
        {
            try
            {
                var rootFolder = await _folderService.GetOrCreateRootFolderAsync(userId);
                return Ok(rootFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting root folder for user {userId}", ex);
                return StatusCode(500, "An error occurred while retrieving the root folder");
            }
        }
    }

    /// <summary>
    /// Request to create a folder
    /// </summary>
    public class CreateFolderRequest
    {
        /// <summary>
        /// Gets or sets the user ID
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the folder name
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the parent folder ID
        /// </summary>
        public Guid? ParentId { get; set; }
    }

    /// <summary>
    /// Request to update a folder's name
    /// </summary>
    public class UpdateFolderNameRequest
    {
        /// <summary>
        /// Gets or sets the new folder name
        /// </summary>
        [Required]
        public string Name { get; set; }
    }

    /// <summary>
    /// Request to update a folder's parent
    /// </summary>
    public class UpdateFolderParentRequest
    {
        /// <summary>
        /// Gets or sets the new parent folder ID
        /// </summary>
        public Guid? ParentId { get; set; }
    }
}
