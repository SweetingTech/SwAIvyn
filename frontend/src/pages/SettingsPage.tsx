import React, { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import {
  User, Network, Save, Speech, Database,
  Palette, Image as ImageIcon, Upload,
  Volume2, ServerCog, Plus, Edit, Trash2, Bot
} from 'lucide-react';
import { Tooltip } from '../components/Tooltip';
import chatService from '../services/chatService';
import CharacterEditor from './CharacterEditor';
import yaml from 'js-yaml';
import { useInitialization } from '../contexts/InitializationContext';

const tabs = [
  { id: 'account', label: 'Account', icon: <User size={16} /> },
  { id: 'invocation', label: 'Invocation', icon: <Speech size={16} /> },
  { id: 'model', label: 'AI Model', icon: <Database size={16} /> },
  { id: 'voice', label: 'Voice', icon: <Volume2 size={16} /> },
  { id: 'character', label: 'Character', icon: <ImageIcon size={16} /> },
  { id: 'agents', label: 'Connections', icon: <ServerCog size={16} /> },
  { id: 'agentstack', label: 'Agent Stack', icon: <Bot size={16} /> },
  { id: 'appearance', label: 'Appearance', icon: <Palette size={16} /> },
  { id: 'network', label: 'Network', icon: <Network size={16} /> }
];

const VoiceSettings = () => {
  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">Voice Settings</h2>
      <p className="text-sm text-gray-600 mb-4">
        Configure voice settings for your AI assistant.
      </p>
      {/* Add voice settings content here */}
    </div>
  );
};

const SettingsPage = () => {
  const [activeTab, setActiveTab] = useState('account');

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
          <h1 className="text-2xl font-medium text-gray-800">Settings</h1>
          <p className="text-gray-600">Configure your AI assistant</p>
        </div>

        <div className="bg-white rounded-lg shadow-soft overflow-hidden">
          <div className="sm:flex">
            <div className="sm:w-64 bg-gray-50 p-0">
              <nav className="sticky top-0">
                <ul className="divide-y divide-gray-200">
                  {tabs.map(tab => (
                    <li key={tab.id}>
                      <button
                        className={`w-full text-left px-4 py-3 flex items-center transition-colors duration-150 ${
                          activeTab === tab.id
                            ? 'bg-primary-50 text-primary-700 border-l-4 border-primary-500'
                            : 'text-gray-700 hover:bg-gray-100'
                        }`}
                        onClick={() => setActiveTab(tab.id)}
                      >
                        <span className="mr-2">{tab.icon}</span>
                        {tab.label}
                      </button>
                    </li>
                  ))}
                </ul>
              </nav>
            </div>

            <div className="flex-grow p-6">
              {activeTab === 'account' && <AccountSettings />}
              {activeTab === 'invocation' && <InvocationSettings />}
              {activeTab === 'model' && <ModelSettings />}
              {activeTab === 'voice' && <VoiceSettings />}
              {activeTab === 'character' && <CharacterSettings />}
              {activeTab === 'agents' && <AgentsSettings />}
              {activeTab === 'agentstack' && <AgentStackSettings />}
              {activeTab === 'appearance' && <AppearanceSettings />}
              {activeTab === 'network' && <NetworkSettings />}
            </div>
          </div>
        </div>
      </div>
    </motion.div>
  );
};

const AccountSettings = () => {
  const { user } = useInitialization();
  const [recoveryCodes, setRecoveryCodes] = useState<string[]|null>(null);
  const [pin, setPin] = useState('');
  const [pinSet, setPinSet] = useState(false);
  const [userInfo, setUserInfo] = useState({
    username: '',
    email: '',
    createdAt: '',
    lastLogin: ''
  });
  const [loading, setLoading] = useState(true);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');

  useEffect(() => {
    loadUserInfo();
  }, []);

  const loadUserInfo = async () => {
    try {
      setLoading(true);

      if (!user?.id) {
        // If no user from context, use fallback data
        setUserInfo({
          username: 'Default User',
          email: 'user@example.com',
          createdAt: new Date().toISOString(),
          lastLogin: new Date().toISOString()
        });
        return;
      }

      const response = await fetch(`/api/user/${user.id}`);
      if (response.ok) {
        const userData = await response.json();
        setUserInfo({
          username: userData.username || 'Default User',
          email: userData.email || 'user@example.com',
          createdAt: userData.createdAt || new Date().toISOString(),
          lastLogin: userData.lastLogin || new Date().toISOString()
        });
      } else {
        // Fallback data
        setUserInfo({
          username: 'Default User',
          email: 'user@example.com',
          createdAt: new Date().toISOString(),
          lastLogin: new Date().toISOString()
        });
      }
    } catch (error) {
      console.error('Error loading user info:', error);
      setUserInfo({
        username: 'Error Loading',
        email: 'error@example.com',
        createdAt: new Date().toISOString(),
        lastLogin: new Date().toISOString()
      });
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    try {
      return new Date(dateString).toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch {
      return 'Unknown';
    }
  };

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-center h-32">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600"></div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Account Settings</h2>
        <p className="text-sm text-gray-600 mb-6">
          Manage your account information and security settings
        </p>
      </div>

      {/* Success/Error Messages */}
      {saveSuccess && (
        <div className="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded-md">
          Account settings updated successfully!
        </div>
      )}
      {saveError && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {saveError}
        </div>
      )}

      {/* User Information */}
      <div className="bg-gray-50 border border-gray-200 rounded-md p-4">
        <h3 className="text-lg font-medium text-gray-900 mb-3">Account Information</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">Username</label>
            <p className="text-sm text-gray-900 mt-1">{userInfo.username}</p>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Email</label>
            <p className="text-sm text-gray-900 mt-1">{userInfo.email}</p>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Account Created</label>
            <p className="text-sm text-gray-900 mt-1">{formatDate(userInfo.createdAt)}</p>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Last Login</label>
            <p className="text-sm text-gray-900 mt-1">{formatDate(userInfo.lastLogin)}</p>
          </div>
        </div>
      </div>

      <div className="space-y-4">
        <div>
          <label htmlFor="password" className="block text-sm font-medium text-gray-700 mb-1">
            Password
          </label>
          <button className="btn btn-ghost border border-gray-300 text-sm">
            Change Password
          </button>
        </div>

        <div>
          <h3 className="text-sm font-medium text-gray-700 mb-1">PIN Code</h3>
          {pinSet ? (
            <p className="text-sm text-green-600">
              A PIN is already set. <button className="text-primary-600 underline" onClick={() => setPinSet(false)}>Change it</button>
            </p>
          ) : (
            <div className="flex items-center">
              <input
                type="password"
                maxLength={4}
                placeholder="4-digit PIN"
                value={pin}
                onChange={e => setPin(e.target.value.replace(/\D/,''))}
                className="w-24 px-2 py-1 border border-gray-300 rounded-md"
              />
              <button
                className="ml-2 btn btn-ghost border text-sm"
                onClick={() => setPinSet(true)}
                disabled={pin.length !== 4}
              >
                Save PIN
              </button>
            </div>
          )}
        </div>

        <div>
          <h3 className="text-sm font-medium text-gray-700 mb-2">Recovery Phrases</h3>
          {!recoveryCodes ? (
            <button
              className="btn btn-ghost border border-gray-300 text-sm"
              onClick={() => setRecoveryCodes([
                'alpha-bravo-charlie',
                'delta-echo-foxtrot',
                'golf-hotel-india',
                'juliet-kilo-lima',
                'mike-november-oscar'
              ])}
            >
              Generate Recovery Phrases
            </button>
          ) : (
            <>
              <textarea
                readOnly
                rows={5}
                className="w-full font-mono p-2 border border-gray-300 rounded-md bg-gray-50"
                value={recoveryCodes.join('\n')}
              />
              <p className="text-xs text-gray-500 mt-1">
                These phrases can be used once each to reset your password. Store them securely.
              </p>
            </>
          )}
        </div>
      </div>

      <div className="pt-4 flex justify-end">
        <button className="btn btn-primary">
          <Save size={16} className="mr-1.5" />
          Save Changes
        </button>
      </div>
    </div>
  );
};

const ModelSettings = () => {
  const { user } = useInitialization();
  const [selectedEngine, setSelectedEngine] = useState(''); // Start empty to avoid race conditions
  const [selectedModel, setSelectedModel] = useState('');
  const [ollamaModels, setOllamaModels] = useState<string[]>([]);
  const [ollamaApiUrl, setOllamaApiUrl] = useState('http://localhost:11434');
  const [lmStudioApiUrl, setLmStudioApiUrl] = useState('http://localhost:1234');
  const [openAiApiUrl, setOpenAiApiUrl] = useState('https://api.openai.com');
  const [openAiApiKey, setOpenAiApiKey] = useState('');
  const [claudeApiUrl, setClaudeApiUrl] = useState('https://api.anthropic.com');
  const [claudeApiKey, setClaudeApiKey] = useState('');
  const [enableStreaming, setEnableStreaming] = useState(true);
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [settingsLoaded, setSettingsLoaded] = useState(false); // Track if settings have been loaded

  // Load settings when user is available
  useEffect(() => {
    console.log('🚀 SettingsPage useEffect starting...');

    const loadUserAndSettings = async () => {
      try {
        console.log('🔄 Starting loadUserAndSettings...');

        if (!user?.id) {
          console.warn('⚠️ No user available from context yet');
          return;
        }

        console.log('✅ Using user ID from context:', user.id);

        // Now load settings with the user ID
        console.log('🔄 About to load settings with userId:', user.id);
        await loadSettings(user.id);
      } catch (error) {
        console.error('❌ Error loading user and settings:', error);
        setSaveError('Failed to load user information. Please try again.');
      }
    };

    loadUserAndSettings();
  }, [user?.id]);

  // Debug useEffect to track selectedEngine changes
  useEffect(() => {
    console.log('🔍 selectedEngine state changed to:', selectedEngine);
  }, [selectedEngine]);

  // Debug useEffect to track selectedModel changes
  useEffect(() => {
    console.log('🔍 selectedModel state changed to:', selectedModel);
  }, [selectedModel]);

  // Handle engine changes - load models when engine changes
  useEffect(() => {
    const loadModelsForEngine = async () => {
      // Only proceed if we have a valid setup and settings have been loaded
      if (!user?.id || loading || !settingsLoaded || !selectedEngine) return;

      try {
        if (selectedEngine === 'ollama') {
          console.log('🔄 Loading Ollama models...');
          const modelsResponse = await fetch('/api/llm/ollama/models');
          if (modelsResponse.ok) {
            const models = await modelsResponse.json();
            setOllamaModels(models);
            console.log('✅ Loaded Ollama models:', models);
          } else {
            // Use dummy data if API call fails
            setOllamaModels(['llama2', 'mistral', 'mixtral', 'phi4:latest']);
          }
        }
      } catch (error) {
        console.error('Error loading models for engine:', error);
        if (selectedEngine === 'ollama') {
          setOllamaModels(['llama2', 'mistral', 'mixtral', 'phi4:latest']);
        }
      }
    };

    loadModelsForEngine();
  }, [selectedEngine, user?.id, settingsLoaded]);

  // Load current settings function
  const loadSettings = async (userIdToUse: string) => {
    try {
      setLoading(true);
      console.log('🔄 Starting to load settings for user:', userIdToUse);

      // Validate user ID before making API calls
      if (!userIdToUse || userIdToUse.length < 30) {
        console.error('❌ Invalid user ID provided to loadSettings:', userIdToUse);
        setSaveError('Invalid user ID. Please refresh the page.');
        return;
      }

      // Get current LLM settings with user ID
      const settings = await chatService.getLlmSettings(userIdToUse);
      console.log('🔄 Raw settings from API:', settings);
      console.log('🔄 Settings type:', typeof settings);
      console.log('🔄 Settings.engine:', settings.engine);
      console.log('🔄 Settings.model:', settings.model);

      const engineToSet = settings.engine || 'ollama';
      let modelToSet = settings.model || '';

      // Verify the model matches the engine and get current state
      if (engineToSet === 'lmstudio') {
        // For LM Studio, get the actual current model from the API
        try {
          const modelsResponse = await fetch('/api/llm/lmstudio/models');
          if (modelsResponse.ok) {
            const modelsData = await modelsResponse.json();
            if (modelsData.data && modelsData.data.length > 0) {
              modelToSet = modelsData.data[0].id; // Use actual current model
              console.log('🔄 Detected actual LM Studio model:', modelToSet);
            }
          }
        } catch (error) {
          console.error('Error getting LM Studio current model:', error);
        }
      } else if (engineToSet === 'ollama') {
        // For Ollama, verify the model exists in available models
        try {
          const modelsResponse = await fetch('/api/llm/ollama/models');
          if (modelsResponse.ok) {
            const models = await modelsResponse.json();
            setOllamaModels(models);
            // If stored model doesn't exist in available models, use first available
            if (!models.includes(modelToSet)) {
              modelToSet = models[0] || '';
              console.log('🔄 Stored Ollama model not found, using:', modelToSet);
            }
          }
        } catch (error) {
          console.error('Error getting Ollama models:', error);
        }
      }

      console.log('🔄 About to set engine to:', engineToSet);
      console.log('🔄 About to set model to:', modelToSet);

      setSelectedEngine(engineToSet);
      setSelectedModel(modelToSet);

      console.log('✅ Called setSelectedEngine with:', engineToSet);
      console.log('✅ Called setSelectedModel with:', modelToSet);

      // Mark settings as loaded to prevent race conditions
      setSettingsLoaded(true);

      // Add a small delay to check if state updates
      setTimeout(() => {
        console.log('🔍 State check after 100ms - selectedEngine:', selectedEngine);
        console.log('🔍 State check after 100ms - selectedModel:', selectedModel);
      }, 100);

      // Get connection settings from API with user ID
      try {
        // Validate user ID before making connection settings API call
        if (!userIdToUse || userIdToUse.length < 30) {
          console.warn('⚠️ Invalid user ID for connection settings, using defaults');
          setOllamaApiUrl('http://localhost:11434');
          setLmStudioApiUrl('http://localhost:1234');
          setEnableStreaming(true);
        } else {
          console.log('🔄 Loading connection settings for user:', userIdToUse);
          const connectionResponse = await fetch(`/api/settings/connections?userId=${userIdToUse}`);
          if (connectionResponse.ok) {
            const connectionSettings = await connectionResponse.json();
            console.log('🔄 Connection settings loaded:', connectionSettings);
            setOllamaApiUrl(connectionSettings.ollamaApiUrl || 'http://localhost:11434');
            setLmStudioApiUrl(connectionSettings.lmStudioApiUrl || 'http://localhost:1234');
            setOpenAiApiUrl(connectionSettings.openAiApiUrl || 'https://api.openai.com');
            setOpenAiApiKey(connectionSettings.openAiApiKey || '');
            setClaudeApiUrl(connectionSettings.claudeApiUrl || 'https://api.anthropic.com');
            setClaudeApiKey(connectionSettings.claudeApiKey || '');
            setEnableStreaming(connectionSettings.enableStreaming !== false); // Default to true
          } else {
            console.warn('⚠️ Connection settings API call failed:', connectionResponse.status);
            // Use default values if API call fails
            setOllamaApiUrl('http://localhost:11434');
            setLmStudioApiUrl('http://localhost:1234');
            setEnableStreaming(true);
          }
        }
      } catch (connectionError) {
        console.error('❌ Error loading connection settings:', connectionError);
        // Use default values if API call fails
        setOllamaApiUrl('http://localhost:11434');
        setLmStudioApiUrl('http://localhost:1234');
        setOpenAiApiUrl('https://api.openai.com');
        setOpenAiApiKey('');
        setClaudeApiUrl('https://api.anthropic.com');
        setClaudeApiKey('');
        setEnableStreaming(true);
      }

      // Get available models from API
      try {
        if (settings.engine === 'ollama') {
          const modelsResponse = await fetch('/api/llm/ollama/models');
          if (modelsResponse.ok) {
            const models = await modelsResponse.json();
            setOllamaModels(models);
          } else {
            // Use dummy data if API call fails
            setOllamaModels(['llama2', 'mistral', 'mixtral', 'phi4:latest']);
          }
        }
      } catch (modelsError) {
        console.error('Error loading models:', modelsError);
        // Use dummy data if API call fails
        setOllamaModels(['llama2', 'mistral', 'mixtral', 'phi4:latest']);
      }
    } catch (error) {
      console.error('Error loading LLM settings:', error);
      setSaveError('Failed to load settings. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  // Save settings
  const saveSettings = async () => {
    try {
      setLoading(true);
      setSaveSuccess(false);
      setSaveError('');

      // Skip saving if no valid user ID
      if (!user?.id) {
        setSaveError('Cannot save settings: No valid user ID available');
        return;
      }

      // Save LLM settings with user ID
      await chatService.updateLlmSettings(selectedEngine, selectedModel, user.id);

      // Save connection settings to API with user ID
      try {
        await fetch('/api/settings/connections', {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            UserId: user.id, // Pass the actual user ID (capital U to match backend)
            OllamaApiUrl: ollamaApiUrl || '', // Capital O and A to match backend, ensure not null
            LmStudioApiUrl: lmStudioApiUrl || '', // Capital L, S, A, U to match backend, ensure not null
            OpenAiApiUrl: openAiApiUrl || '',
            OpenAiApiKey: openAiApiKey || '',
            ClaudeApiUrl: claudeApiUrl || '',
            ClaudeApiKey: claudeApiKey || '',
            Neo4jUri: '', // Include Neo4jUri as empty string (required by backend model)
            EnableStreaming: enableStreaming // Capital E and S to match backend
          })
        });
        console.log('Connection settings saved successfully');
      } catch (connectionError) {
        console.error('Error saving connection settings:', connectionError);
        // Continue even if connection settings fail
      }

      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 5000); // Show success message for 5 seconds
    } catch (error) {
      console.error('Error saving LLM settings:', error);
      setSaveError('Failed to save settings. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">AI Model Settings</h2>

      {loading && (
        <div className="text-sm text-gray-500">Loading settings...</div>
      )}



      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            LLM Engine
            <Tooltip text="Choose which language model engine to use (Ollama, LM Studio, OpenAI, or Claude)">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          {/* Debug info */}
          <div className="text-xs text-gray-500 mb-1">
            Debug: selectedEngine = "{selectedEngine}" | loading = {loading.toString()}
          </div>
          <select
            className="w-full border rounded px-3 py-2"
            value={selectedEngine}
            onChange={async (e) => {
              const newEngine = e.target.value;
              console.log('🔄 Engine dropdown changed to:', newEngine);
              setSelectedEngine(newEngine);

              // Auto-detect and set the current model for the selected engine
              try {
                if (newEngine === 'ollama') {
                  // For Ollama, keep the current model selection or use first available
                  const modelsResponse = await fetch('/api/llm/ollama/models');
                  if (modelsResponse.ok) {
                    const models = await modelsResponse.json();
                    setOllamaModels(models);
                    // Keep current model if it exists in the list, otherwise use first available
                    if (!selectedModel || !models.includes(selectedModel)) {
                      setSelectedModel(models[0] || '');
                    }
                  }
                } else if (newEngine === 'lmstudio') {
                  // For LM Studio, get the actual current model from the API
                  const modelsResponse = await fetch('/api/llm/lmstudio/models');
                  if (modelsResponse.ok) {
                    const modelsData = await modelsResponse.json();
                    if (modelsData.data && modelsData.data.length > 0) {
                      const currentModel = modelsData.data[0].id;
                      setSelectedModel(currentModel);
                      console.log('🔄 Auto-detected LM Studio model:', currentModel);
                    } else {
                      setSelectedModel('');
                    }
                  }
                }
              } catch (error) {
                console.error('Error auto-detecting model for engine:', newEngine, error);
              }
            }}
            disabled={loading}
          >
            <option value="ollama">Ollama</option>
            <option value="lmstudio">LM Studio</option>
            <option value="openai">OpenAI</option>
            <option value="claude">Claude</option>
          </select>
        </div>

        {selectedEngine === 'ollama' && (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
                Ollama API URL
                <Tooltip text="The URL where your Ollama server is running. Default: http://localhost:11434">
                  <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
                </Tooltip>
              </label>
              <div className="flex space-x-2">
                <input
                  type="text"
                  placeholder="http://localhost:11434"
                  value={ollamaApiUrl}
                  onChange={e => setOllamaApiUrl(e.target.value)}
                  className="flex-grow border rounded px-3 py-2"
                  disabled={loading}
                />
                <button
                  className="btn btn-primary"
                  onClick={async () => {
                    try {
                      setLoading(true);
                      const response = await fetch(`${ollamaApiUrl}/v1/models`);
                      if (response.ok) {
                        const data = await response.json();
                        if (data.models && data.models.length > 0) {
                          alert(`Connection successful! Found models: ${data.models.map((m: { name: string }) => m.name).join(', ')}`);
                        } else {
                          alert('Connection successful, but no models found.');
                        }
                      } else {
                        alert(`Connection failed: ${response.status} ${response.statusText}`);
                      }
                    } catch (error) {
                      const message = error instanceof Error ? error.message : String(error);
                      alert(`Connection failed: ${message}`);
                    } finally {
                      setLoading(false);
                    }
                  }}
                  disabled={loading}
                >
                  Test Connection
                </button>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Ollama Model
              </label>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={e => setSelectedModel(e.target.value)}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {ollamaModels.map((model: string) => (
                  <option key={model} value={model}>{model}</option>
                ))}
              </select>
            </div>
          </>
        )}

        {selectedEngine === 'lmstudio' && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
              LM Studio API URL
              <Tooltip text="The URL where your LM Studio server is running. Default: http://localhost:1234">
                <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
              </Tooltip>
            </label>
            <div className="flex space-x-2">
              <input
                type="text"
                placeholder="http://localhost:1234"
                value={lmStudioApiUrl}
                onChange={e => setLmStudioApiUrl(e.target.value)}
                className="flex-grow border rounded px-3 py-2"
                disabled={loading}
              />
              <button
                className="btn btn-primary"
                onClick={async () => {
                  try {
                    setLoading(true);
                    const response = await fetch(`${lmStudioApiUrl}/v1/models`);
                    if (response.ok) {
                      const data = await response.json();
                      if (data.data && data.data.length > 0) {
                        alert(`Connection successful! Found model: ${data.data[0].id}`);
                      } else {
                        alert('Connection successful, but no models found.');
                      }
                    } else {
                      alert(`Connection failed: ${response.status} ${response.statusText}`);
                    }
                  } catch (error) {
                    const message = error instanceof Error ? error.message : String(error);
                    alert(`Connection failed: ${message}`);
                  } finally {
                    setLoading(false);
                  }
                }}
                disabled={loading}
              >
                Test Connection
              </button>
            </div>
          </div>
        )}

        {selectedEngine === 'openai' && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
              OpenAI API URL
            </label>
            <input
              type="text"
              value={openAiApiUrl}
              onChange={e => setOpenAiApiUrl(e.target.value)}
              className="w-full border rounded px-3 py-2"
              disabled={loading}
            />
            <label className="block text-sm font-medium text-gray-700 mt-2">API Key</label>
            <input
              type="password"
              value={openAiApiKey}
              onChange={e => setOpenAiApiKey(e.target.value)}
              className="w-full border rounded px-3 py-2"
              disabled={loading}
            />
          </div>
        )}

        {selectedEngine === 'claude' && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
              Claude API URL
            </label>
            <input
              type="text"
              value={claudeApiUrl}
              onChange={e => setClaudeApiUrl(e.target.value)}
              className="w-full border rounded px-3 py-2"
              disabled={loading}
            />
            <label className="block text-sm font-medium text-gray-700 mt-2">API Key</label>
            <input
              type="password"
              value={claudeApiKey}
              onChange={e => setClaudeApiKey(e.target.value)}
              className="w-full border rounded px-3 py-2"
              disabled={loading}
            />
          </div>
        )}

        {/* Streaming Option */}
        <div>
          <label className="flex items-center space-x-2">
            <input
              type="checkbox"
              checked={enableStreaming}
              onChange={e => setEnableStreaming(e.target.checked)}
              className="rounded border-gray-300"
              disabled={loading}
            />
            <span className="text-sm font-medium text-gray-700">Enable Streaming</span>
            <Tooltip text="When enabled, responses will stream in real-time as they're generated. When disabled, you'll receive the complete response at once.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <p className="text-xs text-gray-500 mt-1">
            {enableStreaming
              ? "Responses will appear word-by-word as they're generated"
              : "You'll receive the complete response all at once"
            }
          </p>
        </div>
      </div>

      <div className="pt-4 flex justify-between items-center">
        <div>
          {saveSuccess && (
            <div className="fixed top-4 right-4 bg-green-600 text-white px-4 py-2 rounded shadow-lg z-50 animate-fade-in">
              Settings saved successfully!
            </div>
          )}
          {saveError && (
            <div className="fixed top-4 right-4 bg-red-600 text-white px-4 py-2 rounded shadow-lg z-50 animate-fade-in">
              {saveError}
            </div>
          )}
        </div>
        <button
          className="btn btn-primary flex items-center"
          onClick={saveSettings}
          disabled={loading}
        >
          {loading ? <span className="loader mr-2"></span> : <Save size={16} className="mr-1.5" />}
          {loading ? 'Saving...' : 'Save Changes'}
        </button>
      </div>
    </div>
  );
};

interface Avatar {
  id: string;
  type: string;
  thumbnailPath: string;
}

interface Character {
  id: string;
  userId: string;
  name: string;
  yamlProfile: string;
  createdAt: string;
  lastModified: string;
}

const CharacterSettings = () => {
  const [cardFile, setCardFile] = useState<File|null>(null);
  const [avatars, setAvatars] = useState<Avatar[]>([]);
  const [activeAvatar, setActiveAvatar] = useState('');
  const [characters, setCharacters] = useState<Character[]>([]);
  const [showEditor, setShowEditor] = useState(false);
  const [editingCharacter, setEditingCharacter] = useState<string | undefined>(undefined);
  const [userId, setUserId] = useState('demo-user-id');

  useEffect(() => {
    setAvatars([{ id: '1', type: '2D', thumbnailPath: '/default-avatar.png' }]);
    loadUserAndCharacters();
  }, []);

  const loadUserAndCharacters = async () => {
    try {
      // Use the default and only user ID for this application
      const userIdToUse = '00000000-0000-0000-0000-000000000001';
      setUserId(userIdToUse);

      // Load characters with the user ID
      await loadCharacters(userIdToUse);
    } catch (error) {
      console.error('Error loading user and characters:', error);
    }
  };

  const loadCharacters = async (userIdToUse?: string) => {
    try {
      // Always use the hardcoded default user ID
      const idToUse = userIdToUse || '00000000-0000-0000-0000-000000000001';
      console.log('🔍 CharacterSettings: Loading characters with userId:', idToUse);

      // Skip API call if userId is invalid
      if (!idToUse || idToUse.length !== 36) {
        console.warn('🔍 CharacterSettings: Invalid userId, skipping character loading:', idToUse);
        setCharacters([]);
        return;
      }

      const response = await fetch(`/api/character/user/${idToUse}`);
      if (response.ok) {
        const data = await response.json();
        setCharacters(data);
      }
    } catch (error) {
      console.error('Error loading characters:', error);
    }
  };

  const handleCreateCharacter = () => {
    setEditingCharacter(undefined);
    setShowEditor(true);
  };

  const handleEditCharacter = (characterId: string) => {
    setEditingCharacter(characterId);
    setShowEditor(true);
  };

  const handleDeleteCharacter = async (characterId: string) => {
    if (confirm('Are you sure you want to delete this character?')) {
      try {
        const response = await fetch(`/api/character/${characterId}`, {
          method: 'DELETE'
        });
        if (response.ok) {
          loadCharacters();
        }
      } catch (error) {
        console.error('Error deleting character:', error);
      }
    }
  };

  const handleSaveCharacter = () => {
    setShowEditor(false);
    setEditingCharacter(undefined);
    loadCharacters();
  };

  // Character Modal Overlay
  const CharacterModal = () => {
    if (!showEditor) return null;

    // Handle escape key to close modal
    React.useEffect(() => {
      const handleEscape = (e: KeyboardEvent) => {
        if (e.key === 'Escape') {
          setShowEditor(false);
        }
      };
      document.addEventListener('keydown', handleEscape);
      return () => document.removeEventListener('keydown', handleEscape);
    }, []);

    return (
      <div
        className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
        onClick={(e) => {
          // Close modal when clicking backdrop
          if (e.target === e.currentTarget) {
            setShowEditor(false);
          }
        }}
      >
        <div className="bg-white rounded-lg shadow-xl max-w-5xl w-full max-h-[90vh] overflow-hidden">
          {/* Modal Header */}
          <div className="flex items-center justify-between p-6 border-b bg-gray-50">
            <h2 className="text-xl font-medium text-gray-800">
              {editingCharacter ? 'Edit Character' : 'Create Character'}
            </h2>
            <button
              onClick={() => setShowEditor(false)}
              className="p-2 text-gray-500 hover:text-gray-700 rounded-full hover:bg-gray-100"
            >
              ✕
            </button>
          </div>

          {/* Modal Content */}
          <div className="overflow-y-auto max-h-[calc(90vh-80px)]">
            <CharacterEditor
              userId={userId}
              characterId={editingCharacter}
              onSave={handleSaveCharacter}
              onCancel={() => setShowEditor(false)}
            />
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Character Management</h2>
        <p className="text-sm text-gray-600 mb-6">
          Create and manage AI character profiles using YAML format
        </p>
      </div>

      <div className="flex justify-between items-center">
        <h3 className="text-lg font-medium">Your Characters</h3>
        <button
          onClick={handleCreateCharacter}
          className="btn btn-primary flex items-center"
        >
          <Plus size={16} className="mr-1.5" />
          Create Character
        </button>
      </div>

      <div className="grid gap-4">
        {characters.length === 0 ? (
          <div className="text-center py-8 text-gray-500">
            <p>No characters created yet.</p>
            <p className="text-sm">Click "Create Character" to get started.</p>
          </div>
        ) : (
          characters.map(character => {
            // Extract character name from YAML
            let characterName = 'Unnamed Character';
            try {
              const parsed = yaml.load(character.yamlProfile) as any;
              characterName = parsed?.name || 'Unnamed Character';
            } catch (error) {
              // Use fallback name if YAML parsing fails
            }

            return (
              <div key={character.id} className="border rounded-lg p-4 bg-white hover:shadow-md transition-shadow">
                <div className="flex justify-between items-start">
                  <div className="flex-grow">
                    <h4 className="font-medium text-gray-800">{characterName}</h4>
                    <p className="text-sm text-gray-500">
                      Created: {new Date(character.createdAt).toLocaleDateString()}
                    </p>
                    <p className="text-sm text-gray-500">
                      Modified: {new Date(character.lastModified).toLocaleDateString()}
                    </p>
                  </div>
                <div className="flex space-x-2">
                  <button
                    onClick={() => {
                      // Generate system prompt from YAML
                      let systemPrompt = '';
                      try {
                        const parsed = yaml.load(character.yamlProfile) as any;
                        if (parsed) {
                          systemPrompt = `You are roleplaying as the AI character below. Remain fully in character.

Name: ${parsed.name || 'Unknown Character'}
Description: ${parsed.description || ''}
Personality: ${parsed.personality || ''}
Scenario: ${parsed.scenario || ''}
System Prompt: ${parsed.system_prompt || ''}

Respond using the character's voice and personality at all times.`;
                        }
                      } catch (error) {
                        console.error('Error parsing YAML for chat:', error);
                      }

                      // Store character selection for chat
                      localStorage.setItem('selectedCharacterId', character.id);
                      // Navigate to chat
                      window.location.href = '/chat';
                    }}
                    className="btn btn-ghost border text-green-600 hover:bg-green-50 text-sm flex items-center"
                  >
                    💬 Chat
                  </button>
                  <button
                    onClick={() => handleEditCharacter(character.id)}
                    className="btn btn-ghost border text-sm flex items-center"
                  >
                    <Edit size={14} className="mr-1" />
                    Edit
                  </button>
                  <button
                    onClick={() => handleDeleteCharacter(character.id)}
                    className="btn btn-ghost border border-red-300 text-red-600 text-sm flex items-center hover:bg-red-50"
                  >
                    <Trash2 size={14} className="mr-1" />
                    Delete
                  </button>
                </div>
              </div>
            </div>
            );
          })
        )}
      </div>

      <div>
        <h3 className="text-lg font-medium mb-4">Import Character Card</h3>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Upload Character Card (JSON)
          </label>
          <div className="flex items-center">
            <input
              type="file"
              accept=".json"
              onChange={e => setCardFile(e.target.files?.[0]||null)}
              className="mr-2"
            />
            <button
              disabled={!cardFile}
              onClick={() => {}}
              className="btn btn-ghost border text-sm flex items-center"
            >
              <Upload size={14} className="mr-1" />
              Import
            </button>
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-lg font-medium mb-4">Avatar Settings</h3>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Avatars
          </label>
          <div className="flex space-x-4 mb-4">
            {avatars.map(a => (
              <div
                key={a.id}
                onClick={() => setActiveAvatar(a.id)}
                className={`cursor-pointer p-1 border rounded-lg ${
                  a.id === activeAvatar ? 'border-primary-500' : 'border-gray-200'
                }`}
              >
                {a.type === '2D' ? (
                  <img
                    src={a.thumbnailPath}
                    alt="Avatar"
                    className="w-16 h-16 object-cover rounded"
                  />
                ) : (
                  <div className="w-16 h-16 bg-gray-100 flex items-center justify-center text-xs rounded">
                    3D Placeholder
                  </div>
                )}
              </div>
            ))}
          </div>
          <input
            type="file"
            accept="image/*"
            className="block w-full text-sm text-gray-500
              file:mr-4 file:py-2 file:px-4
              file:rounded-full file:border-0
              file:text-sm file:font-semibold
              file:bg-primary-50 file:text-primary-700
              hover:file:bg-primary-100"
          />
        </div>
      </div>

      {/* Character Modal */}
      <CharacterModal />
    </div>
  );
};

interface Agent {
  id: string;
  name: string;
  description: string;
  enabled: boolean;
}

const AgentsSettings = () => {
  const [agents, setAgents] = useState<Agent[]>([]);

  const toggleAgent = (id: string) => {
    setAgents(agents.map(a =>
      a.id === id ? { ...a, enabled: !a.enabled } : a
    ));
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">Connected Agents</h2>
      <p className="text-sm text-gray-600 mb-4">
        These background agents manage tasks, timing, and quality control.
      </p>
      <ul className="space-y-3">
        {agents.map(a => (
          <li key={a.id} className="flex items-center justify-between p-3 border rounded-md">
            <div>
              <p className="font-medium">{a.name}</p>
              <p className="text-xs text-gray-500">{a.description}</p>
            </div>
            <button
              onClick={() => toggleAgent(a.id)}
              className={`px-3 py-1 rounded ${
                a.enabled
                  ? 'bg-red-100 text-red-700'
                  : 'bg-green-100 text-green-700'
              }`}
            >
              {a.enabled ? 'Disable' : 'Enable'}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
};

const InvocationSettings = () => {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Invocation Settings</h2>
        <p className="text-sm text-gray-600 mb-6">
          Configure how you interact with your AI assistant
        </p>
      </div>

      <div className="space-y-4">
        <div>
          <label htmlFor="ai-name" className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            AI Name
            <Tooltip text="Set the name for your AI assistant">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <input
            id="ai-name"
            type="text"
            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
          />
          <p className="mt-1 text-xs text-gray-500">
            This is how you'll refer to your AI assistant
          </p>
        </div>

        <div>
          <label htmlFor="wake-word" className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            Wake Word
            <Tooltip text="Set the phrase to activate voice recognition">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <input
            id="wake-word"
            type="text"
            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
          />
          <p className="mt-1 text-xs text-gray-500">
            Say this phrase to activate voice recognition
          </p>
        </div>

        <div>
          <label htmlFor="wake-sensitivity" className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            Wake Word Sensitivity
            <Tooltip text="Adjust the sensitivity for detecting the wake word">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <input
            id="wake-sensitivity"
            type="range"
            min="1"
            max="10"
            className="w-full"
          />
          <div className="flex justify-between text-xs text-gray-500 mt-1">
            <span>Less Sensitive</span>
            <span>More Sensitive</span>
          </div>
        </div>
      </div>

      <div className="pt-4 flex justify-end">
        <button className="btn btn-primary">
          <Save size={16} className="mr-1.5" />
          Save Changes
        </button>
      </div>
    </div>
  );
};

const AgentStackSettings = () => {
  const [agentStackUrl, setAgentStackUrl] = useState('http://localhost:8080');
  const [agentStackApiKey, setAgentStackApiKey] = useState('');
  const [connected, setConnected] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');

  useEffect(() => {
    loadAgentStackSettings();
  }, []);

  const loadAgentStackSettings = async () => {
    try {
      setLoading(true);

      // Use the default and only user ID for this application
      const userId = '00000000-0000-0000-0000-000000000001';

      // Load agent stack settings from API
      const settingsResponse = await fetch(`/api/settings?userId=${userId}`);
      if (settingsResponse.ok) {
        const settings = await settingsResponse.json();
        setAgentStackUrl(settings.AgentStackUrl || 'http://localhost:8080');
        setAgentStackApiKey(settings.AgentStackApiKey || '');
      }
    } catch (error) {
      console.error('Error loading agent stack settings:', error);
    } finally {
      setLoading(false);
    }
  };

  const testConnection = async () => {
    try {
      setLoading(true);
      const response = await fetch(`${agentStackUrl}/health`);
      if (response.ok) {
        setConnected(true);
        alert('Connection successful!');
      } else {
        setConnected(false);
        alert('Connection failed. Please check the URL and try again.');
      }
    } catch (error) {
      setConnected(false);
      alert('Connection failed. Please check the URL and try again.');
    } finally {
      setLoading(false);
    }
  };

  const saveAgentStackSettings = async () => {
    try {
      setLoading(true);
      setSaveSuccess(false);
      setSaveError('');

      // Use the default and only user ID for this application
      const userId = '00000000-0000-0000-0000-000000000001';

      // Save settings to API
      const settingsToSave = {
        AgentStackUrl: agentStackUrl,
        AgentStackApiKey: agentStackApiKey
      };

      const response = await fetch('/api/settings', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          UserId: userId,
          Settings: settingsToSave
        })
      });

      if (response.ok) {
        setSaveSuccess(true);
        setTimeout(() => setSaveSuccess(false), 3000);
      } else {
        setSaveError('Failed to save settings. Please try again.');
      }
    } catch (error) {
      console.error('Error saving agent stack settings:', error);
      setSaveError('Failed to save settings. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Agent Stack Connection</h2>
        <p className="text-sm text-gray-600 mb-6">
          Connect to an external agent stack for advanced API access, MCP support, and agent functionality
        </p>
      </div>

      {/* Success/Error Messages */}
      {saveSuccess && (
        <div className="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded-md">
          Agent stack settings saved successfully!
        </div>
      )}
      {saveError && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {saveError}
        </div>
      )}

      <div className="space-y-4">
        {/* Agent Stack URL */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Agent Stack URL
          </label>
          <div className="flex space-x-2">
            <input
              type="text"
              value={agentStackUrl}
              onChange={(e) => setAgentStackUrl(e.target.value)}
              placeholder="http://localhost:8080"
              className="flex-grow border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
              disabled={loading}
            />
            <button
              onClick={testConnection}
              className="btn btn-primary"
              disabled={loading}
            >
              Test Connection
            </button>
          </div>
          <p className="text-xs text-gray-500 mt-1">
            URL of the external agent stack server
          </p>
        </div>

        {/* API Key */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            API Key (Optional)
          </label>
          <input
            type="password"
            value={agentStackApiKey}
            onChange={(e) => setAgentStackApiKey(e.target.value)}
            placeholder="Enter API key if required"
            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
            disabled={loading}
          />
          <p className="text-xs text-gray-500 mt-1">
            API key for authentication (if required by the agent stack)
          </p>
        </div>

        {/* Connection Status */}
        <div className="bg-gray-50 border border-gray-200 rounded-md p-4">
          <div className="flex items-center space-x-2">
            <div className={`w-3 h-3 rounded-full ${connected ? 'bg-green-500' : 'bg-red-500'}`}></div>
            <span className="text-sm font-medium text-gray-700">
              Status: {connected ? 'Connected' : 'Disconnected'}
            </span>
          </div>
          <p className="text-xs text-gray-500 mt-1">
            {connected
              ? 'Successfully connected to the agent stack'
              : 'Not connected to the agent stack'
            }
          </p>
        </div>

        {/* Features Info */}
        <div className="bg-blue-50 border border-blue-200 rounded-md p-4">
          <h4 className="text-sm font-medium text-blue-900 mb-2">Agent Stack Features</h4>
          <ul className="text-xs text-blue-700 space-y-1">
            <li>• External API integrations</li>
            <li>• Model Context Protocol (MCP) support</li>
            <li>• Advanced agent orchestration</li>
            <li>• Custom tool and function calling</li>
            <li>• Multi-agent workflows</li>
          </ul>
        </div>
      </div>

      <div className="pt-4 flex justify-end">
        <button
          className="btn btn-primary flex items-center"
          onClick={saveAgentStackSettings}
          disabled={loading}
        >
          {loading ? <span className="loader mr-2"></span> : <Save size={16} className="mr-1.5" />}
          {loading ? 'Saving...' : 'Save Changes'}
        </button>
      </div>
    </div>
  );
};

const AppearanceSettings = () => {
  const [theme, setTheme] = useState('dark');
  const [language, setLanguage] = useState('en');
  const [accentColor, setAccentColor] = useState('#8B5CF6');
  const [fontSize, setFontSize] = useState('medium');
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');

  // Simple fallback translations for now
  const t = (key: string) => {
    const translations: Record<string, string> = {
      'appearance.title': 'Appearance Settings',
      'appearance.subtitle': 'Customize the look and feel of your AI assistant',
      'appearance.theme': 'Theme',
      'appearance.light': 'Light',
      'appearance.dark': 'Dark',
      'appearance.system': 'System',
      'appearance.language': 'Language',
      'appearance.selectLanguage': 'Select your preferred language for the interface',
      'appearance.accentColor': 'Accent Color',
      'appearance.fontSize': 'Font Size',
      'appearance.adjustFontSize': 'Adjust the size of text throughout the application',
      'appearance.small': 'Small',
      'appearance.medium': 'Medium',
      'appearance.large': 'Large',
      'common.save': 'Save',
      'settings.saveChanges': 'Save Changes',
      'settings.saving': 'Saving...',
      'settings.settingsSaved': 'Appearance settings saved successfully!',
      'settings.failedToSave': 'Failed to save settings. Please try again.'
    };
    return translations[key] || key;
  };

  // Load settings on component mount
  useEffect(() => {
    loadAppearanceSettings();
  }, []);

  const loadAppearanceSettings = async () => {
    try {
      setLoading(true);

      // Use the default and only user ID for this application
      const userId = '00000000-0000-0000-0000-000000000001';

      // Load appearance settings from API
      const settingsResponse = await fetch(`/api/settings?userId=${userId}`);
      if (settingsResponse.ok) {
        const settings = await settingsResponse.json();
        setTheme(settings.Theme || 'dark');
        setLanguage(settings.Language || 'en');
        setAccentColor(settings.AccentColor || '#8B5CF6');
        setFontSize(settings.FontSize || 'medium');
      }
    } catch (error) {
      console.error('Error loading appearance settings:', error);
    } finally {
      setLoading(false);
    }
  };

  const saveAppearanceSettings = async () => {
    try {
      setLoading(true);
      setSaveSuccess(false);
      setSaveError('');

      // Use the default and only user ID for this application
      const userId = '00000000-0000-0000-0000-000000000001';

      // Save settings to API
      const settingsToSave = {
        Theme: theme,
        Language: language,
        AccentColor: accentColor,
        FontSize: fontSize
      };

      // Also update the browser language if i18n is available
      if (typeof window !== 'undefined' && (window as any).i18n) {
        try {
          await (window as any).i18n.changeLanguage(language);
        } catch (error) {
          console.log('i18n not available, language change will apply on next reload');
        }
      }

      const response = await fetch('/api/settings', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          UserId: userId,
          Settings: settingsToSave
        })
      });

      if (response.ok) {
        setSaveSuccess(true);
        setTimeout(() => setSaveSuccess(false), 3000);
      } else {
        setSaveError('Failed to save settings. Please try again.');
      }
    } catch (error) {
      console.error('Error saving appearance settings:', error);
      setSaveError('Failed to save settings. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const themes = [
    { id: 'light', name: 'Light', preview: 'bg-white border-gray-200' },
    { id: 'dark', name: 'Dark', preview: 'bg-gray-800' },
    { id: 'system', name: 'System', preview: 'bg-gradient-to-b from-white to-gray-800' }
  ];

  const accentColors = [
    { id: '#8B5CF6', name: 'Purple', color: 'bg-purple-500' },
    { id: '#3B82F6', name: 'Blue', color: 'bg-blue-500' },
    { id: '#10B981', name: 'Green', color: 'bg-green-500' },
    { id: '#F59E0B', name: 'Orange', color: 'bg-orange-500' },
    { id: '#EF4444', name: 'Red', color: 'bg-red-500' }
  ];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Appearance Settings</h2>
        <p className="text-sm text-gray-600 mb-6">
          Customize the look and feel of your AI assistant
        </p>
      </div>

      {/* Success/Error Messages */}
      {saveSuccess && (
        <div className="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded-md">
          Appearance settings saved successfully!
        </div>
      )}
      {saveError && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {saveError}
        </div>
      )}

      <div className="space-y-6">
        {/* Theme Selection */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-3">
            Theme
          </label>
          <div className="grid grid-cols-3 gap-3">
            {themes.map((themeOption) => (
              <button
                key={themeOption.id}
                onClick={() => setTheme(themeOption.id)}
                className={`flex flex-col items-center p-3 border rounded-md transition-all ${
                  theme === themeOption.id
                    ? 'border-primary-500 bg-primary-50 shadow-sm'
                    : 'border-gray-300 bg-white hover:border-gray-400'
                }`}
                disabled={loading}
              >
                <div className={`w-full h-16 ${themeOption.preview} border border-gray-200 rounded-md mb-2`}></div>
                <span className="text-xs font-medium">{themeOption.name}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Language Selection */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Language
          </label>
          <select
            value={language}
            onChange={(e) => setLanguage(e.target.value)}
            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
            disabled={loading}
          >
            <option value="en">English</option>
            <option value="ja">日本語 (Japanese)</option>
          </select>
          <p className="text-xs text-gray-500 mt-1">
            Select your preferred language for the interface
          </p>
        </div>

        {/* Accent Color */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-3">
            Accent Color
          </label>
          <div className="grid grid-cols-5 gap-3">
            {accentColors.map((colorOption) => (
              <button
                key={colorOption.id}
                onClick={() => setAccentColor(colorOption.id)}
                className={`w-full h-12 ${colorOption.color} rounded-md border-2 transition-all ${
                  accentColor === colorOption.id
                    ? 'border-gray-800 scale-105'
                    : 'border-gray-300 hover:border-gray-400'
                }`}
                disabled={loading}
                title={colorOption.name}
              />
            ))}
          </div>
        </div>

        {/* Font Size */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Font Size
          </label>
          <select
            value={fontSize}
            onChange={(e) => setFontSize(e.target.value)}
            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
            disabled={loading}
          >
            <option value="small">Small</option>
            <option value="medium">Medium</option>
            <option value="large">Large</option>
          </select>
          <p className="text-xs text-gray-500 mt-1">
            Adjust the size of text throughout the application
          </p>
        </div>
      </div>

      <div className="pt-4 flex justify-end">
        <button
          className="btn btn-primary flex items-center"
          onClick={saveAppearanceSettings}
          disabled={loading}
        >
          {loading ? <span className="loader mr-2"></span> : <Save size={16} className="mr-1.5" />}
          {loading ? 'Saving...' : 'Save Changes'}
        </button>
      </div>
    </div>
  );
};

const NetworkSettings = () => {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Network Settings</h2>
        <p className="text-sm text-gray-600 mb-6">
          Configure network and connection settings
        </p>
      </div>

      <div className="space-y-4">
        <div className="flex items-center">
          <input
            id="federation"
            type="checkbox"
            className="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
          />
          <label htmlFor="federation" className="ml-2 block text-sm text-gray-700 flex items-center">
            Enable Federation
            <Tooltip text="Allow your AI to communicate with other instances, sharing selected memories and capabilities">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
        </div>
        <p className="text-xs text-gray-500">
          Federation allows your AI to communicate with other instances, sharing selected memories and capabilities
        </p>

        <div>
          <label htmlFor="server-url" className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            Federation Server
            <Tooltip text="Enter the URL of the federation server">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <input
            id="server-url"
            type="text"
            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
            placeholder="Enter server URL"
          />
        </div>

        <div className="flex items-center">
          <input
            id="offline-mode"
            type="checkbox"
            className="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
          />
          <label htmlFor="offline-mode" className="ml-2 block text-sm text-gray-700 flex items-center">
            Offline Mode
            <Tooltip text="Enable offline mode to use only local models and avoid network requests">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
        </div>
        <p className="text-xs text-gray-500">
          In offline mode, your AI will only use local models and won't make any network requests
        </p>
      </div>

      <div className="pt-4 flex justify-end">
        <button className="btn btn-primary">
          <Save size={16} className="mr-1.5" />
          Save Changes
        </button>
      </div>
    </div>
  );
};

export default SettingsPage;