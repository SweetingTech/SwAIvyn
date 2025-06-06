import React, { useState, useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import useEngineModels from '../hooks/useEngineModels';
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
    const load = async () => {
      if (!user?.id) return;
      setLoading(true);
      try {
        const result = await ttsService.getSettings(user.id);
        setApiKey(result.apiKey || '');
        setVoice(result.voice || 'Rachel');
      } catch {
        // ignore
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [user?.id]);

  const save = async () => {
    if (!user?.id) return;
    setLoading(true);
    try {
      await ttsService.updateSettings(apiKey, voice, user.id);
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
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
    loadUserInfo();
  }, [user?.id]);

  const loadUserInfo = async () => {
    setLoading(true);
    try {
      if (!user?.id) {
        setUserInfo({
          username: 'Default User',
          email: 'user@example.com',
          createdAt: new Date().toISOString(),
          lastLogin: new Date().toISOString(),
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
          lastLogin: userData.lastLogin || new Date().toISOString(),
        });
      } else {
        setUserInfo({
          username: 'Default User',
          email: 'user@example.com',
          createdAt: new Date().toISOString(),
          lastLogin: new Date().toISOString(),
        });
      }
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
  const qc = useQueryClient();
  const [selectedEngine, setSelectedEngine] = useState('');
  const [selectedModel, setSelectedModel] = useState('');
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
  const [settingsLoaded, setSettingsLoaded] = useState(false);

  const { data: ollamaModels = [] } = useEngineModels('ollama');
  const { data: lmstudioModels } = useEngineModels('lmstudio');
  const { data: openAiModels = [] } = useEngineModels('openai');
  const { data: claudeModels = [] } = useEngineModels('claude');

  useEffect(() => {
    if (user?.id) {
      loadSettings(user.id);
    }
  }, [user?.id]);

  useEffect(() => {
    if (!user?.id) return;
    ['ollama', 'lmstudio', 'openai', 'claude'].forEach(engine =>
      qc.prefetchQuery({
        queryKey: ['models', engine],
        queryFn: () => fetchWithTimeout(`/api/llm/${engine}/models`).then(r => r.json()),
      }),
    );
  }, [user?.id, qc]);

  useEffect(() => {
    if (!settingsLoaded) return;

    const map: Record<string, string[]> = {
      ollama: ollamaModels,
      lmstudio: lmstudioModels?.data?.map((m: any) => m.id) ?? [],
      openai: openAiModels,
      claude: claudeModels,
    };

    const list = map[selectedEngine] || [];
    if (list.length > 0 && !list.includes(selectedModel)) {
      setSelectedModel(list[0]);
    }
  }, [selectedEngine, settingsLoaded, ollamaModels, lmstudioModels, openAiModels, claudeModels]);

  const loadSettings = async (userIdToUse: string) => {
    setLoading(true);
    try {
      if (!userIdToUse) return;
      const settings = await chatService.getLlmSettings(userIdToUse);
      const engineToSet = settings.engine || 'ollama';
      const modelToSet = settings.model || '';

      setSelectedEngine(engineToSet);
      setSelectedModel(modelToSet);
      setSettingsLoaded(true);

      // Load connection settings
      const connRes = await fetch(`/api/settings/connections?userId=${userIdToUse}`);
      if (connRes.ok) {
        const conn = await connRes.json();
        setOllamaApiUrl(conn.ollamaApiUrl || 'http://localhost:11434');
        setLmStudioApiUrl(conn.lmStudioApiUrl || 'http://localhost:1234');
        setOpenAiApiUrl(conn.openAiApiUrl || 'https://api.openai.com');
        setOpenAiApiKey(conn.openAiApiKey || '');
        setClaudeApiUrl(conn.claudeApiUrl || 'https://api.anthropic.com');
        setClaudeApiKey(conn.claudeApiKey || '');
        setEnableStreaming(conn.enableStreaming !== false);
      } else {
        setOllamaApiUrl('http://localhost:11434');
        setLmStudioApiUrl('http://localhost:1234');
        setOpenAiApiUrl('https://api.openai.com');
        setOpenAiApiKey('');
        setClaudeApiUrl('https://api.anthropic.com');
        setClaudeApiKey('');
        setEnableStreaming(true);
      }
    } catch {
      setSaveError('Failed to load settings. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const saveSettings = async () => {
    setLoading(true);
    setSaveSuccess(false);
    setSaveError('');
    try {
      if (!user?.id) {
        setSaveError('Cannot save settings: No valid user ID.');
        return;
      }
      await chatService.updateLlmSettings(selectedEngine, selectedModel, user.id);

      await fetch('/api/settings/connections', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          UserId: user.id,
          OllamaApiUrl: ollamaApiUrl || '',
          LmStudioApiUrl: lmStudioApiUrl || '',
          OpenAiApiUrl: openAiApiUrl || '',
          OpenAiApiKey: openAiApiKey || '',
          ClaudeApiUrl: claudeApiUrl || '',
          ClaudeApiKey: claudeApiKey || '',
          Neo4jUri: '',
          EnableStreaming: enableStreaming,
        }),
      });

      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 5000);
    } catch {
      setSaveError('Failed to save settings. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">AI Model Settings</h2>
      {loading && <div className="text-sm text-gray-500">Loading settings...</div>}

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            LLM Engine
            <Tooltip text="Choose which LLM engine to use.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <select
            className="w-full border rounded px-3 py-2"
            value={selectedEngine}
            onChange={e => {
              setSelectedEngine(e.target.value);
              setSettingsLoaded(true);
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
                <Tooltip text="URL of your Ollama server (default http://localhost:11434).">
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
                      const res = await fetch(`${ollamaApiUrl}/api/tags`);
                      if (res.ok) {
                        const data = await res.json();
                        alert(
                          data.models && data.models.length > 0
                            ? `Connected! Models: ${data.models.map((m: any) => m.name).join(', ')}`
                            : 'Connected, but no models found.'
                        );
                      } else {
                        alert(`Connection failed: ${res.status} ${res.statusText}`);
                      }
                    } catch (err) {
                      alert(`Connection failed: ${(err as Error).message}`);
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
              <label className="block text-sm font-medium text-gray-700 mb-1">Ollama Model</label>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={e => setSelectedModel(e.target.value)}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {ollamaModels.map(model => (
                  <option key={model} value={model}>
                    {model}
                  </option>
                ))}
              </select>
            </div>
          </>
        )}

        {selectedEngine === 'lmstudio' && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
              LM Studio API URL
              <Tooltip text="URL of your LM Studio server (default http://localhost:1234).">
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
                    const res = await fetch(`${lmStudioApiUrl}/v1/models`);
                    if (res.ok) {
                      const data = await res.json();
                      alert(
                        data.data && data.data.length > 0
                          ? `Connected! Model: ${data.data[0].id}`
                          : 'Connected, but no models found.'
                      );
                    } else {
                      alert(`Connection failed: ${res.status} ${res.statusText}`);
                    }
                  } catch (err) {
                    alert(`Connection failed: ${(err as Error).message}`);
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
            <label className="block text-sm font-medium text-gray-700 mb-1">OpenAI API URL</label>
            <input
              type="text"
              placeholder="https://api.openai.com"
              value={openAiApiUrl}
              onChange={e => setOpenAiApiUrl(e.target.value)}
              className="w-full border rounded px-3 py-2 mb-2"
              disabled={loading}
            />
            <label className="block text-sm font-medium text-gray-700 mb-1">OpenAI API Key</label>
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
            <label className="block text-sm font-medium text-gray-700 mb-1">Claude API URL</label>
            <input
              type="text"
              placeholder="https://api.anthropic.com"
              value={claudeApiUrl}
              onChange={e => setClaudeApiUrl(e.target.value)}
              className="w-full border rounded px-3 py-2 mb-2"
              disabled={loading}
            />
            <label className="block text-sm font-medium text-gray-700 mb-1">Claude API Key</label>
            <input
              type="password"
              value={claudeApiKey}
              onChange={e => setClaudeApiKey(e.target.value)}
              className="w-full border rounded px-3 py-2"
              disabled={loading}
            />
          </div>
        )}

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
            <Tooltip text="When enabled, responses stream in real time. Otherwise, you receive the full response at once.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <p className="text-xs text-gray-500 mt-1">
            {enableStreaming
              ? "Responses appear word-by-word as they're generated."
              : "You'll receive the complete response at once."}
          </p>
        </div>
      </div>

      <div className="pt-4 flex justify-between items-center">
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
  const [cardFile, setCardFile] = useState<File | null>(null);
  const [avatars, setAvatars] = useState<Avatar[]>([]);
  const [activeAvatar, setActiveAvatar] = useState('');
  const [characters, setCharacters] = useState<Character[]>([]);
  const [showEditor, setShowEditor] = useState(false);
  const [editingCharacter, setEditingCharacter] = useState<string>();
  const defaultUserId = '00000000-0000-0000-0000-000000000001';
  const [userId, setUserId] = useState(defaultUserId);

  useEffect(() => {
    setAvatars([{ id: '1', type: '2D', thumbnailPath: '/default-avatar.png' }]);
    loadCharacters(defaultUserId);
  }, []);

  const loadCharacters = async (idToUse: string) => {
    if (!idToUse) return;
    try {
      const response = await fetch(`/api/character/user/${idToUse}`);
      if (response.ok) {
        const data = await response.json();
        setCharacters(data);
      }
    } catch {
      // ignore
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
    if (!confirm('Are you sure you want to delete this character?')) return;
    try {
      const response = await fetch(`/api/character/${characterId}`, { method: 'DELETE' });
      if (response.ok) loadCharacters(defaultUserId);
    } catch {
      // ignore
    }
  };

  const handleSaveCharacter = () => {
    setShowEditor(false);
    setEditingCharacter(undefined);
    loadCharacters(defaultUserId);
  };

  const CharacterModal = () => {
    if (!showEditor) return null;
    useEffect(() => {
      const handleEscape = (e: KeyboardEvent) => {
        if (e.key === 'Escape') setShowEditor(false);
      };
      document.addEventListener('keydown', handleEscape);
      return () => document.removeEventListener('keydown', handleEscape);
    }, []);

    return (
      <div
        className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
        onClick={e => e.target === e.currentTarget && setShowEditor(false)}
      >
        <div className="bg-white rounded-lg shadow-xl max-w-5xl w-full max-h-[90vh] overflow-hidden">
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
        <button onClick={handleCreateCharacter} className="btn btn-primary flex items-center">
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
            let characterName = 'Unnamed Character';
            try {
              const parsed = yaml.load(character.yamlProfile) as any;
              characterName = parsed?.name || 'Unnamed Character';
            } catch {
              // ignore
            }
            return (
              <div
                key={character.id}
                className="border rounded-lg p-4 bg-white hover:shadow-md transition-shadow"
              >
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
                        let systemPrompt = '';
                        try {
                          const parsed = yaml.load(character.yamlProfile) as any;
                          systemPrompt = `You are roleplaying as the AI character below. Remain fully in character.

Name: ${parsed.name || 'Unknown Character'}
Description: ${parsed.description || ''}
Personality: ${parsed.personality || ''}
Scenario: ${parsed.scenario || ''}
System Prompt: ${parsed.system_prompt || ''}

Respond using the character's voice and personality at all times.`;
                        } catch {
                          // ignore
                        }
                        localStorage.setItem('selectedCharacterId', character.id);
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
        <div className="flex items-center space-x-2">
          <input
            type="file"
            accept=".json"
            onChange={e => setCardFile(e.target.files?.[0] || null)}
          />
          <button disabled={!cardFile} onClick={() => {}} className="btn btn-ghost border text-sm flex items-center">
            <Upload size={14} className="mr-1" />
            Import
          </button>
        </div>
      </div>

      <div>
        <h3 className="text-lg font-medium mb-4">Avatar Settings</h3>
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
    setAgents(curr => curr.map(a => (a.id === id ? { ...a, enabled: !a.enabled } : a)));
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">Connected Agents</h2>
      <p className="text-sm text-gray-600">These background agents manage tasks and quality control.</p>
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
                a.enabled ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'
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
      <h2 className="text-xl font-medium text-gray-800">Invocation Settings</h2>
      <p className="text-sm text-gray-600">Configure how you interact with your AI assistant.</p>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            AI Name
            <Tooltip text="Set the name for your AI assistant.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <input type="text" className="w-full border rounded px-3 py-2" />
          <p className="mt-1 text-xs text-gray-500">This is how you'll refer to your AI assistant.</p>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            Wake Word
            <Tooltip text="Set the phrase to activate voice recognition.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <input type="text" className="w-full border rounded px-3 py-2" />
          <p className="mt-1 text-xs text-gray-500">Say this phrase to activate voice recognition.</p>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            Wake Word Sensitivity
            <Tooltip text="Adjust sensitivity for detecting the wake word.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <input type="range" min="1" max="10" className="w-full" />
          <div className="flex justify-between text-xs text-gray-500 mt-1">
            <span>Less Sensitive</span>
            <span>More Sensitive</span>
          </div>
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

const AgentStackSettings = () => {
  const defaultUserId = '00000000-0000-0000-0000-000000000001';
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
    setLoading(true);
    try {
      const res = await fetch(`/api/settings?userId=${defaultUserId}`);
      if (res.ok) {
        const settings = await res.json();
        setAgentStackUrl(settings.AgentStackUrl || 'http://localhost:8080');
        setAgentStackApiKey(settings.AgentStackApiKey || '');
      }
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  };

  const testConnection = async () => {
    setLoading(true);
    try {
      const res = await fetch(`${agentStackUrl}/health`);
      setConnected(res.ok);
      alert(res.ok ? 'Connection successful!' : 'Connection failed. Check URL.');
    } catch {
      setConnected(false);
      alert('Connection failed. Check URL.');
    } finally {
      setLoading(false);
    }
  };

  const saveAgentStackSettings = async () => {
    setLoading(true);
    setSaveSuccess(false);
    setSaveError('');
    try {
      const response = await fetch('/api/settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          UserId: defaultUserId,
          Settings: {
            AgentStackUrl: agentStackUrl,
            AgentStackApiKey: agentStackApiKey,
          },
        }),
      });
      if (response.ok) {
        setSaveSuccess(true);
        setTimeout(() => setSaveSuccess(false), 3000);
      } else {
        setSaveError('Failed to save settings. Please try again.');
      }
    } catch {
      setSaveError('Failed to save settings. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium text-gray-800">Agent Stack Connection</h2>
      <p className="text-sm text-gray-600">Connect to an external agent stack for advanced API access.</p>

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
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">Agent Stack URL</label>
          <div className="flex space-x-2">
            <input
              type="text"
              value={agentStackUrl}
              onChange={e => setAgentStackUrl(e.target.value)}
              placeholder="http://localhost:8080"
              className="flex-grow border rounded px-3 py-2"
              disabled={loading}
            />
            <button onClick={testConnection} className="btn btn-primary" disabled={loading}>
              Test Connection
            </button>
          </div>
          <p className="text-xs text-gray-500 mt-1">URL of the external agent stack server.</p>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">API Key (Optional)</label>
          <input
            type="password"
            value={agentStackApiKey}
            onChange={e => setAgentStackApiKey(e.target.value)}
            placeholder="Enter API key if required"
            className="w-full border rounded px-3 py-2"
            disabled={loading}
          />
          <p className="text-xs text-gray-500 mt-1">API key for authentication (if required).</p>
        </div>

        <div className="bg-gray-50 border border-gray-200 rounded-md p-4">
          <div className="flex items-center space-x-2">
            <div className={`w-3 h-3 rounded-full ${connected ? 'bg-green-500' : 'bg-red-500'}`}></div>
            <span className="text-sm font-medium text-gray-700">
              Status: {connected ? 'Connected' : 'Disconnected'}
            </span>
          </div>
          <p className="text-xs text-gray-500 mt-1">
            {connected
              ? 'Successfully connected to the agent stack.'
              : 'Not connected to the agent stack.'}
          </p>
        </div>

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
  const defaultUserId = '00000000-0000-0000-0000-000000000001';
  const [theme, setTheme] = useState('dark');
  const [language, setLanguage] = useState('en');
  const [accentColor, setAccentColor] = useState('#8B5CF6');
  const [fontSize, setFontSize] = useState('medium');
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');

  const t = (key: string) => {
    const translations: Record<string, string> = {
      'appearance.title': 'Appearance Settings',
      'appearance.subtitle': 'Customize the look and feel of your AI assistant',
      'appearance.theme': 'Theme',
      'appearance.light': 'Light',
      'appearance.dark': 'Dark',
      'appearance.system': 'System',
      'appearance.language': 'Language',
      'appearance.selectLanguage': 'Select your preferred language',
      'appearance.accentColor': 'Accent Color',
      'appearance.fontSize': 'Font Size',
      'appearance.adjustFontSize': 'Adjust text size throughout the application',
      'appearance.small': 'Small',
      'appearance.medium': 'Medium',
      'appearance.large': 'Large',
      'settings.saveChanges': 'Save Changes',
      'settings.saving': 'Saving...',
      'settings.settingsSaved': 'Appearance settings saved successfully!',
      'settings.failedToSave': 'Failed to save settings. Please try again.',
    };
    return translations[key] || key;
  };

  useEffect(() => {
    loadAppearanceSettings();
  }, []);

  const loadAppearanceSettings = async () => {
    setLoading(true);
    try {
      const res = await fetch(`/api/settings?userId=${defaultUserId}`);
      if (res.ok) {
        const settings = await res.json();
        setTheme(settings.Theme || 'dark');
        setLanguage(settings.Language || 'en');
        setAccentColor(settings.AccentColor || '#8B5CF6');
        setFontSize(settings.FontSize || 'medium');
      }
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  };

  const saveAppearanceSettings = async () => {
    setLoading(true);
    setSaveSuccess(false);
    setSaveError('');
    try {
      if (typeof window !== 'undefined' && (window as any).i18n) {
        try {
          await (window as any).i18n.changeLanguage(language);
        } catch {
          // ignore
        }
      }

      const response = await fetch('/api/settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          UserId: defaultUserId,
          Settings: {
            Theme: theme,
            Language: language,
            AccentColor: accentColor,
            FontSize: fontSize,
          },
        }),
      });
      if (response.ok) {
        setSaveSuccess(true);
        setTimeout(() => setSaveSuccess(false), 3000);
      } else {
        setSaveError(t('settings.failedToSave'));
      }
    } catch {
      setSaveError(t('settings.failedToSave'));
    } finally {
      setLoading(false);
    }
  };

  const themes = [
    { id: 'light', name: t('appearance.light'), preview: 'bg-white border-gray-200' },
    { id: 'dark', name: t('appearance.dark'), preview: 'bg-gray-800' },
    { id: 'system', name: t('appearance.system'), preview: 'bg-gradient-to-b from-white to-gray-800' },
  ];

  const accentColors = [
    { id: '#8B5CF6', name: 'Purple', color: 'bg-purple-500' },
    { id: '#3B82F6', name: 'Blue', color: 'bg-blue-500' },
    { id: '#10B981', name: 'Green', color: 'bg-green-500' },
    { id: '#F59E0B', name: 'Orange', color: 'bg-orange-500' },
    { id: '#EF4444', name: 'Red', color: 'bg-red-500' },
  ];

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium text-gray-800 mb-4">{t('appearance.title')}</h2>
      <p className="text-sm text-gray-600 mb-6">{t('appearance.subtitle')}</p>

      {saveSuccess && (
        <div className="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded-md">
          {t('settings.settingsSaved')}
        </div>
      )}
      {saveError && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {saveError}
        </div>
      )}

      <div className="space-y-6">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-3">{t('appearance.theme')}</label>
          <div className="grid grid-cols-3 gap-3">
            {themes.map(opt => (
              <button
                key={opt.id}
                onClick={() => setTheme(opt.id)}
                className={`flex flex-col items-center p-3 border rounded-md transition-all ${
                  theme === opt.id ? 'border-primary-500 bg-primary-50 shadow-sm' : 'border-gray-300 bg-white hover:border-gray-400'
                }`}
                disabled={loading}
              >
                <div className={`w-full h-16 ${opt.preview} border border-gray-200 rounded-md mb-2`}></div>
                <span className="text-xs font-medium">{opt.name}</span>
              </button>
            ))}
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">{t('appearance.language')}</label>
          <select
            value={language}
            onChange={e => setLanguage(e.target.value)}
            className="w-full border rounded px-3 py-2"
            disabled={loading}
          >
            <option value="en">English</option>
            <option value="ja">日本語</option>
          </select>
          <p className="text-xs text-gray-500 mt-1">{t('appearance.selectLanguage')}</p>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-3">{t('appearance.accentColor')}</label>
          <div className="grid grid-cols-5 gap-3">
            {accentColors.map(colorOption => (
              <button
                key={colorOption.id}
                onClick={() => setAccentColor(colorOption.id)}
                className={`w-full h-12 ${colorOption.color} rounded-md border-2 transition-all ${
                  accentColor === colorOption.id ? 'border-gray-800 scale-105' : 'border-gray-300 hover:border-gray-400'
                }`}
                disabled={loading}
                title={colorOption.name}
              />
            ))}
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">{t('appearance.fontSize')}</label>
          <select
            value={fontSize}
            onChange={e => setFontSize(e.target.value)}
            className="w-full border rounded px-3 py-2"
            disabled={loading}
          >
            <option value="small">{t('appearance.small')}</option>
            <option value="medium">{t('appearance.medium')}</option>
            <option value="large">{t('appearance.large')}</option>
          </select>
          <p className="text-xs text-gray-500 mt-1">{t('appearance.adjustFontSize')}</p>
        </div>
      </div>

      <div className="pt-4 flex justify-end">
        <button
          className="btn btn-primary flex items-center"
          onClick={saveAppearanceSettings}
          disabled={loading}
        >
          {loading ? <span className="loader mr-2"></span> : <Save size={16} className="mr-1.5" />}
          {loading ? t('settings.saving') : t('settings.saveChanges')}
        </button>
      </div>
    </div>
  );
};

const NetworkSettings = () => {
  const [federation, setFederation] = useState(false);
  const [serverUrl, setServerUrl] = useState('');
  const [offlineMode, setOfflineMode] = useState(false);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-medium text-gray-800 mb-4">Network Settings</h2>
        <p className="text-sm text-gray-600 mb-6">Configure network and connection options.</p>
      </div>

      <div className="space-y-4">
        <div className="flex items-center">
          <input
            id="federation"
            type="checkbox"
            checked={federation}
            onChange={e => setFederation(e.target.checked)}
            className="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
          />
          <label htmlFor="federation" className="ml-2 block text-sm text-gray-700 flex items-center">
            Enable Federation
            <Tooltip text="Allow communication with other instances.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
        </div>
        <p className="text-xs text-gray-500">
          Federation allows your AI to share select memories and capabilities with other instances.
        </p>

        <div>
          <label htmlFor="server-url" className="block text-sm font-medium text-gray-700 mb-1 flex items-center">
            Federation Server
            <Tooltip text="Enter the URL of the federation server.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
          <input
            id="server-url"
            type="text"
            className="w-full px-3 py-2 border border-gray-300 rounded-md"
            placeholder="Enter server URL"
            value={serverUrl}
            onChange={e => setServerUrl(e.target.value)}
          />
        </div>

        <div className="flex items-center">
          <input
            id="offline-mode"
            type="checkbox"
            checked={offlineMode}
            onChange={e => setOfflineMode(e.target.checked)}
            className="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
          />
          <label htmlFor="offline-mode" className="ml-2 block text-sm text-gray-700 flex items-center">
            Offline Mode
            <Tooltip text="Use only local models; no network requests.">
              <span className="ml-1 text-gray-400 cursor-help">&#9432;</span>
            </Tooltip>
          </label>
        </div>
        <p className="text-xs text-gray-500">
          In offline mode, your AI will only use local models and avoid network calls.
        </p>
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

export default SettingsPage;