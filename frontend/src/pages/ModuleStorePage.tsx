import React from 'react';
import { motion } from 'framer-motion';
import { ArrowLeft } from 'lucide-react';
import { Link } from 'react-router-dom';

const ModuleStorePage: React.FC = () => {
  return (
    <motion.div
      className="min-h-[calc(100vh-64px)] bg-gray-50 p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="max-w-5xl mx-auto">
        <div className="mb-6">
          <Link to="/modules" className="flex items-center text-primary-500 hover:text-primary-700 transition-colors">
            <ArrowLeft size={20} className="mr-2" />
            Back to Modules
          </Link>
        </div>
        <h1 className="text-2xl font-medium text-gray-800 mb-4">Module Store</h1>
        <div className="bg-white p-6 rounded-lg shadow-soft">
          <p className="text-gray-700">
            Welcome to the Module Store! This is where you will be able to browse and install new MCP modules to extend the capabilities of your AI assistant.
          </p>
          <p className="text-gray-700 mt-2">
            This section is currently under development.
          </p>
        </div>
      </div>
    </motion.div>
  );
};

export default ModuleStorePage;
