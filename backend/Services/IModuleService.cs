using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    public interface IModuleService
    {
        Task<IEnumerable<ModuleDefinition>> GetModulesAsync(Guid? userId);
    }

    public class ModuleDefinition // Corresponds to frontend's Module interface
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public bool Enabled { get; set; }
        public bool Installed { get; set; }
        // Add any other fields that might come from the mcpServers config if needed for display
    }
}
