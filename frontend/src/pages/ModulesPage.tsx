import { motion } from 'framer-motion';
import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Download, Settings, RefreshCw, X, Search, Plus, Package } from 'lucide-react';
import InlineSpinner from '../components/ui/InlineSpinner';

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

  const loadModules = async () => {
    try {
      setLoading(true);

      // Use the default and only user ID for this application
      const userId = '00000000-0000-0000-0000-000000000001';

      // Load modules from API
      const response = await fetch(`/api/modules/user/${userId}`);
      if (response.ok) {
        const data = await response.json();
        setModules(data || []);
      } else {
        // No modules found or API error
        setModules([]);
      }
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
          <button className="btn btn-primary mt-2 sm:mt-0">
            <Plus size={16} className="mr-1.5" />
            Add Module
          </button>
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
                    name={module.name}
                    version={module.version}
                    description={module.description}
                    enabled={module.enabled}
                    installed={module.installed}
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
      'Voice & Audio': 'Speech recognition and text-to-speech engines',
      'AI Models': 'Language models and providers',
      'Extensions': 'Additional capabilities and integrations',
      'Tools': 'Utility tools and helpers',
      'Integrations': 'Third-party service connections'
    };
    return descriptions[category] || 'Additional modules and extensions';
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
  name: string;
  version: string;
  description: string;
  enabled: boolean;
  installed: boolean;
}

const ModuleItem = ({ name, version, description, enabled, installed }: ModuleItemProps) => {
  return (
    <div className="p-4 hover:bg-gray-50 transition-colors duration-150">
      <div className="flex items-center justify-between">
        <div className="flex-grow">
          <div className="flex items-center">
            <h3 className="text-base font-medium text-gray-800">{name}</h3>
            <span className="ml-2 text-xs text-gray-500">{version}</span>
            <span className={`ml-auto text-xs font-medium px-2 py-0.5 rounded ${
              enabled
                ? 'bg-success-100 text-success-800'
                : 'bg-gray-100 text-gray-800'
            }`}>
              {enabled ? 'Enabled' : 'Disabled'}
            </span>
          </div>
          <p className="mt-1 text-sm text-gray-600">{description}</p>
        </div>
      </div>

      <div className="mt-3 flex space-x-2">
        <button className="btn btn-ghost text-xs py-1 px-2">
          <Settings size={14} className="mr-1" />
          Configure
        </button>
        <button className="btn btn-ghost text-xs py-1 px-2">
          <RefreshCw size={14} className="mr-1" />
          Update
        </button>
        <button className="btn btn-ghost text-xs py-1 px-2">
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