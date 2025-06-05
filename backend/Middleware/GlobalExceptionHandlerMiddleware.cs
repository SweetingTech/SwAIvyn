using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SwAIvyn.Services;

namespace SwAIvyn.Middleware
{
    /// <summary>
    /// Middleware that catches all unhandled exceptions in the request pipeline
    /// and logs them with detailed information
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ISimpleLoggerService _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ISimpleLoggerService logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Log the exception with detailed information
            _logger.LogError($"Unhandled exception in request {context.Request.Path}", exception);
            
            // Create a detailed error message for the log
            var requestInfo = new
            {
                Path = context.Request.Path,
                Method = context.Request.Method,
                QueryString = context.Request.QueryString.ToString(),
                RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].ToString()
            };
            
            _logger.LogError($"Request details: {JsonSerializer.Serialize(requestInfo)}");
            
            // Create a user-friendly error response
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            
            var response = new
            {
                error = "An unexpected error occurred",
                errorId = Guid.NewGuid().ToString(), // This can be used to correlate with the logs
                timestamp = DateTime.UtcNow
            };
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
