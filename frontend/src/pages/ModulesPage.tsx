import { motion } from 'framer-motion';
import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Download, Settings, RefreshCw, X, Search, Plus, Package } from 'lucide-react';
import InlineSpinner from '../components/ui/InlineSpinner';
// Removed static import: import modulesData from '../../modules/SwAIvyn_modules_settings.json';

interface Module {
  id: string;
  name: string;
  version: string;
  description: string;
  category: string;
  enabled: boolean;
  installed: boolean;
}

const ModulesPage = () => {
  const [modules, setModules] = useState<Module[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    loadModules();
  }, []);

  const getCategoryFromName = (name: string): string => {
    const lowerName = name.toLowerCase();
    if (lowerName.includes('voice') || lowerName.includes('audio') || lowerName.includes('tts') || lowerName.includes('speech')) {
      return 'Voice & Audio';
    }
    if (lowerName.includes('model') || lowerName.includes('ai') || lowerName.includes('llm') || lowerName.includes('claude') || lowerName.includes('openai') || lowerName.includes('anthropic')) {
      return 'AI Models';
    }
    if (lowerName.includes('git') || lowerName.includes('kubernetes') || lowerName.includes('docker') || lowerName.includes('filesystem')) {
      return 'DevOps & Tools';
    }
    if (lowerName.includes('db') || lowerName.includes('database') || lowerName.includes('sqlite') || lowerName.includes('neo4j') || lowerName.includes('memory')) {
      return 'Data & Memory';
    }
    if (lowerName.includes('browser') || lowerName.includes('web')) {
      return 'Web & Browser';
    }
    if (lowerName.includes('blender') || lowerName.includes('unity') || lowerName.includes('3d')) {
      return '3D & Graphics';
    }
    if (lowerName.includes('notion') || lowerName.includes('github') ) {
      return 'Integrations';
    }
    return 'Extensions'; // Default category
  };

  const loadModules = async () => {
    setLoading(true);
    try {
      // Fetch the module settings JSON file
      const response = await fetch('/modules/SwAIvyn_modules_settings.json');
      if (!response.ok) {
        throw new Error(`Failed to fetch module settings: ${response.status} ${response.statusText}`);
      }
      const modulesData = await response.json();

      const loadedModules: Module[] = Object.entries(modulesData.mcpServers).map(([id, entry]: [string, any]) => {
        const name = id.substring(id.lastIndexOf('/') + 1);
        return {
          id,
          name,
          version: entry.version || '1.0.0', // Use version from JSON if available, else placeholder
          description: entry.description || `Module for ${name}`, // Use description from JSON if available
          category: getCategoryFromName(name),
          enabled: !entry.disabled,
          installed: true, // All modules from JSON are considered "installed"
        };
      });
      setModules(loadedModules);
    } catch (error) {
      console.error('Error loading modules:', error);
      setModules([]);
    } finally {
      setLoading(false);
    }
  };

  const filteredModules = modules.filter(module =>
    module.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    module.description.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const groupedModules = filteredModules.reduce((acc, module) => {
    if (!acc[module.category]) {
      acc[module.category] = [];
    }
    acc[module.category].push(module);
    return acc;
  }, {} as Record<string, Module[]>);

  const handleToggleModuleStatus = (moduleId: string) => {
    setModules(prevModules =>
      prevModules.map(module =>
        module.id === moduleId ? { ...module, enabled: !module.enabled } : module
      )
    );
  };

  return (
    <motion.div
      className="min-h-[calc(100vh-64px)] bg-gray-50 p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="max-w-5xl mx-auto">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between mb-6">
          <div>
            <h1 className="text-2xl font-medium text-gray-800">Modules</h1>
            <p className="text-gray-600">Manage your AI extensions and components</p>
          </div>
          <div className="flex items-center space-x-2 mt-2 sm:mt-0">
            <button className="btn btn-outline" onClick={loadModules}>
              <RefreshCw size={16} className="mr-1.5" />
              Scan for Changes
            </button>
            <button className="btn btn-primary">
              <Plus size={16} className="mr-1.5" />
              Add Module
            </button>
          </div>
        </div>

        <div className="mb-6">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" size={18} />
            <input
              type="text"
              placeholder="Search modules..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-10 pr-4 py-2 w-full border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
            />
          </div>
        </div>

        {/* Module Categories */}
        <div className="space-y-8">
          {loading ? (
            <div className="text-center py-12">
              <InlineSpinner />
              <p className="mt-2 text-gray-500">Loading modules...</p>
            </div>
          ) : Object.keys(groupedModules).length === 0 ? (
            <div className="text-center py-12">
              <Package size={48} className="mx-auto text-gray-400 mb-4" />
              <h3 className="text-lg font-medium text-gray-900 mb-2">No modules found</h3>
              <p className="text-gray-600 mb-4">
                {searchTerm
                  ? 'Try adjusting your search criteria.'
                  : 'No modules are currently installed. Browse the module store to add functionality to your AI assistant.'}
              </p>
              <button className="btn btn-primary" onClick={() => navigate('/module-store')}>
                <Plus size={16} className="mr-1.5" />
                Browse Module Store
              </button>
            </div>
          ) : (
            Object.entries(groupedModules).map(([category, categoryModules]) => (
              <ModuleCategory
                key={category}
                title={category}
                description={getCategoryDescription(category)}
              >
                {categoryModules.map((module) => (
                  <ModuleItem
                    key={module.id}
                    id={module.id} // Pass the id
                    name={module.name}
                    version={module.version}
                    description={module.description}
                    enabled={module.enabled}
                    installed={module.installed}
                    onToggleStatus={handleToggleModuleStatus} // Pass the handler
                  />
                ))}
              </ModuleCategory>
            ))
          )}
        </div>
      </div>
    </motion.div>
  );

  function getCategoryDescription(category: string) {
    const descriptions: Record<string, string> = {
      'Voice & Audio': 'Speech recognition, synthesis, and voice control modules.',
      'AI Models': 'Large Language Models, vision models, and other AI capabilities.',
      'DevOps & Tools': 'Modules for development operations, system management, and utility functions.',
      'Data & Memory': 'Database connectors, memory management, and data processing modules.',
      'Web & Browser': 'Tools for web interaction, automation, and browsing assistance.',
      '3D & Graphics': 'Modules for 3D modeling, rendering, and graphical content generation.',
      'Integrations': 'Connectors for third-party services and platforms like GitHub, Notion, etc.',
      'Extensions': 'General extensions providing miscellaneous functionalities.'
    };
    return descriptions[category] || 'Various modules enhancing AI capabilities.';
  }
};

interface ModuleCategoryProps {
  title: string;
  description: string;
  children: React.ReactNode;
}

const ModuleCategory = ({ title, description, children }: ModuleCategoryProps) => {
  return (
    <div>
      <div className="mb-2">
        <h2 className="text-xl font-medium text-gray-800">{title}</h2>
        <p className="text-sm text-gray-600">{description}</p>
      </div>
      <div className="bg-white rounded-lg shadow-soft divide-y">
        {children}
      </div>
    </div>
  );
};

interface ModuleItemProps {
  id: string;
  name: string;
  version: string;
  description: string;
  enabled: boolean;
  installed: boolean;
  onToggleStatus: (id: string) => void;
}

const ModuleItem = ({ id, name, version, description, enabled, installed, onToggleStatus }: ModuleItemProps) => {
  return (
    <div 
      className="p-4 hover:bg-gray-100 transition-colors duration-150 cursor-pointer"
      onClick={() => onToggleStatus(id)}
    >
      <div className="flex items-start">
        <Package size={24} className="mr-3 text-gray-500 flex-shrink-0 mt-1" />
        <div className="flex-grow">
          <div className="flex items-center">
            <h3 className="text-base font-medium text-gray-800">{name}</h3>
            <div className={`w-3 h-3 rounded-full ${enabled ? 'bg-green-500' : 'bg-red-500'} ml-2`}></div>
            <span className="ml-auto text-xs text-gray-500">{version}</span>
          </div>
          <p className="mt-1 text-sm text-gray-600">{description}</p>
        </div>
      </div>

      {/* Optional: Keep action buttons or remove them based on further requirements */}
      {/* For this subtask, they are kept but the primary toggle is the item click */}
      <div className="mt-3 flex space-x-2 pl-9"> {/* Added pl-9 to align with description text */}
        <button 
          className="btn btn-ghost text-xs py-1 px-2" 
          onClick={(e) => { e.stopPropagation(); /* TODO: Implement configure action */ alert(`Configure ${name}`); }}
        >
          <Settings size={14} className="mr-1" />
          Configure
        </button>
        <button 
          className="btn btn-ghost text-xs py-1 px-2" 
          onClick={(e) => { e.stopPropagation(); /* TODO: Implement update action */ alert(`Update ${name}`); }}
        >
          <RefreshCw size={14} className="mr-1" />
          Update
        </button>
        {/* The main click toggles enabled status, so this button might be redundant or act as a secondary explicit toggle */}
        <button 
          className="btn btn-ghost text-xs py-1 px-2" 
          onClick={(e) => { e.stopPropagation(); onToggleStatus(id); }}
        >
          {enabled ? (
            <>
              <X size={14} className="mr-1" />
              Disable
            </>
          ) : (
            <>
              <Download size={14} className="mr-1" />
              Enable
            </>
          )}
        </button>
      </div>
    </div>
  );
};

export default ModulesPage;