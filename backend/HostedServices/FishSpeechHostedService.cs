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
    /// Hosted service that automatically starts and manages the Fish Speech TTS API server.
    /// </summary>
    public class FishSpeechHostedService : BackgroundService
    {
        private readonly ILogger<FishSpeechHostedService> _logger;
        private Process? _fishSpeechProcess;
        private readonly string _fishSpeechPath;
        private readonly string _pythonScript;        public FishSpeechHostedService(ILogger<FishSpeechHostedService> logger)
        {
            _logger = logger;
            _logger.LogInformation("FishSpeechHostedService constructor called.");
            
            // Determine the path to the Fish Speech installation (go up one level from backend)
            _fishSpeechPath = Path.Combine(
                Directory.GetParent(Directory.GetCurrentDirectory()).FullName,
                "speech", "TTS", "openaudio-s1-mini"
            );
            
            _pythonScript = Path.Combine(_fishSpeechPath, "fish_speech_api.py");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FishSpeechHostedService is starting.");
            try
            {
                // Check if Fish Speech is available
                if (!IsFishSpeechAvailable())
                {
                    _logger.LogWarning("Fish Speech TTS not available. Model files not found at: {Path}", _fishSpeechPath);
                    return;
                }

                _logger.LogInformation("Starting Fish Speech TTS API server...");
                
                // Start the Fish Speech API server
                await StartFishSpeechAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Fish Speech hosted service");
            }
        }        private bool IsFishSpeechAvailable()
        {
            if (!Directory.Exists(_fishSpeechPath))
            {
                _logger.LogDebug("Fish Speech directory not found: {Path}", _fishSpeechPath);
                return false;
            }

            if (!File.Exists(_pythonScript))
            {
                _logger.LogDebug("Fish Speech API script not found: {Script}", _pythonScript);
                return false;
            }

            // Check for model files in the Fish Speech directory
            var modelPath = Path.Combine(_fishSpeechPath, "model.pth");
            var codecPath = Path.Combine(_fishSpeechPath, "codec.pth");

            var modelExists = File.Exists(modelPath);
            var codecExists = File.Exists(codecPath);
            
            _logger.LogDebug("Checking for Fish Speech model files. Model Path: {ModelPath}, Codec Path: {CodecPath}. Model exists: {ModelExists}, Codec exists: {CodecExists}",
                modelPath, codecPath, modelExists, codecExists);

            if (!modelExists || !codecExists)
            {
                _logger.LogWarning("Fish Speech model files not found. Model exists: {ModelExists}, Codec exists: {CodecExists}",
                    modelExists, codecExists);
                return false;
            }

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

        private async Task StartFishSpeechAsync(CancellationToken cancellationToken)
        {
            try
            {                // Use the working Fish Speech API with virtual environment Python
                var pythonExe = Path.Combine(_fishSpeechPath, "fish_speech_env", "Scripts", "python.exe");
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = "fish_speech_api.py --listen 127.0.0.1:8081",
                    WorkingDirectory = _fishSpeechPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation("Attempting to start Fish Speech process with command: {FileName} {Arguments} in working directory {WorkingDirectory}",
                    startInfo.FileName, startInfo.Arguments, startInfo.WorkingDirectory);

                _fishSpeechProcess = new Process { StartInfo = startInfo };
                
                // Handle output for logging
                _fishSpeechProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogInformation("[Fish Speech] {Output}", e.Data);
                    }
                };

                _fishSpeechProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogWarning("[Fish Speech Error] {Error}", e.Data);
                    }
                };

                if (_fishSpeechProcess.Start())
                {
                    _fishSpeechProcess.BeginOutputReadLine();
                    _fishSpeechProcess.BeginErrorReadLine();
                    
                    _logger.LogInformation("Fish Speech TTS API server started successfully");
                    
                    // Wait for the process to exit or cancellation
                    while (!_fishSpeechProcess.HasExited && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                else
                {
                    _logger.LogError("Failed to start Fish Speech TTS API server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Fish Speech TTS API server process");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Fish Speech TTS API server...");
            
            try
            {
                if (_fishSpeechProcess != null && !_fishSpeechProcess.HasExited)
                {
                    _fishSpeechProcess.Kill(entireProcessTree: true);
                    await _fishSpeechProcess.WaitForExitAsync(cancellationToken);
                    _logger.LogInformation("Fish Speech TTS API server stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Fish Speech TTS API server");
            }
            finally
            {
                _fishSpeechProcess?.Dispose();
                _fishSpeechProcess = null;
            }

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            try
            {
                if (_fishSpeechProcess != null && !_fishSpeechProcess.HasExited)
                {
                    _fishSpeechProcess.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Fish Speech process");
            }
            finally
            {
                _fishSpeechProcess?.Dispose();
            }

            base.Dispose();
        }
    }
}
