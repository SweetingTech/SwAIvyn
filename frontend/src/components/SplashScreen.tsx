import React from 'react';
import { motion } from 'framer-motion';
import { Loader2, Zap, AlertCircle, RefreshCw } from 'lucide-react';

interface SplashScreenProps {
  isLoading: boolean;
  currentStep: string;
  error: string | null;
  onRetry?: () => void;
}

const SplashScreen: React.FC<SplashScreenProps> = ({ 
  isLoading, 
  currentStep, 
  error, 
  onRetry 
}) => {
  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-50 to-primary-100 flex items-center justify-center">
      <motion.div
        initial={{ opacity: 0, scale: 0.9 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ duration: 0.5 }}
        className="bg-white rounded-2xl shadow-2xl p-8 max-w-md w-full mx-4"
      >
        {/* Logo/Brand */}
        <div className="text-center mb-8">
          <motion.div
            initial={{ scale: 0 }}
            animate={{ scale: 1 }}
            transition={{ delay: 0.2, type: "spring", stiffness: 200 }}
            className="inline-flex items-center justify-center w-16 h-16 bg-primary-600 rounded-full mb-4"
          >
            <Zap className="w-8 h-8 text-white" />
          </motion.div>
          <h1 className="text-2xl font-bold text-gray-900 mb-2">SwAIvyn</h1>
          <p className="text-gray-600">AI Assistant Platform</p>
        </div>

        {/* Loading State */}
        {isLoading && !error && (
          <div className="text-center">
            <motion.div
              animate={{ rotate: 360 }}
              transition={{ duration: 2, repeat: Infinity, ease: "linear" }}
              className="inline-block mb-4"
            >
              <Loader2 className="w-8 h-8 text-primary-600" />
            </motion.div>
            <p className="text-gray-700 font-medium mb-2">Initializing...</p>
            <p className="text-sm text-gray-500">{currentStep}</p>
            
            {/* Progress Bar */}
            <div className="mt-4 bg-gray-200 rounded-full h-2 overflow-hidden">
              <motion.div
                className="h-full bg-primary-600 rounded-full"
                initial={{ width: "0%" }}
                animate={{ width: "100%" }}
                transition={{ duration: 3, ease: "easeInOut" }}
              />
            </div>
          </div>
        )}

        {/* Error State */}
        {error && (
          <div className="text-center">
            <motion.div
              initial={{ scale: 0 }}
              animate={{ scale: 1 }}
              transition={{ type: "spring", stiffness: 200 }}
              className="inline-flex items-center justify-center w-12 h-12 bg-red-100 rounded-full mb-4"
            >
              <AlertCircle className="w-6 h-6 text-red-600" />
            </motion.div>
            <h3 className="text-lg font-semibold text-gray-900 mb-2">
              Initialization Failed
            </h3>
            <p className="text-sm text-gray-600 mb-4">{error}</p>
            {onRetry && (
              <button
                onClick={onRetry}
                className="inline-flex items-center px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors"
              >
                <RefreshCw className="w-4 h-4 mr-2" />
                Retry
              </button>
            )}
          </div>
        )}

        {/* Success State (brief) */}
        {!isLoading && !error && (
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="text-center"
          >
            <motion.div
              initial={{ scale: 0 }}
              animate={{ scale: 1 }}
              transition={{ type: "spring", stiffness: 200 }}
              className="inline-flex items-center justify-center w-12 h-12 bg-green-100 rounded-full mb-4"
            >
              <motion.div
                initial={{ pathLength: 0 }}
                animate={{ pathLength: 1 }}
                transition={{ duration: 0.5 }}
              >
                ✓
              </motion.div>
            </motion.div>
            <p className="text-green-600 font-medium">Ready!</p>
          </motion.div>
        )}

        {/* Footer */}
        <div className="mt-8 text-center">
          <p className="text-xs text-gray-400">
            Powered by AI • Version 1.0
          </p>
        </div>
      </motion.div>
    </div>
  );
};

export default SplashScreen;
