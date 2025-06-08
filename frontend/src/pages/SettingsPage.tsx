import React, { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import InlineSpinner from '../components/ui/InlineSpinner';
import {
  User,
  Network,
  Save,
  Speech,
  Database,
  Palette,
  Image as ImageIcon,
  Upload,
  Volume2,
  ServerCog,
  Plus,
  Edit,
  Trash2,
  Bot,
} from 'lucide-react';
import CharacterEditor from './CharacterEditor';
import chatService from '../services/chatService';
import ttsService from '../services/ttsService';
import yaml from 'js-yaml';
import { Tooltip } from '../components/Tooltip';
import { useInitialization } from '../contexts/InitializationContext';
import fetchWithTimeout from '../utils/fetchWithTimeout';

// Hardcoded model arrays (defined outside component to prevent recreation on every render)
const OPENAI_MODELS = [
  'gpt-4o-mini',
  'gpt-4o',
  'gpt-4-turbo',
  'gpt-4',
  'gpt-3.5-turbo',
];

const CLAUDE_MODELS = [
  'claude-3-5-sonnet-20241022',
  'claude-3-5-haiku-20241022',
  'claude-3-opus-20240229',
  'claude-3-sonnet-20240229',
  'claude-3-haiku-20240307',
];

const tabs = [
  { id: 'account', label: 'Account', icon: <User size={16} /> },
  { id: 'invocation', label: 'Invocation', icon: <Speech size={16} /> },
  { id: 'model', label: 'AI Model', icon: <Database size={16} /> },
  { id: 'voice', label: 'Voice', icon: <Volume2 size={16} /> },
  { id: 'character', label: 'Character', icon: <ImageIcon size={16} /> },
  { id: 'agents', label: 'Connections', icon: <ServerCog size={16} /> },
  { id: 'agentstack', label: 'Agent Stack', icon: <Bot size={16} /> },
  { id: 'appearance', label: 'Appearance', icon: <Palette size={16} /> },
  { id: 'network', label: 'Network', icon: <Network size={16} /> },
];

const VoiceSettings = () => {
  const { user } = useInitialization();
  const [apiKey, setApiKey] = useState('');
  const [voice, setVoice] = useState('Rachel');
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);

  const voices = ['Rachel', 'Domi', 'Bella', 'Antoni'];

  useEffect(() => {
    if (!user?.id) return;
    setLoading(true);
    ttsService.getSettings(user.id)
      .then(result => {
        setApiKey(result.apiKey || '');
        setVoice(result.voice || 'Rachel');
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [user?.id]);

  const save = () => {
    if (!user?.id) return;
    setLoading(true);
    ttsService.updateSettings(apiKey, voice, user.id)
      .then(() => {
        setSaveSuccess(true);
        setTimeout(() => setSaveSuccess(false), 3000);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">Voice Settings</h2>
      <p className="text-sm text-gray-600">Configure voice settings for your AI assistant.</p>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            ElevenLabs API Key
          </label>
          <input
            type="password"
            className="w-full border rounded px-3 py-2"
            value={apiKey}
            onChange={e => setApiKey(e.target.value)}
            disabled={loading}
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Voice</label>
          <select
            className="w-full border rounded px-3 py-2"
            value={voice}
            onChange={e => setVoice(e.target.value)}
            disabled={loading}
          >
            {voices.map(v => (
              <option key={v} value={v}>
                {v}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="pt-4 flex justify-end">
        <button className="btn btn-primary" onClick={save} disabled={loading}>
          {loading ? 'Saving...' : 'Save Changes'}
        </button>
        {saveSuccess && <div className="text-green-600 text-sm ml-4">Saved!</div>}
      </div>
    </div>
  );
};

const AccountSettings = () => {
  const { user } = useInitialization();
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [pin, setPin] = useState('');
  const [pinSet, setPinSet] = useState(false);
  const [userInfo, setUserInfo] = useState({
    username: '',
    email: '',
    createdAt: '',
    lastLogin: '',
  });
  const [loading, setLoading] = useState(true);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');

  useEffect(() => {
    const loadUserInfo = async () => {
      setLoading(true);
      try {
        if (!user?.id) throw new Error();
        const response = await fetch(`/api/user/${user.id}`);
        if (!response.ok) throw new Error();
        const userData = await response.json();
        setUserInfo({
          username: userData.username || 'Default User',
          email: userData.email || 'user@example.com',
          createdAt: userData.createdAt || new Date().toISOString(),
          lastLogin: userData.lastLogin || new Date().toISOString(),
        });
      } catch {
        setUserInfo({
          username: 'Error Loading',
          email: 'error@example.com',
          createdAt: new Date().toISOString(),
          lastLogin: new Date().toISOString(),
        });
      } finally {
        setLoading(false);
      }
    };
    loadUserInfo();
  }, [user?.id]);

  const formatDate = (dateString: string) => {
    try {
      return new Date(dateString).toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return 'Unknown';
    }
  };

  return loading ? (
    <div className="flex items-center justify-center h-32">
      <InlineSpinner />
    </div>
  ) : (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800">Account Settings</h2>
        <p className="text-sm text-gray-600">Manage your account information and security settings</p>
      </div>

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
          <label className="block text-sm font-medium text-gray-700 mb-1">Password</label>
          <button className="btn btn-ghost border border-gray-300 text-sm">Change Password</button>
        </div>

        <div>
          <h3 className="text-sm font-medium text-gray-700 mb-1">PIN Code</h3>
          {pinSet ? (
            <p className="text-sm text-green-600">
              A PIN is already set.&nbsp;
              <button
                className="text-primary-600 underline"
                onClick={() => setPinSet(false)}
              >
                Change it
              </button>
            </p>
          ) : (
            <div className="flex items-center">
              <input
                type="password"
                maxLength={4}
                placeholder="4-digit PIN"
                value={pin}
                onChange={e => setPin(e.target.value.replace(/\D/, ''))}
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
              onClick={() =>
                setRecoveryCodes([
                  'alpha-bravo-charlie',
                  'delta-echo-foxtrot',
                  'golf-hotel-india',
                  'juliet-kilo-lima',
                  'mike-november-oscar',
                ])
              }
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
        <button className="btn btn-primary flex items-center">
          <Save size={16} className="mr-1.5" />
          Save Changes
        </button>
      </div>
    </div>
  );
};

const ModelSettings = () => {
  const { user } = useInitialization();
  const [selectedEngine, setSelectedEngine] = useState<'ollama' | 'openai' | 'claude' | 'lmstudio'>('claude');
  const [selectedModel, setSelectedModel] = useState<string>('');
  const [ollamaApiUrl, setOllamaApiUrl] = useState('http://localhost:11434');
  const [lmStudioApiUrl, setLmStudioApiUrl] = useState('http://localhost:1234');
  const [openAiApiKey, setOpenAiApiKey] = useState('');
  const [claudeApiUrl, setClaudeApiUrl] = useState('https://api.anthropic.com/v1');
  const [claudeApiKey, setClaudeApiKey] = useState('');
  const [enableStreaming, setEnableStreaming] = useState(true);
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [cachedOllamaModels, setCachedOllamaModels] = useState<string[]>([]);

  useEffect(() => {
    if (!user?.id) return;
    setLoading(true);

    // Load LLM settings
    chatService.getLlmSettings(user.id)
      .then(settings => {
        setSelectedEngine(settings.engine as any);
        setSelectedModel(settings.model || '');
      })
      .catch(() => {});

    // Load connection settings (API keys) - backend returns PascalCase
    fetch(`/api/settings/connections?userId=${user.id}`)
      .then(response => response.json())
      .then(data => {
        if (data.OpenAiApiKey) setOpenAiApiKey(data.OpenAiApiKey);
        if (data.ClaudeApiKey) setClaudeApiKey(data.ClaudeApiKey);
        if (data.ClaudeApiUrl) setClaudeApiUrl(data.ClaudeApiUrl);
        if (data.OllamaApiUrl) setOllamaApiUrl(data.OllamaApiUrl);
        if (data.LmStudioApiUrl) setLmStudioApiUrl(data.LmStudioApiUrl);
        if (data.EnableStreaming !== undefined) setEnableStreaming(data.EnableStreaming);
      })
      .catch(() => {})
      .finally(() => setLoading(false));

    const cached = localStorage.getItem('cachedOllamaModels');
    if (cached) setCachedOllamaModels(JSON.parse(cached));
  }, [user?.id]);

  const refreshOllamaModels = async () => {
    try {
      const apiUrl = selectedEngine === 'lmstudio' ? lmStudioApiUrl : ollamaApiUrl;
      const endpoint = selectedEngine === 'lmstudio' ? '/v1/models' : '/api/tags';

      const res = await fetchWithTimeout(`${apiUrl}${endpoint}`);
      if (res.ok) {
        const json = await res.json();
        let models: string[] = [];

        if (selectedEngine === 'lmstudio') {
          // LM Studio uses OpenAI-compatible format
          models = json.data?.map((m: any) => m.id) || [];
        } else {
          // Ollama format
          models = json.models?.map((m: any) => m.name) || [];
        }

        setCachedOllamaModels(models);
        localStorage.setItem('cachedOllamaModels', JSON.stringify(models));
      }
    } catch (e) {
      console.error('Model refresh failed', e);
    }
  };

  const refreshOpenAiModels = () => {
    console.log('🔄 Would fetch latest OpenAI models here');
  };
  const refreshClaudeModels = () => {
    console.log('🔄 Would fetch latest Claude models here');
  };

  const renderEngineSpecificFields = () => {
    switch (selectedEngine) {
      case 'ollama':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Ollama API URL
              </label>
              <input
                type="text"
                placeholder="http://localhost:11434"
                value={ollamaApiUrl}
                onChange={e => setOllamaApiUrl(e.target.value)}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
            </div>

            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">Model</label>
                <button
                  onClick={refreshOllamaModels}
                  className="text-sm text-blue-600 hover:text-blue-800"
                  disabled={loading}
                >
                  🔄 Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={e => setSelectedModel(e.target.value)}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {cachedOllamaModels.map(m => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
            </div>
          </>
        );

      case 'openai':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                OpenAI API URL
              </label>
              <input
                type="text"
                value={openAiApiKey ? 'https://api.openai.com/v1' : ''}
                readOnly
                className="w-full border rounded px-3 py-2 bg-gray-50"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                OpenAI API Key
              </label>
              <input
                type="password"
                placeholder="sk-..."
                value={openAiApiKey}
                onChange={e => setOpenAiApiKey(e.target.value)}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
            </div>

            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">Model</label>
                <button
                  onClick={refreshOpenAiModels}
                  className="text-sm text-blue-600 hover:text-blue-800"
                  disabled={loading}
                >
                  🔄 Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={e => setSelectedModel(e.target.value)}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {OPENAI_MODELS.map(m => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
            </div>
          </>
        );

      case 'claude':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Claude API URL
              </label>
              <input
                type="text"
                placeholder="https://api.anthropic.com/v1"
                value={claudeApiUrl}
                onChange={e => setClaudeApiUrl(e.target.value)}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Claude API Key
              </label>
              <input
                type="password"
                placeholder="sk-ant-..."
                value={claudeApiKey}
                onChange={e => setClaudeApiKey(e.target.value)}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
            </div>

            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">Model</label>
                <button
                  onClick={refreshClaudeModels}
                  className="text-sm text-blue-600 hover:text-blue-800"
                  disabled={loading}
                >
                  🔄 Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={e => setSelectedModel(e.target.value)}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {CLAUDE_MODELS.map(m => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
            </div>
          </>
        );

      case 'lmstudio':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                LM Studio API URL
              </label>
              <input
                type="text"
                placeholder="http://localhost:1234"
                value={lmStudioApiUrl}
                onChange={e => setLmStudioApiUrl(e.target.value)}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
            </div>

            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">Model</label>
                <button
                  onClick={refreshOllamaModels}
                  className="text-sm text-blue-600 hover:text-blue-800"
                  disabled={loading}
                >
                  🔄 Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={e => setSelectedModel(e.target.value)}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {cachedOllamaModels.map(m => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
            </div>
          </>
        );

      default:
        return null;
    }
  };

  const saveSettings = async () => {
    if (!user?.id) return;
    setLoading(true);
    setSaveError('');
    try {
      // Save LLM engine and model settings
      await chatService.updateLlmSettings(selectedEngine, selectedModel, user.id);

      // Save API keys and connection settings
      const connectionSettings: any = {
        UserId: user.id,
        EnableStreaming: enableStreaming
      };

      // Add API keys based on selected engine (using PascalCase to match backend)
      if (selectedEngine === 'openai' && openAiApiKey) {
        connectionSettings.OpenAiApiKey = openAiApiKey;
        connectionSettings.OpenAiApiUrl = 'https://api.openai.com/v1';
      }

      if (selectedEngine === 'claude' && claudeApiKey) {
        connectionSettings.ClaudeApiKey = claudeApiKey;
        connectionSettings.ClaudeApiUrl = claudeApiUrl || 'https://api.anthropic.com/v1';
      }

      if (selectedEngine === 'ollama' && ollamaApiUrl) {
        connectionSettings.OllamaApiUrl = ollamaApiUrl;
      }

      if (selectedEngine === 'lmstudio' && lmStudioApiUrl) {
        connectionSettings.LmStudioApiUrl = lmStudioApiUrl;
      }

      // Save connection settings
      await fetch('/api/settings/connections', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(connectionSettings)
      });

      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch (e) {
      console.error('Save failed:', e);
      setSaveError('Save failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">AI Model Settings</h2>
      <p className="text-sm text-gray-600">Configure your AI model provider</p>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            LLM Engine
          </label>
          <select
            className="w-full border rounded px-3 py-2"
            value={selectedEngine}
            onChange={e => {
              console.log('🔄 LLM Engine changed to:', e.target.value);
              setSelectedEngine(e.target.value as any);
              // Reset model selection when engine changes
              setSelectedModel('');
            }}
            disabled={loading}
          >
            <option value="claude">Claude</option>
            <option value="openai">OpenAI</option>
            <option value="ollama">Ollama</option>
            <option value="lmstudio">LM Studio</option>
          </select>
        </div>

        {renderEngineSpecificFields()}

        <div>
          <label className="flex items-center space-x-2">
            <input
              type="checkbox"
              checked={enableStreaming}
              onChange={e => setEnableStreaming(e.target.checked)}
              className="rounded border-gray-300"
              disabled={loading}
            />
            <span className="text-sm font-medium text-gray-700">
              Enable Streaming
            </span>
          </label>
          <p className="text-xs text-gray-500 mt-1">
            Responses stream in real time.
          </p>
        </div>
      </div>

      <div className="pt-4 flex justify-end">
        {saveSuccess && (
          <div className="fixed top-4 right-4 bg-green-600 text-white px-4 py-2 rounded shadow-lg z-50">
            Settings saved successfully!
          </div>
        )}
        {saveError && (
          <div className="fixed top-4 right-4 bg-red-600 text-white px-4 py-2 rounded shadow-lg z-50">
            {saveError}
          </div>
        )}
        <button
          className="btn btn-primary flex items-center"
          onClick={saveSettings}
          disabled={loading}
        >
          {loading ? 'Saving...' : <><Save size={16} className="mr-1.5"/>Save Changes</>}
        </button>
      </div>
    </div>
  );
};

const CharacterSettings = () => {
  // ... (unchanged)
  return <div>/* your character settings code */</div>;
};

const AgentsSettings = () => (
  <div className="space-y-6">
    <h2 className="text-xl font-medium">Connected Agents</h2>
    <p className="text-sm text-gray-600">These background agents manage tasks and quality control.</p>
  </div>
);

const InvocationSettings = () => (
  <div className="space-y-6">
    <h2 className="text-xl font-medium text-gray-800">Invocation Settings</h2>
    <p className="text-sm text-gray-600">Configure how you interact with your AI assistant.</p>
  </div>
);

const AgentStackSettings = () => (
  <div className="space-y-6">
    <h2 className="text-xl font-medium text-gray-800">Agent Stack Connection</h2>
    <p className="text-sm text-gray-600">Connect to an external agent stack for advanced API access.</p>
  </div>
);

const AppearanceSettings = () => (
  <div className="space-y-6">
    <h2 className="text-xl font-medium text-gray-800">Appearance Settings</h2>
    <p className="text-sm text-gray-600">Customize the look and feel of your AI assistant.</p>
  </div>
);

const NetworkSettings = () => (
  <div className="space-y-6">
    <h2 className="text-xl font-medium text-gray-800">Network Settings</h2>
    <p className="text-sm text-gray-600">Configure network and connection options.</p>
  </div>
);

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
            <div className="sm:w-64 bg-gray-50">
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

export default SettingsPage;
