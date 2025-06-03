using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SwAIvyn.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SwAIvyn.HostedServices
{
    /// <summary>
    /// Hosted service that starts and manages the FastAPI search service (search.py)
    /// </summary>
    public class SearchServiceHostedService : BackgroundService
    {
        private readonly ISimpleLoggerService _logger;
        private readonly IConfiguration _configuration;
        private Process? _searchProcess;
        private readonly string _searchServicePath;
        private readonly int _searchServicePort = 8001;

        public SearchServiceHostedService(ISimpleLoggerService logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // Get the search service path relative to the application
            var currentDirectory = Directory.GetCurrentDirectory();
            _searchServicePath = Path.Combine(currentDirectory, "search", "search.py");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInfo("🔍 SearchServiceHostedService: Starting FastAPI search service...");

                // Check if search.py exists
                if (!File.Exists(_searchServicePath))
                {
                    _logger.LogError($"🔍 Search service file not found at: {_searchServicePath}");
                    return;
                }

                // Check if Python is available
                if (!IsPythonAvailable())
                {
                    _logger.LogError("🔍 Python is not available. Cannot start search service.");
                    return;
                }

                // Start the search service
                await StartSearchServiceAsync(stoppingToken);

                // Keep the service running until cancellation is requested
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Check if the process is still running
                    if (_searchProcess?.HasExited == true)
                    {
                        _logger.LogWarning("🔍 Search service process has exited. Attempting to restart...");
                        await StartSearchServiceAsync(stoppingToken);
                    }

                    // Wait before checking again
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo("🔍 SearchServiceHostedService: Stopping due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError($"🔍 SearchServiceHostedService: Error in ExecuteAsync: {ex.Message}", ex);
            }
        }

        private async Task StartSearchServiceAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Kill any existing search service processes
                await KillExistingSearchProcessesAsync();

                _logger.LogInfo("🔍 Starting FastAPI search service...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = _searchServicePath,
                    WorkingDirectory = Path.GetDirectoryName(_searchServicePath),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _searchProcess = new Process { StartInfo = startInfo };

                // Log output from the search service
                _searchProcess.OutputDataReceived += (_, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogInfo($"🔍 [SEARCH] {e.Data}");
                };

                _searchProcess.ErrorDataReceived += (_, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogWarning($"🔍 [SEARCH ERROR] {e.Data}");
                };

                _searchProcess.Start();
                _searchProcess.BeginOutputReadLine();
                _searchProcess.BeginErrorReadLine();

                _logger.LogInfo($"🔍 Search service started with PID: {_searchProcess.Id}");

                // Wait a moment for the service to start
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                // Verify the service is responding
                var isHealthy = await VerifySearchServiceHealthAsync();
                if (isHealthy)
                {
                    _logger.LogInfo("✅ Search service is running and healthy");
                }
                else
                {
                    _logger.LogWarning("⚠️ Search service started but health check failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"🔍 Failed to start search service: {ex.Message}", ex);
            }
        }

        private bool IsPythonAvailable()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task KillExistingSearchProcessesAsync()
        {
            try
            {
                var processes = Process.GetProcessesByName("python");
                foreach (var process in processes)
                {
                    try
                    {
                        // Check if this is our search service by looking at command line
                        var commandLine = GetProcessCommandLine(process);
                        if (commandLine?.Contains("search.py") == true)
                        {
                            _logger.LogInfo($"🔍 Killing existing search service process: {process.Id}");
                            process.Kill();
                            await process.WaitForExitAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"🔍 Failed to kill process {process.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"🔍 Error during process cleanup: {ex.Message}");
            }
        }

        private string? GetProcessCommandLine(Process process)
        {
            try
            {
                return process.StartInfo.Arguments;
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> VerifySearchServiceHealthAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                
                var response = await httpClient.GetAsync($"http://localhost:{_searchServicePort}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInfo("🔍 SearchServiceHostedService: Stopping search service...");

            try
            {
                if (_searchProcess != null && !_searchProcess.HasExited)
                {
                    _logger.LogInfo("🔍 Terminating search service process...");
                    _searchProcess.Kill();
                    await _searchProcess.WaitForExitAsync();
                    _searchProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"🔍 Error stopping search service: {ex.Message}");
            }

            await base.StopAsync(cancellationToken);
            _logger.LogInfo("🔍 SearchServiceHostedService: Stopped");
        }
    }
}
