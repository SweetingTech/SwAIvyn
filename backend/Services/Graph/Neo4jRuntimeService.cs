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
        private readonly ISimpleLoggerService _logger;
        private readonly string _appDataPath;
        private readonly string _neo4jHomePath;
        private readonly string _neo4jBinPath;
        private readonly string _neo4jConfPath;
        private Process? _neo4jProcess;
        private bool _isStarting = false;
        private bool _isRunning = false;
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the Neo4jRuntimeService
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger service</param>
        public Neo4jRuntimeService(
            IConfiguration configuration,
            ISimpleLoggerService logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();

            // Determine the AppData path based on the platform
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
            _neo4jBinPath = Path.Combine(_neo4jHomePath, "bin");
            _neo4jConfPath = Path.Combine(_neo4jHomePath, "conf", "neo4j.conf");

            _logger.LogInfo($"Neo4j home path: {_neo4jHomePath}");
        }

        /// <summary>
        /// Initializes the Neo4j runtime
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInfo("Initializing Neo4j runtime...");

                // Check if Neo4j is already extracted
                if (!Directory.Exists(_neo4jHomePath))
                {
                    await ExtractNeo4jAsync();

                    // Update Neo4j configuration after extraction
                    if (Directory.Exists(_neo4jHomePath) && File.Exists(_neo4jConfPath))
                    {
                        UpdateNeo4jConfiguration();
                    }
                }
                else if (File.Exists(_neo4jConfPath))
                {
                    // Update Neo4j configuration if it exists
                    UpdateNeo4jConfiguration();
                }

                // Start Neo4j if it was extracted successfully
                if (Directory.Exists(_neo4jHomePath) && Directory.Exists(_neo4jBinPath))
                {
                    await StartNeo4jAsync();
                }
                else
                {
                    _logger.LogWarning("Neo4j runtime not found. Skipping Neo4j startup.");
                }

                _logger.LogInfo("Neo4j runtime initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Neo4j runtime", ex);
                throw;
            }
        }

        /// <summary>
        /// Extracts Neo4j from the bundled ZIP file
        /// </summary>
        private async Task ExtractNeo4jAsync()
        {
            try
            {
                _logger.LogInfo("Extracting Neo4j...");

                // Create the Neo4j directory
                Directory.CreateDirectory(_neo4jHomePath);

                // Log the current directory and base directory
                _logger.LogInfo($"Current directory: {Directory.GetCurrentDirectory()}");
                _logger.LogInfo($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");

                // Get the path to the Neo4j ZIP file
                string neo4jZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "neo4j", "neo4j-community-2025.04.0-windows.zip");
                _logger.LogInfo($"Looking for Neo4j ZIP file at: {neo4jZipPath}");

                // Check if the ZIP file exists
                if (!File.Exists(neo4jZipPath))
                {
                    _logger.LogInfo($"Neo4j ZIP file not found at {neo4jZipPath}, trying fallback locations...");

                    // Try the generic filename as fallback
                    neo4jZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "neo4j", "neo4j.zip");
                    _logger.LogInfo($"Looking for Neo4j ZIP file at: {neo4jZipPath}");

                    if (!File.Exists(neo4jZipPath))
                    {
                        // Try to find any Neo4j ZIP file in the directory
                        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "neo4j");
                        _logger.LogInfo($"Looking for Neo4j ZIP files in directory: {directory}");

                        if (Directory.Exists(directory))
                        {
                            var zipFiles = Directory.GetFiles(directory, "neo4j*.zip");
                            _logger.LogInfo($"Found {zipFiles.Length} Neo4j ZIP files in directory");

                            if (zipFiles.Length > 0)
                            {
                                neo4jZipPath = zipFiles[0];
                                _logger.LogInfo($"Found Neo4j ZIP file: {neo4jZipPath}");
                            }
                            else
                            {
                                // Try looking in the project root directory
                                directory = Path.Combine(Directory.GetCurrentDirectory(), "..", "assets", "neo4j");
                                _logger.LogInfo($"Looking for Neo4j ZIP files in project directory: {directory}");

                                if (Directory.Exists(directory))
                                {
                                    zipFiles = Directory.GetFiles(directory, "neo4j*.zip");
                                    _logger.LogInfo($"Found {zipFiles.Length} Neo4j ZIP files in project directory");

                                    if (zipFiles.Length > 0)
                                    {
                                        neo4jZipPath = zipFiles[0];
                                        _logger.LogInfo($"Found Neo4j ZIP file: {neo4jZipPath}");
                                    }
                                    else
                                    {
                                        throw new FileNotFoundException($"No Neo4j ZIP file found in {directory}");
                                    }
                                }
                                else
                                {
                                    throw new DirectoryNotFoundException($"Neo4j assets directory not found at {directory}");
                                }
                            }
                        }
                        else
                        {
                            // Try looking in the project root directory
                            directory = Path.Combine(Directory.GetCurrentDirectory(), "..", "assets", "neo4j");
                            _logger.LogInfo($"Looking for Neo4j ZIP files in project directory: {directory}");

                            if (Directory.Exists(directory))
                            {
                                var zipFiles = Directory.GetFiles(directory, "neo4j*.zip");
                                _logger.LogInfo($"Found {zipFiles.Length} Neo4j ZIP files in project directory");

                                if (zipFiles.Length > 0)
                                {
                                    neo4jZipPath = zipFiles[0];
                                    _logger.LogInfo($"Found Neo4j ZIP file: {neo4jZipPath}");
                                }
                                else
                                {
                                    throw new FileNotFoundException($"No Neo4j ZIP file found in {directory}");
                                }
                            }
                            else
                            {
                                throw new DirectoryNotFoundException($"Neo4j assets directory not found at {directory}");
                            }
                        }
                    }
                }

                // Extract the ZIP file
                using (ZipArchive archive = ZipFile.OpenRead(neo4jZipPath))
                {
                    // Get the root directory name in the ZIP file (e.g., "neo4j-community-5.x.x")
                    string rootDirName = archive.Entries[0].FullName.Split('/')[0];

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // Skip the root directory
                        if (entry.FullName.Equals($"{rootDirName}/", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Remove the root directory from the entry path
                        string relativePath = entry.FullName.Substring(rootDirName.Length + 1);
                        string destinationPath = Path.Combine(_neo4jHomePath, relativePath);

                        // Create the directory if it doesn't exist
                        if (entry.FullName.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            // Create the directory for the file if it doesn't exist
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                            // Extract the file
                            entry.ExtractToFile(destinationPath, true);
                        }
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

        /// <summary>
        /// Updates the Neo4j configuration
        /// </summary>
        private void UpdateNeo4jConfiguration()
        {
            try
            {
                _logger.LogInfo("Updating Neo4j configuration...");

                // Check if the configuration file exists
                if (!File.Exists(_neo4jConfPath))
                {
                    _logger.LogWarning($"Neo4j configuration file not found at {_neo4jConfPath}. Skipping configuration update.");
                    return;
                }

                // Read the configuration file
                string[] lines = File.ReadAllLines(_neo4jConfPath);
                StringBuilder newConfig = new StringBuilder();

                // Process each line
                foreach (string line in lines)
                {
                    // Skip lines that we're going to replace
                    if (line.TrimStart().StartsWith("dbms.default_listen_address=") ||
                        line.TrimStart().StartsWith("dbms.connector.bolt.listen_address=") ||
                        line.TrimStart().StartsWith("dbms.connector.http.listen_address=") ||
                        line.TrimStart().StartsWith("dbms.windows_service_name="))
                    {
                        continue;
                    }

                    // Add the line to the new configuration
                    newConfig.AppendLine(line);
                }

                // Add our custom configuration
                newConfig.AppendLine("dbms.default_listen_address=127.0.0.1");
                newConfig.AppendLine("dbms.connector.bolt.listen_address=:7687");
                newConfig.AppendLine("dbms.connector.http.listen_address=:7474");
                newConfig.AppendLine("dbms.windows_service_name=SwAIvynNeo");

                // Write the new configuration
                File.WriteAllText(_neo4jConfPath, newConfig.ToString());

                _logger.LogInfo("Neo4j configuration updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update Neo4j configuration", ex);
                // Don't throw, just log the error and continue
            }
        }

        /// <summary>
        /// Starts the Neo4j process
        /// </summary>
        private async Task StartNeo4jAsync()
        {
            try
            {
                _logger.LogInfo("Starting Neo4j...");
                _isStarting = true;

                // Get the path to the Neo4j executable
                string neo4jExePath = Path.Combine(_neo4jBinPath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "neo4j.bat" : "neo4j");

                // Check if the executable exists
                if (!File.Exists(neo4jExePath))
                {
                    _logger.LogWarning($"Neo4j executable not found at {neo4jExePath}. Skipping Neo4j startup.");
                    return;
                }

                // Start the Neo4j process
                _neo4jProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = neo4jExePath,
                        Arguments = "console",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                _neo4jProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogInfo($"Neo4j: {e.Data}");
                    }
                };

                _neo4jProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogError($"Neo4j: {e.Data}");
                    }
                };

                _neo4jProcess.Start();
                _neo4jProcess.BeginOutputReadLine();
                _neo4jProcess.BeginErrorReadLine();

                // Wait for Neo4j to start
                bool requireNeo4j = _configuration.GetValue<bool>("AppSettings:RequireNeo4j", false);
                bool isAvailable = await WaitForNeo4jAsync(requireNeo4j);

                if (isAvailable)
                {
                    _isRunning = true;
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

                // Don't throw if Neo4j is not required
                bool requireNeo4j = _configuration.GetValue<bool>("AppSettings:RequireNeo4j", false);
                if (requireNeo4j)
                {
                    throw;
                }
            }
            finally
            {
                _isStarting = false;
            }
        }

        /// <summary>
        /// Waits for Neo4j to start
        /// </summary>
        /// <param name="requireNeo4j">Whether Neo4j is required</param>
        /// <returns>True if Neo4j is available</returns>
        private async Task<bool> WaitForNeo4jAsync(bool requireNeo4j)
        {
            _logger.LogInfo("Waiting for Neo4j to start...");

            // Check if Neo4j process has exited with an error
            if (_neo4jProcess != null && _neo4jProcess.HasExited)
            {
                int exitCode = _neo4jProcess.ExitCode;
                _logger.LogError($"Neo4j process exited with code {exitCode}");

                // Check if the error is related to Java version
                if (_neo4jProcess.StandardError.ReadToEnd().Contains("UnsupportedClassVersionError"))
                {
                    _logger.LogError("Neo4j requires a newer version of Java (Java 17 or later). The installed Java version is too old.");
                    return false;
                }
            }

            // Wait for Neo4j to start (10 seconds max)
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    // Check if Neo4j is available
                    var response = await _httpClient.GetAsync("http://localhost:7474/");
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInfo("Neo4j is available");
                        return true;
                    }
                }
                catch
                {
                    // Ignore exceptions
                }

                // Wait for 1 second
                await Task.Delay(1000);
            }

            _logger.LogWarning("Neo4j is not available after waiting 10 seconds");
            return false;
        }

        /// <summary>
        /// Checks if Neo4j is available
        /// </summary>
        /// <returns>True if Neo4j is available</returns>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // If Neo4j is starting, return false
                if (_isStarting)
                {
                    return false;
                }

                // Check if Neo4j is available
                var response = await _httpClient.GetAsync("http://localhost:7474/");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the status of Neo4j
        /// </summary>
        /// <returns>The status of Neo4j (online, starting, offline)</returns>
        public async Task<string> GetStatusAsync()
        {
            if (_isStarting)
            {
                return "starting";
            }

            bool isAvailable = await IsAvailableAsync();
            return isAvailable ? "online" : "offline";
        }

        /// <summary>
        /// Disposes the Neo4j runtime service
        /// </summary>
        public void Dispose()
        {
            _shutdownTokenSource.Cancel();

            // Stop the Neo4j process
            if (_neo4jProcess != null && !_neo4jProcess.HasExited)
            {
                try
                {
                    _logger.LogInfo("Stopping Neo4j...");
                    _neo4jProcess.Kill(true);
                    _neo4jProcess.Dispose();
                    _logger.LogInfo("Neo4j stopped successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to stop Neo4j", ex);
                }
            }

            _httpClient.Dispose();
            _shutdownTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
