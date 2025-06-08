using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting; // Required for IHostEnvironment

namespace SwAIvyn.Services
{
    public class ModuleService : IModuleService
    {
        private readonly ISimpleLoggerService _logger;
        private readonly string _mcpConfigPath;
        private McpConfig _mcpConfig;

        // Define structure for mcpServers.json
        public class McpConfig
        {
            public Dictionary<string, McpServerDetail> McpServers { get; set; } = new Dictionary<string, McpServerDetail>();
        }

        public class McpServerDetail
        {
            public List<string> AutoApprove { get; set; } = new List<string>();
            public bool Disabled { get; set; } // Maps to !Enabled
            public int Timeout { get; set; }
            public string Command { get; set; }
            public List<string> Args { get; set; } = new List<string>();
            public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>();
            public string TransportType { get; set; }
            // Add other potential fields like 'description', 'version', 'category' if they exist in the real config
            // For now, we'll derive some of these.
        }

        public ModuleService(ISimpleLoggerService logger, IHostEnvironment hostEnvironment)
        {
            _logger = logger;
            _mcpConfigPath = Path.Combine(hostEnvironment.ContentRootPath, "mcpServers.json");
            LoadMcpConfig().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private async Task LoadMcpConfig()
        {
            try
            {
                if (File.Exists(_mcpConfigPath))
                {
                    _logger.LogInfo($"Loading MCP configuration from: {_mcpConfigPath}");
                    var json = await File.ReadAllTextAsync(_mcpConfigPath);
                    _mcpConfig = JsonSerializer.Deserialize<McpConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    _logger.LogInfo($"Successfully loaded {_mcpConfig?.McpServers?.Count ?? 0} MCP servers.");
                }
                else
                {
                    _logger.LogWarning($"MCP configuration file not found at: {_mcpConfigPath}. Creating a default/empty config.");
                    _mcpConfig = GetDefaultMcpConfig();
                    // Optionally, write the default config to the file path
                    // var defaultJson = JsonSerializer.Serialize(_mcpConfig, new JsonSerializerOptions { WriteIndented = true });
                    // await File.WriteAllTextAsync(_mcpConfigPath, defaultJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading MCP configuration: {ex.Message}", ex);
                _mcpConfig = GetDefaultMcpConfig(); // Fallback to default
            }
        }

        private McpConfig GetDefaultMcpConfig()
        {
            // Provides the example structure if mcpServers.json is missing or fails to load
            return new McpConfig
            {
                McpServers = new Dictionary<string, McpServerDetail>
                {
                    {
                        "github.com/Flux159/mcp-server-kubernetes", new McpServerDetail
                        {
                            Disabled = true, // Example: This one is installed but disabled
                            // Other fields from your example...
                        }
                    },
                    {
                        "github.com/github/github-mcp-server", new McpServerDetail
                        {
                            Disabled = false, // Example: Installed and enabled
                            // Other fields...
                        }
                    }
                    // Add more default examples if necessary
                }
            };
        }

        public async Task<IEnumerable<ModuleDefinition>> GetModulesAsync(Guid? userId)
        {
            // For now, userId is not used, but it's in the interface for future-proofing.
            // If config hasn't loaded somehow, try again (or ensure constructor handles it)
            if (_mcpConfig == null || _mcpConfig.McpServers == null)
            {
                _logger.LogWarning("McpConfig or McpServers is null in GetModulesAsync. Attempting reload or using default.");
                await LoadMcpConfig(); // Ensure it's loaded
                    if (_mcpConfig == null || _mcpConfig.McpServers == null) // Still null after attempt
                    {
                    _logger.LogError("Failed to load McpConfig even after explicit call in GetModulesAsync. Returning empty list.");
                    return new List<ModuleDefinition>();
                    }
            }

            var modules = _mcpConfig.McpServers.Select(kvp =>
            {
                var serverId = kvp.Key;
                var serverDetails = kvp.Value;
                var nameParts = serverId.Split('/');
                var name = nameParts.Length > 1 ? nameParts.Last() : serverId; // Simple name extraction

                return new ModuleDefinition
                {
                    Id = serverId,
                    Name = name,
                    Version = "1.0.0", // Placeholder, ideally from config or git
                    Description = $"Module for {name}", // Placeholder
                    Category = GetCategoryFromName(name), // Simple categorization
                    Enabled = !serverDetails.Disabled,
                    Installed = true // All modules in mcpServers.json are considered "installed"
                };
            }).ToList();

            _logger.LogInfo($"Returning {modules.Count} modules for user {userId?.ToString() ?? "Global"}.");
            return modules;
        }

        private string GetCategoryFromName(string name)
        {
            if (name.ToLower().Contains("kubernetes")) return "Cloud & Infrastructure";
            if (name.ToLower().Contains("github")) return "Developer Tools";
            if (name.ToLower().Contains("filesystem")) return "System Utilities";
            if (name.ToLower().Contains("sequentialthinking")) return "AI & Machine Learning";
            if (name.ToLower().Contains("git")) return "Developer Tools";
            if (name.ToLower().Contains("blender")) return "Graphics & Design";
            if (name.ToLower().Contains("unity")) return "Game Development";
            if (name.ToLower().Contains("sqlite") || name.ToLower().Contains("neo4j")) return "Databases";
            if (name.ToLower().Contains("memory")) return "AI & Machine Learning";
            if (name.ToLower().Contains("notion")) return "Productivity";
            if (name.ToLower().Contains("browser-tools")) return "Web Development";
            return "Extensions"; // Default category
        }
    }
}
