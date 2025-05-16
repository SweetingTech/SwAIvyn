import { motion } from 'framer-motion';
import { Download, Settings, RefreshCw, X, Search, Plus } from 'lucide-react';

const ModulesPage = () => {
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
              className="pl-10 pr-4 py-2 w-full border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
            />
          </div>
        </div>
        
        {/* Module Categories */}
        <div className="space-y-8">
          <ModuleCategory 
            title="Voice & Audio" 
            description="Speech recognition and text-to-speech engines"
          >
            <ModuleItem
              name="Whisper STT Engine"
              version="v1.2.0"
              description="Advanced speech-to-text conversion using OpenAI's Whisper model"
              enabled={true}
            />
            <ModuleItem
              name="ElevenLabs TTS"
              version="v3.0.1"
              description="High-quality text-to-speech synthesis with natural-sounding voices"
              enabled={true}
            />
          </ModuleCategory>
          
          <ModuleCategory 
            title="AI Models" 
            description="Language models and providers"
          >
            <ModuleItem
              name="Ollama Integration"
              version="v2.1.0"
              description="Connect to local Ollama models for offline processing"
              enabled={true}
            />
            <ModuleItem
              name="OpenAI API Connector"
              version="v1.5.3"
              description="Connect to OpenAI's APIs for ChatGPT and other models"
              enabled={false}
            />
          </ModuleCategory>
          
          <ModuleCategory 
            title="Extensions" 
            description="Additional capabilities and integrations"
          >
            <ModuleItem
              name="Calendar Plugin"
              version="v1.0.2"
              description="Schedule and manage events with calendar integration"
              enabled={true}
            />
            <ModuleItem
              name="Web Search"
              version="v2.2.1"
              description="Perform web searches and retrieve information from the internet"
              enabled={false}
            />
          </ModuleCategory>
        </div>
      </div>
    </motion.div>
  );
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
}

const ModuleItem = ({ name, version, description, enabled }: ModuleItemProps) => {
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