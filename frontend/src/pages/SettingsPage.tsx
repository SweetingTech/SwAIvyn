import React, { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import {
  User, Network, Save, Speech, Database,
  Palette, Image as ImageIcon, Upload,
  Volume2, ServerCog, Plus, Edit, Trash2
} from 'lucide-react';
import { Tooltip } from '../components/Tooltip';
import chatService from '../services/chatService';
import CharacterEditor from './CharacterEditor';
import yaml from 'js-yaml';

const tabs = [
  { id: 'account', label: 'Account', icon: <User size={16} /> },
  { id: 'invocation', label: 'Invocation', icon: <Speech size={16} /> },
  { id: 'model', label: 'AI Model', icon: <Database size={16} /> },
  { id: 'voice', label: 'Voice', icon: <Volume2 size={16} /> },
  { id: 'character', label: 'Character', icon: <ImageIcon size={16} /> },
  { id: 'agents', label: 'Connections', icon: <ServerCog size={16} /> },
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
  const [recoveryCodes, setRecoveryCodes] = useState<string[]|null>(null);
  const [pin, setPin] = useState('');
  const [pinSet, setPinSet] = useState(false);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Account Settings</h2>
        <p className="text-sm text-gray-600 mb-6">
          Manage your account information and security settings
        </p>
      </div>

      <div className="space-y-4">
        <div>
          <label htmlFor="username" className="block text-sm font-medium text-gray-700 mb-1">
            Username
          </label>
          <input
            id="username"
            type="text"
            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
          />
        </div>

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
  const [selectedEngine, setEngine] = useState('ollama');
  const [selectedModel, setModel] = useState('');
  const [ollamaModels, setOllamaModels] = useState<string[]>([]);
  const [ollamaApiUrl, setOllamaApiUrl] = useState('http://localhost:11434');
  const [lmStudioApiUrl, setLmStudioApiUrl] = useState('http://localhost:1234');
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');

  // Load current settings on component mount
  useEffect(() => {
    const loadSettings = async () => {
      try {
        setLoading(true);
        // Get current LLM settings
        const settings = await chatService.getLlmSettings();
        setEngine(settings.engine || 'ollama');
        setModel(settings.model || '');

        // Get connection settings from API
        try {
          const connectionResponse = await fetch('/api/settings/connections');
          if (connectionResponse.ok) {
            const connectionSettings = await connectionResponse.json();
            setOllamaApiUrl(connectionSettings.ollamaApiUrl || 'http://localhost:11434');
            setLmStudioApiUrl(connectionSettings.lmStudioApiUrl || 'http://localhost:1234');
          } else {
            // Use default values if API call fails
            setOllamaApiUrl('http://localhost:11434');
            setLmStudioApiUrl('http://localhost:1234');
          }
        } catch (connectionError) {
          console.error('Error loading connection settings:', connectionError);
          // Use default values if API call fails
          setOllamaApiUrl('http://localhost:11434');
          setLmStudioApiUrl('http://localhost:1234');
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
              setOllamaModels(['llama2', 'mistral', 'mixtral', 'phi']);
            }
          }
        } catch (modelsError) {
          console.error('Error loading models:', modelsError);
          // Use dummy data if API call fails
          setOllamaModels(['llama2', 'mistral', 'mixtral', 'phi']);
        }
      } catch (error) {
        console.error('Error loading LLM settings:', error);
        setSaveError('Failed to load settings. Please try again.');
      } finally {
        setLoading(false);
      }
    };

    loadSettings();
  }, []);

  // Save settings
  const saveSettings = async () => {
    try {
      setLoading(true);
      setSaveSuccess(false);
      setSaveError('');

      // Save LLM settings
      await chatService.updateLlmSettings(selectedEngine, selectedModel);

      // Save connection settings to API
      try {
        await fetch('/api/settings/connections', {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            ollamaApiUrl: ollamaApiUrl,
            lmStudioApiUrl: lmStudioApiUrl
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
            <Tooltip text="Choose which language model engine to use (Ollama or LM Studio)">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <select
            className="w-full border rounded px-3 py-2"
            value={selectedEngine}
            onChange={e => setEngine(e.target.value)}
            disabled={loading}
          >
            <option value="ollama">Ollama</option>
            <option value="lmstudio">LM Studio</option>
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
                onChange={e => setModel(e.target.value)}
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
  const [userId] = useState('dc42bfd4-a6d3-4706-8932-c221bf771a0f'); // TODO: Get from auth context

  useEffect(() => {
    setAvatars([{ id: '1', type: '2D', thumbnailPath: '/default-avatar.png' }]);
    loadCharacters();
  }, []);

  const loadCharacters = async () => {
    try {
      const response = await fetch(`/api/character/user/${userId}`);
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

const AppearanceSettings = () => {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Appearance Settings</h2>
        <p className="text-sm text-gray-600 mb-6">
          Customize the look and feel of your AI assistant
        </p>
      </div>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Theme
          </label>
          <div className="grid grid-cols-3 gap-3">
            <button className="flex flex-col items-center p-3 border border-gray-300 rounded-md bg-white">
              <div className="w-full h-16 bg-white border border-gray-200 rounded-md mb-2"></div>
              <span className="text-xs">Light</span>
            </button>
            <button className="flex flex-col items-center p-3 border border-primary-500 rounded-md bg-white shadow-sm">
              <div className="w-full h-16 bg-gray-800 rounded-md mb-2"></div>
              <span className="text-xs">Dark</span>
            </button>
            <button className="flex flex-col items-center p-3 border border-gray-300 rounded-md bg-white">
              <div className="w-full h-16 bg-gradient-to-b from-white to-gray-800 rounded-md mb-2"></div>
              <span className="text-xs">System</span>
            </button>
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Accent Color
          </label>
          <div className="grid grid-cols-5 gap-2">
            <button className="w-full h-10 bg-primary-500 rounded-md border-2 border-primary-700"></button>
            <button className="w-full h-10 bg-secondary-500 rounded-md"></button>
            <button className="w-full h-10 bg-accent-500 rounded-md"></button>
            <button className="w-full h-10 bg-success-500 rounded-md"></button>
            <button className="w-full h-10 bg-error-500 rounded-md"></button>
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