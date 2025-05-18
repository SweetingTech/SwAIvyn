using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SwAIvyn.Services
{
    public interface ISimpleLoggerService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
        void LogCritical(string message, Exception? exception = null);
        string GetLogFilePath();
    }

    public class SimpleLoggerService : ISimpleLoggerService
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public SimpleLoggerService(IConfiguration configuration)
        {
            // Get log directory from configuration or use default
            var logDirectory = configuration["AppSettings:LogDirectory"] ?? "logs";

            // Ensure the directory exists
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Create log file with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(logDirectory, $"SwAIvyn_{timestamp}.log");

            // Log startup
            LogInfo($"Application started at {DateTime.Now}");
            LogInfo($"Log file created at: {_logFilePath}");
        }

        public void LogInfo(string message)
        {
            WriteToFile("INFO", message);
            Console.WriteLine($"[INFO] {message}");
        }

        public void LogWarning(string message)
        {
            WriteToFile("WARNING", message);
            Console.WriteLine($"[WARNING] {message}");
        }

        public void LogError(string message, Exception? exception = null)
        {
            var logMessage = exception != null
                ? $"{message} - Exception: {exception.Message}\nStackTrace: {exception.StackTrace}"
                : message;

            WriteToFile("ERROR", logMessage);
            Console.WriteLine($"[ERROR] {message}");
            if (exception != null)
            {
                Console.WriteLine($"Exception: {exception.Message}");
                Console.WriteLine($"StackTrace: {exception.StackTrace}");
            }
        }

        public void LogCritical(string message, Exception? exception = null)
        {
            var logMessage = exception != null
                ? $"{message} - Exception: {exception.Message}\nStackTrace: {exception.StackTrace}"
                : message;

            WriteToFile("CRITICAL", logMessage);
            Console.WriteLine($"[CRITICAL] {message}");
            if (exception != null)
            {
                Console.WriteLine($"Exception: {exception.Message}");
                Console.WriteLine($"StackTrace: {exception.StackTrace}");
            }

            // Also create a crash log for critical errors
            if (exception != null)
            {
                try
                {
                    var logDir = Path.GetDirectoryName(_logFilePath) ?? "logs";
                    var crashLogPath = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                    var crashInfo = new StringBuilder();
                    crashInfo.AppendLine($"Crash occurred at: {DateTime.Now}");
                    crashInfo.AppendLine($"Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
                    crashInfo.AppendLine($"Application: SwAIvyn");
                    crashInfo.AppendLine($"Message: {message}");
                    crashInfo.AppendLine($"Exception Type: {exception.GetType().FullName}");
                    crashInfo.AppendLine($"Exception Message: {exception.Message}");

                    // Get the method name and class from the stack trace
                    var stackTrace = new System.Diagnostics.StackTrace(exception, true);
                    var frames = stackTrace.GetFrames();

                    if (frames != null && frames.Length > 0)
                    {
                        crashInfo.AppendLine($"Stack Trace Details:");
                        foreach (var frame in frames)
                        {
                            var method = frame.GetMethod();
                            if (method != null)
                            {
                                var className = method.DeclaringType?.FullName ?? "Unknown";
                                var methodName = method.Name;
                                var fileName = frame.GetFileName() ?? "Unknown";
                                var lineNumber = frame.GetFileLineNumber();

                                crashInfo.AppendLine($"  at {className}.{methodName} in {fileName}:line {lineNumber}");
                            }
                        }
                    }
                    else
                    {
                        // Fallback to the exception's stack trace if we can't get detailed info
                        crashInfo.AppendLine($"Stack Trace: {exception.StackTrace}");
                    }

                    if (exception.InnerException != null)
                    {
                        crashInfo.AppendLine($"Inner Exception Type: {exception.InnerException.GetType().FullName}");
                        crashInfo.AppendLine($"Inner Exception Message: {exception.InnerException.Message}");
                        crashInfo.AppendLine($"Inner Stack Trace: {exception.InnerException.StackTrace}");
                    }

                    File.WriteAllText(crashLogPath, crashInfo.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write crash log: {ex.Message}");
                }
            }
        }

        public string GetLogFilePath()
        {
            return _logFilePath;
        }

        private void WriteToFile(string level, string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

                lock (_lockObject)
                {
                    File.AppendAllText(_logFilePath, logEntry);
                }
            }
            catch (Exception ex)
            {
                // If we can't write to the log file, at least try to log to the console
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
                Console.WriteLine($"Original message: [{level}] {message}");
            }
        }
    }
}
