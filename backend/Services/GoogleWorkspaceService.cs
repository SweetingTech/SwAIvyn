using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Google Workspace service implementation that communicates with the Python API server
    /// </summary>
    public class GoogleWorkspaceService : IGoogleWorkspaceService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleWorkspaceService> _logger;
        private readonly ISettingsService _settingsService;
        private readonly string _baseUrl = "http://127.0.0.1:8082";

        public GoogleWorkspaceService(HttpClient httpClient, ILogger<GoogleWorkspaceService> logger, ISettingsService settingsService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settingsService = settingsService;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <inheritdoc />
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Google Workspace API not available");
                return false;
            }
        }

        /// <summary>
        /// Upload credentials for a user
        /// </summary>
        public async Task<bool> UploadCredentialsAsync(string userId, string credentialsJson)
        {
            try
            {
                // Store credentials in settings
                await _settingsService.SetSettingAsync(Guid.Parse(userId), "GoogleWorkspaceCredentials", credentialsJson);
                
                // Upload to Python service
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("user_id", userId),
                    new KeyValuePair<string, string>("credentials", credentialsJson)
                });

                var response = await _httpClient.PostAsync($"{_baseUrl}/auth/credentials", formData);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Google Workspace credentials uploaded successfully for user {UserId}", userId);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to upload Google Workspace credentials: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading Google Workspace credentials for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Check authentication status for a user
        /// </summary>
        public async Task<bool> IsAuthenticatedAsync(string userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/auth/status/{userId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var status = JsonSerializer.Deserialize<AuthStatus>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return status?.Authenticated == true && status?.Valid == true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking authentication status for user {UserId}", userId);
                return false;
            }
        }

        private async Task<string> GetUserIdAsync(Guid? userId)
        {
            // Convert Guid to string, or use "default" for null
            return userId?.ToString() ?? "default";
        }

        /// <inheritdoc />
        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            try
            {
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("to", to),
                    new KeyValuePair<string, string>("subject", subject),
                    new KeyValuePair<string, string>("body", body),
                    new KeyValuePair<string, string>("html", isHtml.ToString().ToLower())
                });

                var response = await _httpClient.PostAsync($"{_baseUrl}/gmail/send", formData);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email sent successfully to {To}", to);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send email: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {To}", to);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<EmailMessage>> GetRecentEmailsAsync(string query = "", int maxResults = 10)
        {
            try
            {
                var url = $"{_baseUrl}/gmail/messages?query={Uri.EscapeDataString(query)}&max_results={maxResults}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get emails: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<EmailMessage>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<GmailMessagesResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return apiResponse?.Messages?.Select(ConvertToEmailMessage) ?? Enumerable.Empty<EmailMessage>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent emails");
                return Enumerable.Empty<EmailMessage>();
            }
        }

        /// <inheritdoc />
        public async Task<bool> CreateCalendarEventAsync(string summary, DateTime startTime, DateTime endTime, string description = "", string location = "")
        {
            try
            {
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("summary", summary),
                    new KeyValuePair<string, string>("start_time", startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
                    new KeyValuePair<string, string>("end_time", endTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
                    new KeyValuePair<string, string>("description", description),
                    new KeyValuePair<string, string>("location", location)
                });

                var response = await _httpClient.PostAsync($"{_baseUrl}/calendar/events", formData);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Calendar event created successfully: {Summary}", summary);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create calendar event: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating calendar event: {Summary}", summary);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CalendarEvent>> GetUpcomingEventsAsync(int maxResults = 10)
        {
            try
            {
                var timeMin = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var url = $"{_baseUrl}/calendar/events?time_min={Uri.EscapeDataString(timeMin)}&max_results={maxResults}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get calendar events: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<CalendarEvent>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<CalendarEventsResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return apiResponse?.Events?.Select(ConvertToCalendarEvent) ?? Enumerable.Empty<CalendarEvent>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting upcoming events");
                return Enumerable.Empty<CalendarEvent>();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DriveFile>> ListDriveFilesAsync(string folderId = null, int maxResults = 10)
        {
            try
            {
                var url = $"{_baseUrl}/drive/files?max_results={maxResults}";
                if (!string.IsNullOrEmpty(folderId))
                {
                    url += $"&folder_id={Uri.EscapeDataString(folderId)}";
                }

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to list Drive files: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<DriveFile>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<DriveFilesResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return apiResponse?.Files?.Select(ConvertToDriveFile) ?? Enumerable.Empty<DriveFile>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing Drive files");
                return Enumerable.Empty<DriveFile>();
            }
        }

        /// <inheritdoc />
        public async Task<string> UploadToDriveAsync(string fileName, byte[] fileContent, string folderId = null)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(fileContent), "file", fileName);
                
                if (!string.IsNullOrEmpty(folderId))
                {
                    content.Add(new StringContent(folderId), "folder_id");
                }

                var response = await _httpClient.PostAsync($"{_baseUrl}/drive/upload", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var uploadResponse = JsonSerializer.Deserialize<DriveUploadResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    _logger.LogInformation("File uploaded to Drive successfully: {FileName}", fileName);
                    return uploadResponse?.FileId ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to upload file to Drive: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to Drive: {FileName}", fileName);
                return string.Empty;
            }
        }

        #region Helper Methods

        private static EmailMessage ConvertToEmailMessage(JsonElement message)
        {
            // This is a simplified conversion - in reality you'd need to parse the Gmail API response structure
            return new EmailMessage
            {
                Id = message.GetProperty("id").GetString() ?? string.Empty,
                Subject = ExtractHeader(message, "Subject"),
                From = ExtractHeader(message, "From"),
                Snippet = message.GetProperty("snippet").GetString() ?? string.Empty,
                ReceivedDate = DateTime.UtcNow, // Would need to parse the actual date
                IsUnread = true // Would need to check the actual labels
            };
        }

        private static string ExtractHeader(JsonElement message, string headerName)
        {
            // Simplified header extraction - would need proper Gmail API response parsing
            return string.Empty;
        }

        private static CalendarEvent ConvertToCalendarEvent(JsonElement eventItem)
        {            return new CalendarEvent
            {
                Id = eventItem.GetProperty("id").GetString() ?? string.Empty,
                Summary = eventItem.GetProperty("summary").GetString() ?? string.Empty,
                Description = eventItem.TryGetProperty("description", out JsonElement descElement) ? descElement.GetString() ?? string.Empty : string.Empty,
                Location = eventItem.TryGetProperty("location", out JsonElement locElement) ? locElement.GetString() ?? string.Empty : string.Empty,
                StartTime = ParseDateTime(eventItem.GetProperty("start")),
                EndTime = ParseDateTime(eventItem.GetProperty("end")),
                Status = eventItem.TryGetProperty("status", out JsonElement statusElement) ? statusElement.GetString() ?? string.Empty : string.Empty
            };
        }

        private static DateTime ParseDateTime(JsonElement dateTimeElement)
        {
            if (dateTimeElement.TryGetProperty("dateTime", out var dateTime))
            {
                if (DateTime.TryParse(dateTime.GetString(), out var result))
                {
                    return result;
                }
            }
            return DateTime.MinValue;
        }

        private static DriveFile ConvertToDriveFile(JsonElement file)
        {            return new DriveFile
            {
                Id = file.GetProperty("id").GetString() ?? string.Empty,
                Name = file.GetProperty("name").GetString() ?? string.Empty,
                MimeType = file.GetProperty("mimeType").GetString() ?? string.Empty,
                ModifiedTime = file.TryGetProperty("modifiedTime", out JsonElement modTimeElement) && DateTime.TryParse(modTimeElement.GetString(), out DateTime parsedTime) ? parsedTime : DateTime.MinValue,
                Size = file.TryGetProperty("size", out JsonElement sizeElement) && long.TryParse(sizeElement.GetString(), out long sizeValue) ? sizeValue : null
            };
        }

        #endregion

        #region API Response Models

        private class GmailMessagesResponse
        {
            public JsonElement[]? Messages { get; set; }
        }

        private class CalendarEventsResponse
        {
            public JsonElement[]? Events { get; set; }
        }

        private class DriveFilesResponse
        {
            public JsonElement[]? Files { get; set; }
        }        private class DriveUploadResponse
        {
            public string? FileId { get; set; }
        }

        private class AuthStatus
        {
            public bool Authenticated { get; set; }
            public bool Valid { get; set; }
        }

        #endregion
    }
}
