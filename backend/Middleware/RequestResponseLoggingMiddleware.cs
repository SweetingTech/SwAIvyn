using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SwAIvyn.Services;

namespace SwAIvyn.Middleware
{
    /// <summary>
    /// Logs inbound requests and outbound responses with a correlation ID.
    /// For conversation-related endpoints, includes limited request/response bodies (size-capped).
    /// Adds X-Correlation-Id header to responses.
    /// </summary>
    public class RequestResponseLoggingMiddleware
    {
        private const string CorrelationHeader = "X-Correlation-Id";
        private const int MaxLoggedBodyBytes = 4096; // 4KB cap to avoid huge logs

        private readonly RequestDelegate _next;
        private readonly ISimpleLoggerService _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ISimpleLoggerService logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Create or propagate correlation ID
            var correlationId = context.Request.Headers.ContainsKey(CorrelationHeader)
                ? context.Request.Headers[CorrelationHeader].ToString()
                : Guid.NewGuid().ToString();

            context.Items[CorrelationHeader] = correlationId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationHeader] = correlationId;
                return Task.CompletedTask;
            });

            var path = context.Request.Path.ToString();
            var method = context.Request.Method;
            var shouldLogBody = path.StartsWith("/api/conversation", StringComparison.OrdinalIgnoreCase);

            // Avoid buffering streaming endpoints that could hang the client
            var isSseOrStream =
                context.Request.Headers.ContainsKey("Accept") &&
                (context.Request.Headers["Accept"].ToString().Contains("text/event-stream") ||
                 context.Request.Headers["Accept"].ToString().Contains("application/octet-stream"));

            // Capture request body (if applicable)
            string? requestBodySnippet = null;
            if (shouldLogBody && context.Request.ContentLength > 0 && context.Request.Body.CanSeek)
            {
                context.Request.EnableBuffering();
                var length = (int)Math.Min(context.Request.ContentLength ?? 0, MaxLoggedBodyBytes);
                var buffer = new byte[length];
                _ = await context.Request.Body.ReadAsync(buffer.AsMemory(0, length));
                requestBodySnippet = Encoding.UTF8.GetString(buffer);
                context.Request.Body.Position = 0;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                _logger.LogInfo($"[HTTP IN] {method} {path} cid={correlationId} qs={context.Request.QueryString}");
                if (!string.IsNullOrWhiteSpace(requestBodySnippet))
                {
                    _logger.LogInfo($"[HTTP IN BODY] cid={correlationId} {Truncate(requestBodySnippet)}");
                }

                if (isSseOrStream)
                {
                    // Do not buffer; just log IN and let the response stream
                    await _next(context);
                    sw.Stop();
                    _logger.LogInfo($"[HTTP OUT] {method} {path} -> {context.Response.StatusCode} cid={correlationId} {sw.ElapsedMilliseconds}ms (stream)");
                }
                else
                {
                    // For most endpoints, do not swap the response stream to avoid any chance of buffering-induced hangs.
                    // Only buffer and read the response body for conversation endpoints where we explicitly want body logging.
                    if (!shouldLogBody)
                    {
                        await _next(context);
                        sw.Stop();
                        _logger.LogInfo($"[HTTP OUT] {method} {path} -> {context.Response.StatusCode} cid={correlationId} {sw.ElapsedMilliseconds}ms");
                    }
                    else
                    {
                        // Swap response body to capture output for conversation endpoints
                        var originalBody = context.Response.Body;
                        await using var memStream = new MemoryStream();
                        context.Response.Body = memStream;

                        await _next(context);

                        sw.Stop();

                        // Read response body
                        string? responseBodySnippet = null;
                        memStream.Position = 0;
                        using var reader = new StreamReader(memStream, Encoding.UTF8, leaveOpen: true);
                        responseBodySnippet = await reader.ReadToEndAsync();
                        if (responseBodySnippet.Length > MaxLoggedBodyBytes)
                            responseBodySnippet = responseBodySnippet.Substring(0, MaxLoggedBodyBytes) + "…";
                        memStream.Position = 0;

                        // Copy back to original stream
                        await memStream.CopyToAsync(originalBody);
                        context.Response.Body = originalBody;

                        _logger.LogInfo($"[HTTP OUT] {method} {path} -> {context.Response.StatusCode} cid={correlationId} {sw.ElapsedMilliseconds}ms");
                        if (!string.IsNullOrWhiteSpace(responseBodySnippet))
                        {
                            _logger.LogInfo($"[HTTP OUT BODY] cid={correlationId} {responseBodySnippet}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError($"[HTTP ERR] {method} {path} cid={correlationId} {sw.ElapsedMilliseconds}ms", ex);
                throw;
            }
        }

        private static string Truncate(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Length <= MaxLoggedBodyBytes) return s;
            return s.Substring(0, MaxLoggedBodyBytes) + "…";
        }
    }
}

