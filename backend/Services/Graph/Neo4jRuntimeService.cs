using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SwAIvyn.Services;

namespace SwAIvyn.Services.Graph
{
    /// <summary>
    /// Service for managing the Neo4j runtime
    /// </summary>
    public class Neo4jRuntimeService : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ISettingsProvider _settingsProvider;
        private readonly ISimpleLoggerService _logger;
        private readonly string _appDataPath;
        private readonly string _neo4jHomePath;
        private readonly string _neo4jBinPath;
        private readonly string _neo4jConfPath;
        private int _boltPort;
        private int _httpPort;
        private Process? _neo4jProcess;
        private bool _isStarting = false;
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _shutdownTokenSource = new();

        public Neo4jRuntimeService(
            IConfiguration configuration,
            ISettingsProvider settingsProvider,
            ISimpleLoggerService logger)
        {
            _configuration = configuration;
            _settingsProvider = settingsProvider;
            _logger = logger;
            _httpClient = new HttpClient();

            // Read ports from config (fallback to defaults)
            _boltPort = _settingsProvider.GetNeo4jBoltPort();
            _httpPort = _settingsProvider.GetNeo4jHttpPort();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SwAIvyn");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "SwAIvyn");
            }
            else
            {
                _appDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            }

            _neo4jHomePath = Path.Combine(_appDataPath, "neo4j");
            _neo4jBinPath  = Path.Combine(_neo4jHomePath, "bin");
            _neo4jConfPath = Path.Combine(_neo4jHomePath, "conf", "neo4j.conf");

            _logger.LogInfo($"Neo4j home path: {_neo4jHomePath}");
            _logger.LogInfo($"Configured Bolt port: {_boltPort}, HTTP port: {_httpPort}");
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Neo4j runtime...");

                bool isEmbedded = _configuration.GetValue<bool>("AppSettings:Neo4jEmbedded", false);
                _logger.LogInfo($"Neo4jEmbedded setting is: {isEmbedded}");

                if (!isEmbedded)
                {
                    _logger.LogInfo("Neo4jEmbedded is false. Skipping embedded Neo4j runtime initialization, extraction, and startup.");
                    // Ensure we still have the correct ports for external connection if Neo4jService relies on them from this service.
                    _boltPort = _settingsProvider.GetNeo4jBoltPort();
                    _httpPort = _settingsProvider.GetNeo4jHttpPort();
                    _logger.LogInfo($"Ports for external Neo4j (from settingsProvider) - Bolt: {_boltPort}, HTTP: {_httpPort}");
                    return;
                }

                // Proceed with embedded setup only if Neo4jEmbedded is true
                _logger.LogInfo("Neo4jEmbedded is true. Proceeding with embedded Neo4j setup.");

                // Get the latest port settings from configuration for embedded instance
                _boltPort = _settingsProvider.GetNeo4jBoltPort();
                _httpPort = _settingsProvider.GetNeo4jHttpPort();
                _logger.LogInfo($"Using Neo4j ports from settings for embedded instance - Bolt: {_boltPort}, HTTP: {_httpPort}");

                if (!Directory.Exists(_neo4jHomePath))
                {
                    await ExtractNeo4jAsync();
                    if (Directory.Exists(_neo4jHomePath) && File.Exists(_neo4jConfPath))
                        await UpdateNeo4jConfigurationAsync();
                }
                else if (File.Exists(_neo4jConfPath))
                {
                    // Even if it exists, ensure configuration reflects current appsettings.json ports
                    await UpdateNeo4jConfigurationAsync();
                }

                if (Directory.Exists(_neo4jHomePath) && Directory.Exists(_neo4jBinPath))
                {
                    await StartNeo4jAsync();
                }
                else
                {
                    _logger.LogWarning("Neo4j runtime not found for embedded mode. Skipping startup.");
                }

                _logger.LogInfo("Neo4j runtime initialization completed for embedded mode.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Neo4j runtime", ex);
                throw;
            }
        }

        private async Task ExtractNeo4jAsync()
        {
            try
            {
                _logger.LogInfo("Extracting Neo4j...");
                Directory.CreateDirectory(_neo4jHomePath);

                string neo4jZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "neo4j-community-2025.04.0-windows.zip");
                if (!File.Exists(neo4jZipPath))
                {
                    // fallback logic omitted for brevity
                }

                using var archive = ZipFile.OpenRead(neo4jZipPath);
                string rootDir = archive.Entries[0].FullName.Split('/')[0];
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.Equals(rootDir + "/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string relPath = entry.FullName[(rootDir.Length + 1)..];
                    string destPath = Path.Combine(_neo4jHomePath, relPath);

                    if (entry.FullName.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                        Directory.CreateDirectory(destPath);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }

                _logger.LogInfo("Neo4j extracted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to extract Neo4j", ex);
                throw;
            }
        }

        private async Task UpdateNeo4jConfigurationAsync()
        {
            try
            {
                _boltPort = _settingsProvider.GetNeo4jBoltPort();
                _httpPort = _settingsProvider.GetNeo4jHttpPort();
                _logger.LogInfo($"Using Neo4j ports from settings - Bolt: {_boltPort}, HTTP: {_httpPort}");

                if (!File.Exists(_neo4jConfPath))
                {
                    _logger.LogWarning($"Config not found at {_neo4jConfPath}, skipping.");
                    return;
                }

                var lines = File.ReadAllLines(_neo4jConfPath);
                var sb = new StringBuilder();
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("dbms.default_listen_address=") ||
                        line.TrimStart().StartsWith("dbms.connector.bolt.listen_address=") ||
                        line.TrimStart().StartsWith("dbms.connector.http.listen_address=") ||
                        line.TrimStart().StartsWith("dbms.windows_service_name=") ||
                        line.TrimStart().StartsWith("server.default_listen_address=") ||
                        line.TrimStart().StartsWith("server.bolt.listen_address=") ||
                        line.TrimStart().StartsWith("server.http.listen_address=") ||
                        line.TrimStart().StartsWith("server.windows_service_name=") ||
                        line.TrimStart().StartsWith("dbms.security.auth_enabled=") ||
                        line.TrimStart().StartsWith("dbms.security.auth_provider.plugin=") ||
                        line.TrimStart().StartsWith("server.config.strict_validation.enabled=") ||
                        line.TrimStart().StartsWith("dbms.jvm.additional="))
                        continue;
                    sb.AppendLine(line);
                }

                // Disable strict validation to allow deprecated settings
                sb.AppendLine("server.config.strict_validation.enabled=false");

                // Network configuration - using only new settings for Neo4j 2025.04.0
                sb.AppendLine($"server.default_listen_address=127.0.0.1");
                sb.AppendLine($"server.bolt.listen_address=127.0.0.1:{_boltPort}");
                sb.AppendLine($"server.http.listen_address=127.0.0.1:{_httpPort}");
                sb.AppendLine($"server.windows_service_name=SwAIvynNeo");

                // Authentication settings
                sb.AppendLine("dbms.security.auth_enabled=true");

                // Authentication configuration - get from configuration
                string neo4jUser = _configuration["AppSettings:Neo4jUser"] ?? "neo4j";
                string neo4jPassword = _configuration["AppSettings:Neo4jPassword"] ?? "password";

                // Create auth file if it doesn't exist
                string authFilePath = Path.Combine(_neo4jHomePath, "conf", "auth");
                Directory.CreateDirectory(Path.GetDirectoryName(authFilePath)!);

                if (!File.Exists(authFilePath))
                {
                    _logger.LogInfo("Creating Neo4j auth file with credentials from settings");
                    File.WriteAllText(authFilePath, $"{neo4jUser}:{neo4jPassword}");
                }

                // Enable authentication - using newer Neo4j 2025.04.0 settings
                sb.AppendLine("server.config.strict_validation.enabled=false");
                sb.AppendLine("dbms.security.auth_enabled=true");

                File.WriteAllText(_neo4jConfPath, sb.ToString());
                _logger.LogInfo("Neo4j configuration updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update Neo4j configuration", ex);
            }
        }

        private async Task StartNeo4jAsync()
        {
            try
            {
                _logger.LogInfo("Starting Neo4j...");
                _isStarting = true;

                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                string neo4jScript = isWindows ? "neo4j.bat" : "neo4j";
                string neo4jExe    = Path.Combine(_neo4jBinPath, neo4jScript);

                if (!File.Exists(neo4jExe))
                {
                    _logger.LogWarning($"Executable not found at {neo4jExe}, skipping startup.");
                    return;
                }

                try
                {
                    var probe = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName              = "java",
                            Arguments             = "--version",
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,
                            UseShellExecute       = false,
                            CreateNoWindow        = true
                        }
                    };
                    probe.Start();
                    string javaOut = await probe.StandardOutput.ReadToEndAsync();
                    probe.WaitForExit();
                    _logger.LogInfo($"Detected Java: {javaOut.Split('\n')[0]}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to detect Java version: {ex.Message}");
                }

                // Create a direct Java command to bypass PowerShell/batch script issues
                string javaPath = "java"; // Use system Java
                string neo4jLibPath = Path.Combine(_neo4jHomePath, "lib");
                string classpath = $"{neo4jLibPath}\\*";

                var startInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = $"-cp \"{classpath}\" -Dbasedir=\"{_neo4jHomePath}\" org.neo4j.server.startup.Neo4jCommand console",
                    WorkingDirectory = _neo4jHomePath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                // Log the command we're using
                _logger.LogInfo($"Starting Neo4j with direct Java command: {javaPath} -cp \"{classpath}\" -Dbasedir=\"{_neo4jHomePath}\" org.neo4j.server.startup.Neo4jCommand console");

                var jdkPath = _configuration["AppSettings:Neo4jJavaHome"];
                if (!string.IsNullOrEmpty(jdkPath))
                    startInfo.EnvironmentVariables["JAVA_HOME"] = jdkPath;

                _neo4jProcess = new Process { StartInfo = startInfo };

                if (isWindows)
                {
                    _neo4jProcess.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogInfo($"Neo4j: {e.Data}"); };
                    _neo4jProcess.ErrorDataReceived  += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogError($"Neo4j: {e.Data}"); };
                }

                _neo4jProcess.Start();
                if (isWindows)
                {
                    _neo4jProcess.BeginOutputReadLine();
                    _neo4jProcess.BeginErrorReadLine();
                }

                bool requireNeo4j = _configuration.GetValue("AppSettings:RequireNeo4j", false);
                var isAvail       = await WaitForNeo4jAsync(requireNeo4j);

                if (isAvail)
                {
                    // Neo4j is running
                    _logger.LogInfo("Neo4j started successfully");
                }
                else if (requireNeo4j)
                {
                    _logger.LogCritical("Neo4j failed to start and is required");
                    throw new Exception("Neo4j failed to start and is required");
                }
                else
                {
                    _logger.LogWarning("Neo4j failed to start but is not required");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start Neo4j", ex);
                if (_configuration.GetValue("AppSettings:RequireNeo4j", false))
                    throw;
            }
            finally
            {
                _isStarting = false;
            }
        }

        private async Task<bool> WaitForNeo4jAsync(bool _)
        {
            _logger.LogInfo("Waiting for Neo4j to start...");
            if (_neo4jProcess?.HasExited == true)
            {
                var code = _neo4jProcess.ExitCode;
                _logger.LogError($"Neo4j exited with code {code}");
                var err = await _neo4jProcess.StandardError.ReadToEndAsync();
                if (err.Contains("UnsupportedClassVersionError"))
                    _logger.LogError("Neo4j requires Java 17 or later. Please set JAVA_HOME to JDK17+");
                return false;
            }

            for (int i = 0; i < 30; i++)
            {
                try
                {
                    var resp = await _httpClient.GetAsync($"http://localhost:{_httpPort}/");
                    if (resp.IsSuccessStatusCode)
                    {
                        _logger.LogInfo("Neo4j is available");
                        return true;
                    }
                }
                catch { /* ignore */ }
                await Task.Delay(1000);
                if (i % 5 == 0) _logger.LogInfo($"Still waiting... {i}s");
            }

            _logger.LogWarning("Neo4j not available after 30s");
            return false;
        }

        public async Task<bool> IsAvailableAsync()
        {
            if (_isStarting) return false;
            try { return (await _httpClient.GetAsync($"http://localhost:{_httpPort}/")).IsSuccessStatusCode; }
            catch { return false; }
        }

        public async Task<string> GetStatusAsync()
        {
            if (_isStarting) return "starting";
            return await IsAvailableAsync() ? "online" : "offline";
        }

        public void Dispose()
        {
            bool isEmbedded = _configuration.GetValue<bool>("AppSettings:Neo4jEmbedded", false);

            if (isEmbedded)
            {
                _logger.LogInfo("Disposing Neo4jRuntimeService for embedded instance...");
                try
                {
                    if (!_shutdownTokenSource.IsCancellationRequested)
                    {
                        _shutdownTokenSource.Cancel();
                    }
                }
                catch (ObjectDisposedException) { /* Already disposed */ }


                if (_neo4jProcess != null && !_neo4jProcess.HasExited)
                {
                    _logger.LogInfo("Stopping embedded Neo4j process...");
                    try
                    {
                        _neo4jProcess.Kill(true); // Force kill
                        _neo4jProcess.WaitForExit(5000); // Wait for 5 seconds
                        if (!_neo4jProcess.HasExited)
                        {
                             _logger.LogWarning("Neo4j process did not exit after kill signal and wait.");
                        }
                        _neo4jProcess.Dispose();
                        _logger.LogInfo("Embedded Neo4j process stopped and disposed.");
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning($"Could not kill Neo4j process, it might have already exited: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error stopping embedded Neo4j process", ex);
                    }
                }
                else
                {
                     _logger.LogInfo("Embedded Neo4j process was not running or already exited.");
                }
            }
            else
            {
                _logger.LogInfo("Disposing Neo4jRuntimeService (no embedded instance to stop).");
            }
            
            try
            {
                 _httpClient?.Dispose();
            }
            catch(Exception ex)
            {
                _logger.LogError("Error disposing HttpClient in Neo4jRuntimeService", ex);
            }
           
            try
            {
                 _shutdownTokenSource?.Dispose();
            }
            catch(Exception ex)
            {
                _logger.LogError("Error disposing CancellationTokenSource in Neo4jRuntimeService", ex);
            }

            GC.SuppressFinalize(this);
        }
    }
}
