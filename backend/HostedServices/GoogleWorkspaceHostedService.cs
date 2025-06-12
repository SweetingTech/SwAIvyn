using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SwAIvyn.HostedServices
{
    /// <summary>
    /// Hosted service that automatically starts and manages the Google Workspace API server.
    /// </summary>
    public class GoogleWorkspaceHostedService : BackgroundService
    {
        private readonly ILogger<GoogleWorkspaceHostedService> _logger;
        private Process? _googleWorkspaceProcess;
        private readonly string _googleWorkspacePath;
        private readonly string _pythonScript;

        public GoogleWorkspaceHostedService(ILogger<GoogleWorkspaceHostedService> logger)
        {
            _logger = logger;
            _logger.LogInformation("GoogleWorkspaceHostedService constructor called.");
            
            // Determine the path to the Google Workspace service installation
            _googleWorkspacePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "services", "google_workspace"
            );
            
            _pythonScript = Path.Combine(_googleWorkspacePath, "google_workspace_api.py");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GoogleWorkspaceHostedService is starting.");
            try
            {
                // Check if Google Workspace service is available
                if (!IsGoogleWorkspaceAvailable())
                {
                    _logger.LogWarning("Google Workspace API service not available. Python script not found at: {Path}", _pythonScript);
                    return;
                }

                _logger.LogInformation("Starting Google Workspace API server...");
                
                // Start the Google Workspace API server
                await StartGoogleWorkspaceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Google Workspace hosted service");
            }
        }

        private bool IsGoogleWorkspaceAvailable()
        {
            if (!Directory.Exists(_googleWorkspacePath))
            {
                _logger.LogDebug("Google Workspace service directory not found: {Path}", _googleWorkspacePath);
                return false;
            }

            if (!File.Exists(_pythonScript))
            {
                _logger.LogDebug("Google Workspace API script not found: {Script}", _pythonScript);
                return false;
            }

            // Check for requirements file
            var requirementsPath = Path.Combine(_googleWorkspacePath, "requirements.txt");
            var requirementsExists = File.Exists(requirementsPath);

            _logger.LogDebug("Google Workspace service check: Requirements file exists: {RequirementsExists}", requirementsExists);

            return true; // Don't require credentials.json to be present for service to start
        }

        private async Task StartGoogleWorkspaceAsync(CancellationToken cancellationToken)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "google_workspace_api.py --host 127.0.0.1 --port 8082",
                    WorkingDirectory = _googleWorkspacePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation("Attempting to start Google Workspace process with command: {FileName} {Arguments} in working directory {WorkingDirectory}",
                    startInfo.FileName, startInfo.Arguments, startInfo.WorkingDirectory);

                _googleWorkspaceProcess = new Process { StartInfo = startInfo };
                
                // Handle output for logging
                _googleWorkspaceProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogInformation("[Google Workspace] {Output}", e.Data);
                    }
                };

                _googleWorkspaceProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogWarning("[Google Workspace Error] {Error}", e.Data);
                    }
                };

                if (_googleWorkspaceProcess.Start())
                {
                    _googleWorkspaceProcess.BeginOutputReadLine();
                    _googleWorkspaceProcess.BeginErrorReadLine();
                    
                    _logger.LogInformation("Google Workspace API server started successfully");
                    
                    // Wait for the process to exit or cancellation
                    while (!_googleWorkspaceProcess.HasExited && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                else
                {
                    _logger.LogError("Failed to start Google Workspace API server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Google Workspace API server process");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Google Workspace API server...");
            
            try
            {
                if (_googleWorkspaceProcess != null && !_googleWorkspaceProcess.HasExited)
                {
                    _googleWorkspaceProcess.Kill(entireProcessTree: true);
                    await _googleWorkspaceProcess.WaitForExitAsync(cancellationToken);
                    _logger.LogInformation("Google Workspace API server stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Google Workspace API server");
            }
            finally
            {
                _googleWorkspaceProcess?.Dispose();
                _googleWorkspaceProcess = null;
            }

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            try
            {
                if (_googleWorkspaceProcess != null && !_googleWorkspaceProcess.HasExited)
                {
                    _googleWorkspaceProcess.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Google Workspace process");
            }
            finally
            {
                _googleWorkspaceProcess?.Dispose();
            }

            base.Dispose();
        }
    }
}
