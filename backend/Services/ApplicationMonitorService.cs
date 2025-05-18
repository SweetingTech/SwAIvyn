using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Service that monitors the application and logs important events
    /// </summary>
    public class ApplicationMonitorService : BackgroundService
    {
        private readonly ISimpleLoggerService _logger;
        private readonly Process _currentProcess;
        private DateTime _startTime;
        private long _initialMemoryUsage;

        public ApplicationMonitorService(ISimpleLoggerService logger)
        {
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
            _startTime = DateTime.Now;
            _initialMemoryUsage = _currentProcess.WorkingSet64;
            
            // Log initial state
            _logger.LogInfo($"ApplicationMonitorService initialized");
            _logger.LogInfo($"Process ID: {_currentProcess.Id}");
            _logger.LogInfo($"Process Name: {_currentProcess.ProcessName}");
            _logger.LogInfo($"Initial Memory Usage: {FormatBytes(_initialMemoryUsage)}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInfo("ApplicationMonitorService started");
            
            try
            {
                // Monitor the application at regular intervals
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Log memory usage and uptime every minute
                    LogApplicationStatus();
                    
                    // Wait for the next interval
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown, don't treat as error
                _logger.LogInfo("ApplicationMonitorService stopping due to application shutdown");
            }
            catch (Exception ex)
            {
                // Log any unexpected errors
                _logger.LogError("ApplicationMonitorService encountered an error", ex);
            }
            finally
            {
                // Log final status on shutdown
                LogApplicationStatus(true);
                _logger.LogInfo("ApplicationMonitorService stopped");
            }
        }

        private void LogApplicationStatus(bool isFinal = false)
        {
            try
            {
                // Refresh process info
                _currentProcess.Refresh();
                
                // Calculate uptime
                var uptime = DateTime.Now - _startTime;
                
                // Get current memory usage
                var currentMemoryUsage = _currentProcess.WorkingSet64;
                var memoryDelta = currentMemoryUsage - _initialMemoryUsage;
                
                // Get CPU usage (this is approximate)
                var cpuUsage = _currentProcess.TotalProcessorTime;
                
                // Log the status
                var prefix = isFinal ? "Final" : "Current";
                _logger.LogInfo($"{prefix} Application Status:");
                _logger.LogInfo($"  Uptime: {FormatUptime(uptime)}");
                _logger.LogInfo($"  Memory Usage: {FormatBytes(currentMemoryUsage)} ({(memoryDelta >= 0 ? "+" : "")}{FormatBytes(memoryDelta)} since start)");
                _logger.LogInfo($"  CPU Time: {cpuUsage.TotalSeconds:F2} seconds");
                _logger.LogInfo($"  Threads: {_currentProcess.Threads.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to log application status", ex);
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:F2} {suffixes[counter]}";
        }

        private string FormatUptime(TimeSpan timeSpan)
        {
            return timeSpan.TotalDays >= 1 
                ? $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s" 
                : $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        }
    }
}
