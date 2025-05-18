# SwAIvyn Logging System Guide

## Overview

SwAIvyn includes a comprehensive logging system that captures application events, errors, and performance metrics. This guide explains how to use and interpret the logs to diagnose issues.

## Log Types

### 1. Application Logs

**File Format:** `SwAIvyn_YYYYMMDD_HHMMSS.log`

**Contains:**
- Application startup and shutdown events
- User interactions
- Service operations
- Warning and error messages
- Performance metrics

**Log Levels:**
- `INFO`: Normal operations, informational messages
- `WARNING`: Potential issues that don't affect functionality
- `ERROR`: Problems that affect specific features
- `CRITICAL`: Severe issues that may crash the application

### 2. Crash Logs

**File Format:** `crash_YYYYMMDD_HHMMSS.txt`

**Contains:**
- Timestamp of the crash
- Process ID
- Application name
- Exception type and message
- Detailed stack trace
- Memory usage at time of crash
- Inner exception details (if applicable)

## Viewing Logs

### Using the Log Viewer

1. Run `show-logs.cmd` from the application directory:
   ```
   .\show-logs.cmd
   ```

2. This will display:
   - A summary of available logs
   - The most recent application log (last 20 lines)
   - The most recent crash log (if any)
   - Helpful commands for further log analysis

### PowerShell Commands for Log Analysis

View all log files:
```powershell
Get-ChildItem -Path 'C:\path\to\SwAIvyn\logs' | Sort-Object LastWriteTime -Descending
```

View a specific log file:
```powershell
Get-Content -Path 'C:\path\to\SwAIvyn\logs\SwAIvyn_20250517_022800.log'
```

View the last 100 lines of a log file:
```powershell
Get-Content -Path 'C:\path\to\SwAIvyn\logs\SwAIvyn_20250517_022800.log' | Select-Object -Last 100
```

Search for specific errors:
```powershell
Get-Content -Path 'C:\path\to\SwAIvyn\logs\SwAIvyn_20250517_022800.log' | Select-String -Pattern "ERROR"
```

## Understanding Log Entries

### Application Log Format

```
[YYYY-MM-DD HH:MM:SS.fff] [LEVEL] Message
```

Example:
```
[2025-05-17 02:28:00.000] [INFO] Application started at 5/17/2025 2:28:00 AM
[2025-05-17 02:28:01.000] [ERROR] Failed to connect to database - Exception: Connection refused
```

### Crash Log Format

```
Crash occurred at: [Timestamp]
Process ID: [ID]
Application: SwAIvyn
Message: [Error Message]
Exception Type: [Exception Class]
Exception Message: [Detailed Message]
Stack Trace Details:
  at [Namespace].[Class].[Method] in [File]:line [Number]
  at [Namespace].[Class].[Method] in [File]:line [Number]
  ...
```

Example:
```
Crash occurred at: 5/17/2025 2:28:30 AM
Process ID: 12345
Application: SwAIvyn
Message: Unhandled exception: Application is terminating
Exception Type: System.NullReferenceException
Exception Message: Object reference not set to an instance of an object.
Stack Trace Details:
  at SwAIvyn.Services.ApplicationMonitorService.LogApplicationStatus in ApplicationMonitorService.cs:line 85
  at SwAIvyn.Services.ApplicationMonitorService.ExecuteAsync in ApplicationMonitorService.cs:line 42
```

## Key Components of the Logging System

### 1. SimpleLoggerService

**Purpose:** Primary logging service that writes to both console and files.

**Key Methods:**
- `LogInfo(string message)`: Logs informational messages
- `LogWarning(string message)`: Logs warning messages
- `LogError(string message, Exception? exception = null)`: Logs error messages with optional exception details
- `LogCritical(string message, Exception? exception = null)`: Logs critical errors and creates crash logs

**Location:** `backend/Services/SimpleLoggerService.cs`

### 2. ApplicationMonitorService

**Purpose:** Background service that monitors application health and logs performance metrics.

**Key Methods:**
- `ExecuteAsync(CancellationToken stoppingToken)`: Main monitoring loop
- `LogApplicationStatus(bool isFinal = false)`: Logs current application status

**Location:** `backend/Services/ApplicationMonitorService.cs`

### 3. GlobalExceptionHandlerMiddleware

**Purpose:** Middleware that catches unhandled exceptions in the request pipeline.

**Key Methods:**
- `InvokeAsync(HttpContext context)`: Processes HTTP requests and catches exceptions
- `HandleExceptionAsync(HttpContext context, Exception exception)`: Logs exceptions and returns error responses

**Location:** `backend/Middleware/GlobalExceptionHandlerMiddleware.cs`

## Troubleshooting Common Issues

### 1. Application Crashes

**Symptoms:**
- Application closes unexpectedly
- Operations fail with no visible error

**Diagnostic Steps:**
1. Check for crash logs in the `logs` directory
2. Look for the most recent crash log file (`crash_YYYYMMDD_HHMMSS.txt`)
3. Examine the exception type and message
4. Review the stack trace to identify the source of the error
5. Check the method and line number where the exception occurred

**Example:**
If you see a `NullReferenceException` in `ApplicationMonitorService.LogApplicationStatus`, it indicates that the service is trying to access a null object when logging application status.

### 2. Feature-Specific Errors

**Symptoms:**
- Specific feature doesn't work
- Error message displayed to user

**Diagnostic Steps:**
1. Check application logs for ERROR entries
2. Look for logs related to the specific feature
3. Identify the service or controller involved
4. Check for exception details in the log

**Example:**
If chat messages aren't being sent, look for errors related to `ChatService`, `ChatController`, or `LlmConnectorService`.

### 3. Performance Issues

**Symptoms:**
- Application runs slowly
- Operations take longer than expected

**Diagnostic Steps:**
1. Check application logs for performance metrics
2. Look for entries from `ApplicationMonitorService`
3. Monitor memory usage trends
4. Check CPU time and thread count

**Example:**
If memory usage is continuously increasing, it may indicate a memory leak in one of the services.

## Log Maintenance

### Log Rotation

The logging system automatically manages log files:
- Each application session creates a new log file
- Crash logs are created as needed with unique timestamps
- No manual cleanup is typically required

### Disk Space Considerations

If disk space becomes a concern:
1. Older log files can be safely archived or deleted
2. Focus on keeping recent logs for troubleshooting
3. Consider increasing log verbosity only when actively debugging issues

## Best Practices for Developers

### 1. Adding New Log Entries

When adding new code, include appropriate logging:

```csharp
// Example of good logging practice
try
{
    _logger.LogInfo($"Starting import operation for user {userId}");
    var result = await _importService.ImportData(data);
    _logger.LogInfo($"Import completed successfully. Items processed: {result.ItemCount}");
    return result;
}
catch (Exception ex)
{
    _logger.LogError($"Import operation failed for user {userId}", ex);
    throw;
}
```

### 2. Log Level Guidelines

- **INFO**: Normal operations, startup/shutdown, user actions
- **WARNING**: Potential issues, deprecated feature usage, slow operations
- **ERROR**: Exceptions that affect functionality but don't crash the app
- **CRITICAL**: Severe errors that prevent core functionality or crash the app

### 3. Including Context in Logs

Always include relevant context in log messages:
- User IDs (but not sensitive data)
- Operation being performed
- Relevant entity IDs
- Quantitative information (counts, sizes, durations)

## Conclusion

The SwAIvyn logging system provides comprehensive visibility into application behavior. By understanding how to interpret these logs, you can quickly identify and resolve issues that may arise during operation.
