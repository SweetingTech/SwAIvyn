using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Interface for Google Workspace integration services
    /// </summary>
    public interface IGoogleWorkspaceService
    {
        /// <summary>
        /// Check if Google Workspace API is available
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Send an email via Gmail
        /// </summary>
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false);

        /// <summary>
        /// Get recent Gmail messages
        /// </summary>
        Task<IEnumerable<EmailMessage>> GetRecentEmailsAsync(string query = "", int maxResults = 10);

        /// <summary>
        /// Create a calendar event
        /// </summary>
        Task<bool> CreateCalendarEventAsync(string summary, DateTime startTime, DateTime endTime, string description = "", string location = "");

        /// <summary>
        /// Get upcoming calendar events
        /// </summary>
        Task<IEnumerable<CalendarEvent>> GetUpcomingEventsAsync(int maxResults = 10);

        /// <summary>
        /// List files in Google Drive
        /// </summary>
        Task<IEnumerable<DriveFile>> ListDriveFilesAsync(string folderId = null, int maxResults = 10);

        /// <summary>
        /// Upload a file to Google Drive
        /// </summary>
        Task<string> UploadToDriveAsync(string fileName, byte[] fileContent, string folderId = null);
    }

    /// <summary>
    /// Represents an email message
    /// </summary>
    public class EmailMessage
    {
        public string Id { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public DateTime ReceivedDate { get; set; }
        public bool IsUnread { get; set; }
    }

    /// <summary>
    /// Represents a calendar event
    /// </summary>
    public class CalendarEvent
    {
        public string Id { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a Google Drive file
    /// </summary>
    public class DriveFile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public DateTime ModifiedTime { get; set; }
        public long? Size { get; set; }
    }
}
