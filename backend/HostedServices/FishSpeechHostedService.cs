using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwAIvyn.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SwAIvyn.HostedServices
{
    /// <summary>
    /// Hosted service that automatically starts and manages the Fish Speech TTS API server.
    /// </summary>
    public class FishSpeechHostedService : BackgroundService
    {
        private readonly ILogger<FishSpeechHostedService> _logger;
        private Process? _fishSpeechProcess;
        private readonly string _fishSpeechPath;
        private readonly string _pythonScript;
        private readonly FishSpeechOptions _fishSpeechOptions;
        private volatile bool _isApiHealthy = false;
        private volatile bool _isStartupComplete = false;
        private static readonly HttpClient _httpClient = new HttpClient();

        public FishSpeechHostedService(ILogger<FishSpeechHostedService> logger, IOptions<FishSpeechOptions> fishSpeechOptions)
        {
            _logger = logger;
            _fishSpeechOptions = fishSpeechOptions.Value;
            _logger.LogInformation("FishSpeechHostedService constructor called.");
            _logger.LogInformation("FishSpeechOptions AutoStart: {AutoStart}", _fishSpeechOptions.AutoStart);
            _logger.LogInformation("FishSpeechOptions BaseUrl: {BaseUrl}", _fishSpeechOptions.BaseUrl);

            // Determine the path to the Fish Speech installation (same level as current directory)
            _fishSpeechPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "speech", "TTS", "openaudio-s1-mini"
            );

            _pythonScript = Path.Combine(_fishSpeechPath, "fish_speech_api.py");
            _logger.LogInformation("FishSpeechHostedService constructor completed. Path: {Path}", _fishSpeechPath);
        }        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("FishSpeechHostedService attempting to start.");
                _logger.LogInformation("Current working directory: {WorkingDirectory}", Directory.GetCurrentDirectory());
                _logger.LogInformation("Fish Speech path: {FishSpeechPath}", _fishSpeechPath);
                _logger.LogInformation("Python script path: {PythonScript}", _pythonScript);
                _logger.LogInformation("AutoStart setting: {AutoStart}", _fishSpeechOptions.AutoStart);

                if (!_fishSpeechOptions.AutoStart)
                {
                    _logger.LogInformation("FishSpeechHostedService AutoStart is false. Service will not start the Python process.");
                    return;
                }

                if (!ArePrerequisitesMet())
                {
                    _logger.LogWarning("Fish Speech TTS prerequisites not met. Check model files and Python script paths.");
                    return;
                }

                _logger.LogInformation("Attempting to start Fish Speech Python process.");
                bool processStarted = await StartFishSpeechAsync(stoppingToken);

                if (processStarted && !stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Fish Speech Python process started. Waiting for models to load...");

                    // Wait for Fish Speech to load models (similar to Neo4j startup delay)
                    _logger.LogInformation("Waiting 90 seconds for Fish Speech models to fully load...");
                    await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);
                    _logger.LogInformation("Fish Speech model loading delay completed");

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Performing health checks...");
                        await PerformHealthChecksAsync(stoppingToken);

                        // Mark startup as complete after successful health checks
                        if (_isApiHealthy)
                        {
                            _isStartupComplete = true;
                            _logger.LogInformation("Fish Speech startup completed successfully");
                        }
                    }
                }
                else if (!processStarted)
                {
                    _logger.LogError("Fish Speech Python process failed to start. Service will not perform health checks.");
                }
                else
                {
                    _logger.LogInformation("Fish Speech service startup was cancelled.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in FishSpeechHostedService.ExecuteAsync");
                throw; // Re-throw to ensure the service fails properly
            }
        }

        public bool IsApiHealthy() => _isApiHealthy;

        public bool IsStartupComplete() => _isStartupComplete;        private bool ArePrerequisitesMet()
        {
            _logger.LogInformation("Checking Fish Speech prerequisites...");
            _logger.LogInformation("Fish Speech directory path: {Path}", _fishSpeechPath);
            
            if (!Directory.Exists(_fishSpeechPath))
            {
                _logger.LogError("Fish Speech directory not found: {Path}", _fishSpeechPath);
                return false;
            }
            _logger.LogInformation("Fish Speech directory exists: {Path}", _fishSpeechPath);

            _logger.LogInformation("Python script path: {Script}", _pythonScript);
            if (!File.Exists(_pythonScript))
            {
                _logger.LogError("Fish Speech API script not found: {Script}", _pythonScript);
                return false;
            }
            _logger.LogInformation("Python script exists: {Script}", _pythonScript);

            // Check for model files in the Fish Speech directory
            var modelPath = Path.Combine(_fishSpeechPath, "model.pth");
            var codecPath = Path.Combine(_fishSpeechPath, "codec.pth");

            var modelExists = File.Exists(modelPath);
            var codecExists = File.Exists(codecPath);
            
            _logger.LogInformation("Checking for Fish Speech model files. Model Path: {ModelPath}, Codec Path: {CodecPath}. Model exists: {ModelExists}, Codec exists: {CodecExists}",
                modelPath, codecPath, modelExists, codecExists);

            if (!modelExists || !codecExists)
            {
                _logger.LogError("Fish Speech model files not found. Model exists: {ModelExists}, Codec exists: {CodecExists}",
                    modelExists, codecExists);
                return false;
            }

            _logger.LogInformation("All Fish Speech prerequisites met!");
            return true;
        }

        private async Task<string> GetOptimalFishSpeechArgs()
        {
            try
            {
                var autoConfigScript = Path.Combine(_fishSpeechPath, "auto_config.py");
                
                if (!File.Exists(autoConfigScript))
                {
                    _logger.LogWarning("Auto-config script not found, using default CPU settings");
                    return "--device cpu --listen 127.0.0.1:8081 --llama-checkpoint-path \"../\" --decoder-checkpoint-path \"../codec.pth\"";
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "auto_config.py",
                    WorkingDirectory = _fishSpeechPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("Auto-config script failed with exit code {ExitCode}. Error: {Error}", 
                        process.ExitCode, error);
                    return "--device cpu --listen 127.0.0.1:8081 --llama-checkpoint-path \"../\" --decoder-checkpoint-path \"../codec.pth\"";
                }

                // Parse the output to get the Fish Speech arguments
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Fish Speech Arguments:"))
                    {
                        var args = line.Substring("Fish Speech Arguments:".Length).Trim();
                        _logger.LogInformation("Auto-detected Fish Speech configuration: {Args}", args);
                        return args;
                    }
                }

                _logger.LogWarning("Could not parse auto-config output, using default CPU settings");
                return "--device cpu --listen 127.0.0.1:8081 --llama-checkpoint-path \"../\" --decoder-checkpoint-path \"../codec.pth\"";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running auto-configuration, falling back to CPU");
                return "--device cpu --listen 127.0.0.1:8081 --llama-checkpoint-path \"../\" --decoder-checkpoint-path \"../codec.pth\"";
            }
        }

        private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_fishSpeechOptions.BaseUrl) || !Uri.TryCreate(_fishSpeechOptions.BaseUrl + "/health", UriKind.Absolute, out _))
            {
                _logger.LogError("Invalid BaseUrl for Fish Speech API health check: {BaseUrl}", _fishSpeechOptions.BaseUrl);
                _isApiHealthy = false;
                return;
            }

            var healthCheckUrl = _fishSpeechOptions.BaseUrl + "/health";
            _logger.LogInformation("Starting health checks for Fish Speech API at {Url}", healthCheckUrl);

            const int maxAttempts = 10;
            const int delayBetweenAttemptsSeconds = 5;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Health checks cancelled.");
                    return;
                }

                try
                {
                    _logger.LogDebug("Attempting health check {Attempt}/{MaxAttempts} for {Url}", attempt, maxAttempts, healthCheckUrl);
                    HttpResponseMessage response = await _httpClient.GetAsync(healthCheckUrl, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogInformation("Health check successful for {Url}. Response: {Response}", healthCheckUrl, content);
                        _isApiHealthy = true;
                        return; // Health check passed
                    }
                    else
                    {
                        _logger.LogWarning("Health check attempt {Attempt}/{MaxAttempts} failed for {Url}. Status Code: {StatusCode}", attempt, maxAttempts, healthCheckUrl, response.StatusCode);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Health check attempt {Attempt}/{MaxAttempts} for {Url} failed with HttpRequestException.", attempt, maxAttempts, healthCheckUrl);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                     _logger.LogWarning(ex, "Health check attempt {Attempt}/{MaxAttempts} for {Url} timed out.", attempt, maxAttempts, healthCheckUrl);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Health check cancelled during attempt {Attempt}/{MaxAttempts}.", attempt, maxAttempts);
                    return; // Cancellation requested
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred during health check attempt {Attempt}/{MaxAttempts} for {Url}.", attempt, maxAttempts, healthCheckUrl);
                }

                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delayBetweenAttemptsSeconds), cancellationToken);
                }
            }

            _logger.LogError("Fish Speech API failed all health checks after {MaxAttempts} attempts. Service marked as unhealthy.", maxAttempts);
            _isApiHealthy = false;
        }

        private async Task<bool> StartFishSpeechAsync(CancellationToken cancellationToken)
        {
            // Simple arguments for our custom Fish Speech API script
            var arguments = "fish_speech_api.py --listen localhost:8081";

            // Note: Our custom API script handles device selection and model loading internally
            
            var pythonEnvPath = Path.Combine(_fishSpeechPath, "fish_speech_env");
            var pythonExe = Path.Combine(pythonEnvPath, Environment.OSVersion.Platform == PlatformID.Win32NT ? "Scripts" : "bin", Environment.OSVersion.Platform == PlatformID.Win32NT ? "python.exe" : "python");
            
            if (!File.Exists(pythonExe))
            {
                _logger.LogError("Python executable not found at {PythonExePath}. Searched in {PythonEnvPath}", pythonExe, pythonEnvPath);
                // Try system python if not found in venv, though venv is preferred.
                pythonExe = Environment.OSVersion.Platform == PlatformID.Win32NT ? "python.exe" : "python"; 
                _logger.LogWarning("Falling back to system 'python'. This may cause issues if dependencies are not globally available.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = arguments,
                WorkingDirectory = _fishSpeechPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogInformation("Attempting to start Fish Speech process with: FileName='{FileName}', Arguments='{Arguments}', WorkingDirectory='{WorkingDirectory}'",
                startInfo.FileName, startInfo.Arguments, startInfo.WorkingDirectory);

            _fishSpeechProcess = new Process { StartInfo = startInfo };
            
            _fishSpeechProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogInformation("[FishSpeechAPI] {Output}", e.Data);
                }
            };

            _fishSpeechProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Log common Python errors that might indicate setup issues
                    if (e.Data.Contains("ModuleNotFoundError") || e.Data.Contains("ImportError"))
                        _logger.LogError("[FishSpeechAPI ERROR - Potential Dependency Issue] {Error}", e.Data);
                    else if (e.Data.Contains("OSError: [Errno 98] Address already in use"))
                        _logger.LogError("[FishSpeechAPI ERROR - Port Conflict] {Error}", e.Data);
                    else
                        _logger.LogWarning("[FishSpeechAPI Error] {Error}", e.Data);
                }
            };
            
            try
            {
                if (!_fishSpeechProcess.Start())
                {
                    _logger.LogError("Failed to start Fish Speech process. _fishSpeechProcess.Start() returned false.");
                    return false;
                }

                _fishSpeechProcess.BeginOutputReadLine();
                _fishSpeechProcess.BeginErrorReadLine();
                
                _logger.LogInformation("Fish Speech process started successfully (PID: {ProcessId}). Waiting for it to become healthy or exit.", _fishSpeechProcess.Id);

                // Don't wait indefinitely here, ExecuteAsync will monitor for cancellation and health
                // However, we need a task that completes when the process exits to monitor its lifetime
                var processExitCompletionSource = new TaskCompletionSource<bool>();
                _fishSpeechProcess.EnableRaisingEvents = true;
                _fishSpeechProcess.Exited += (sender, args) => {
                    if (!cancellationToken.IsCancellationRequested) // Only log as error if not part of normal shutdown
                    {
                        _logger.LogError("Fish Speech process exited prematurely with code {ExitCode}.", _fishSpeechProcess.ExitCode);
                    }
                    _isApiHealthy = false; // Mark as unhealthy if process dies
                    processExitCompletionSource.TrySetResult(true);
                };
                
                // Ensure we stop monitoring if cancellation is requested
                cancellationToken.Register(() => {
                    processExitCompletionSource.TrySetCanceled();
                });

                // This task is not awaited directly here, but allows ExecuteAsync to continue to health checks
                // while also having a way to react to process exit.
                // The actual waiting for the process to complete its lifecycle or for cancellation
                // will be implicitly handled by the BackgroundService's main loop and StopAsync.
                return true; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception caught while trying to start Fish Speech process. FileName='{FileName}', Arguments='{Arguments}'", startInfo.FileName, startInfo.Arguments);
                return false;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Fish Speech TTS API server...");
            _isApiHealthy = false; // Mark as unhealthy on stop
            _isStartupComplete = false; // Mark startup as incomplete on stop
            
            if (_fishSpeechProcess != null && !_fishSpeechProcess.HasExited)
            {
                _logger.LogInformation("Killing Fish Speech process (PID: {ProcessId}).", _fishSpeechProcess.Id);
                try
                {
                    _fishSpeechProcess.Kill(entireProcessTree: true);
                    await _fishSpeechProcess.WaitForExitAsync(cancellationToken);
                    _logger.LogInformation("Fish Speech process killed and exited.");
                }
                catch (InvalidOperationException)
                {
                    _logger.LogWarning("Fish Speech process may have already exited.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping Fish Speech process.");
                }
            }
            else
            {
                _logger.LogInformation("Fish Speech process was not running or already exited.");
            }
            
            _fishSpeechProcess?.Dispose();
            _fishSpeechProcess = null;

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _logger.LogInformation("Disposing FishSpeechHostedService.");
            _isApiHealthy = false; // Mark as unhealthy on dispose
            _isStartupComplete = false; // Mark startup as incomplete on dispose
            if (_fishSpeechProcess != null && !_fishSpeechProcess.HasExited)
            {
                _logger.LogWarning("Disposing service, but Fish Speech process (PID: {ProcessId}) might still be running. Attempting to kill.", _fishSpeechProcess.Id);
                try
                {
                    _fishSpeechProcess.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error killing Fish Speech process during Dispose.");
                }
            }
            _fishSpeechProcess?.Dispose();
            _fishSpeechProcess = null;
            base.Dispose();
        }
    }
}

