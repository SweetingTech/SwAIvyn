using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using System;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for Google Workspace integration
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GoogleWorkspaceController : ControllerBase
    {
        private readonly IGoogleWorkspaceService _googleWorkspaceService;
        private readonly ILogger<GoogleWorkspaceController> _logger;

        public GoogleWorkspaceController(IGoogleWorkspaceService googleWorkspaceService, ILogger<GoogleWorkspaceController> logger)
        {
            _googleWorkspaceService = googleWorkspaceService;
            _logger = logger;
        }

        /// <summary>
        /// Check if Google Workspace service is available
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> GetHealth()
        {
            var isAvailable = await _googleWorkspaceService.IsAvailableAsync();
            return Ok(new { available = isAvailable });
        }

        /// <summary>
        /// Send an email via Gmail
        /// </summary>
        [HttpPost("gmail/send")]
        public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
        {
            try
            {
                var success = await _googleWorkspaceService.SendEmailAsync(
                    request.To, 
                    request.Subject, 
                    request.Body, 
                    request.IsHtml
                );

                if (success)
                {
                    return Ok(new { success = true, message = "Email sent successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to send email" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get recent Gmail messages
        /// </summary>
        [HttpGet("gmail/messages")]
        public async Task<IActionResult> GetMessages([FromQuery] string query = "", [FromQuery] int maxResults = 10)
        {
            try
            {
                var messages = await _googleWorkspaceService.GetRecentEmailsAsync(query, maxResults);
                return Ok(new { messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Gmail messages");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Create a calendar event
        /// </summary>
        [HttpPost("calendar/events")]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
        {
            try
            {
                var success = await _googleWorkspaceService.CreateCalendarEventAsync(
                    request.Summary,
                    request.StartTime,
                    request.EndTime,
                    request.Description,
                    request.Location
                );

                if (success)
                {
                    return Ok(new { success = true, message = "Event created successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to create event" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating calendar event");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get upcoming calendar events
        /// </summary>
        [HttpGet("calendar/events")]
        public async Task<IActionResult> GetEvents([FromQuery] int maxResults = 10)
        {
            try
            {
                var events = await _googleWorkspaceService.GetUpcomingEventsAsync(maxResults);
                return Ok(new { events });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting calendar events");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// List Google Drive files
        /// </summary>
        [HttpGet("drive/files")]
        public async Task<IActionResult> ListFiles([FromQuery] string folderId = null, [FromQuery] int maxResults = 10)
        {
            try
            {
                var files = await _googleWorkspaceService.ListDriveFilesAsync(folderId, maxResults);
                return Ok(new { files });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing Drive files");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Upload a file to Google Drive
        /// </summary>
        [HttpPost("drive/upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string folderId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "No file provided" });
                }

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var fileContent = memoryStream.ToArray();

                var fileId = await _googleWorkspaceService.UploadToDriveAsync(file.FileName, fileContent, folderId);

                if (!string.IsNullOrEmpty(fileId))
                {
                    return Ok(new { success = true, fileId, message = "File uploaded successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to upload file" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to Drive");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }

    #region Request Models

    /// <summary>
    /// Request model for sending email
    /// </summary>
    public class SendEmailRequest
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = false;
    }

    /// <summary>
    /// Request model for creating calendar event
    /// </summary>
    public class CreateEventRequest
    {
        public string Summary { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    #endregion
}
