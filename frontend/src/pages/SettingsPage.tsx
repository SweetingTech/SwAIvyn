
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
import ttsService, { VoiceDetails, UploadVoiceRequest } from '../services/ttsService';
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
  'claude-opus-4-20250514',      // Claude 4 - Most capable model
  'claude-sonnet-4-20250514',    // Claude 4 - High-performance model
  'claude-3-7-sonnet-20250219',  // Claude 3.7 - Extended thinking capabilities
  'claude-3-5-sonnet-20241022',  // Claude 3.5 Sonnet v2
  'claude-3-5-haiku-20241022',   // Claude 3.5 Haiku
  'claude-3-opus-20240229',      // Claude 3 Opus (legacy)
  'claude-3-sonnet-20240229',    // Claude 3 Sonnet (legacy)
  'claude-3-haiku-20240307',     // Claude 3 Haiku (legacy)
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
  const [voiceId, setVoiceId] = useState('Rachel');
  const [ttsProvider, setTtsProvider] = useState('elevenlabs');
  const [providers, setProviders] = useState<any[]>([]);
  const [availableVoices, setAvailableVoices] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  
  // Voice management states for Fish Speech
  const [showUploadModal, setShowUploadModal] = useState(false);
  const [uploadData, setUploadData] = useState({
    voiceName: '',
    transcript: '',
    audioFile: null as File | null,
  });
  const [uploadError, setUploadError] = useState('');
  const [uploadLoading, setUploadLoading] = useState(false);
  const [voiceDetails, setVoiceDetails] = useState<VoiceDetails[]>([]);
  const [showVoiceDetails, setShowVoiceDetails] = useState<string | null>(null);

  useEffect(() => {
    if (!user?.id) return;
    setLoading(true);
    ttsService.getSettings(user.id)
      .then(result => {
        setApiKey(result.apiKey || '');
        setVoiceId(result.voiceId || 'Rachel');
        setTtsProvider(result.ttsProvider || 'elevenlabs');
        setProviders(result.providers || []);
        
        // Load voices for current provider
        const currentProvider = result.ttsProvider || 'elevenlabs';
        loadVoices(currentProvider);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [user?.id]);

  const loadVoices = async (provider: string) => {
    try {
      const voices = await ttsService.getVoices(provider, user?.id);
      setAvailableVoices(voices);
      
      // If Fish Speech, also load voice details
      if (provider === 'fishspeech') {
        loadVoiceDetails(voices);
      }
    } catch (error) {
      console.error('Failed to load voices:', error);
      // Fallback to default voices based on provider
      if (provider === 'elevenlabs') {
        setAvailableVoices(['Rachel', 'Domi', 'Bella', 'Antoni']);
      } else {
        setAvailableVoices(['default']);
      }
    }
  };

  const loadVoiceDetails = async (voices: string[]) => {
    try {
      const details = await Promise.all(
        voices.map(async (voice) => {
          try {
            return await ttsService.getVoiceDetails(voice);
          } catch {
            return null;
          }
        })
      );
      setVoiceDetails(details.filter(Boolean) as VoiceDetails[]);
    } catch (error) {
      console.error('Failed to load voice details:', error);
    }
  };

  const handleProviderChange = (newProvider: string) => {
    setTtsProvider(newProvider);
    loadVoices(newProvider);
    // Reset voice to first available when switching providers
    setVoiceId('');
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const validation = ttsService.validateVoiceFile(file);
    if (!validation.valid) {
      setUploadError(validation.error || 'Invalid file');
      return;
    }

    setUploadData(prev => ({ ...prev, audioFile: file }));
    setUploadError('');
  };

  const handleUploadVoice = async () => {
    if (!uploadData.audioFile || !uploadData.voiceName || !uploadData.transcript) {
      setUploadError('Please fill in all fields and select an audio file.');
      return;
    }

    setUploadLoading(true);
    setUploadError('');

    try {
      await ttsService.uploadVoice({
        audioFile: uploadData.audioFile,
        transcript: uploadData.transcript,
        voiceName: uploadData.voiceName,
      });

      // Reset form and refresh voices
      setUploadData({ voiceName: '', transcript: '', audioFile: null });
      setShowUploadModal(false);
      loadVoices(ttsProvider);
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Failed to upload voice');
    } finally {
      setUploadLoading(false);
    }
  };

  const handleDeleteVoice = async (voiceName: string) => {
    if (!confirm(`Are you sure you want to delete the voice "${voiceName}"? This action cannot be undone.`)) {
      return;
    }

    try {
      await ttsService.deleteVoice(voiceName);
      loadVoices(ttsProvider);
    } catch (error) {
      alert(`Failed to delete voice: ${error instanceof Error ? error.message : 'Unknown error'}`);
    }
  };

  const save = () => {
    if (!user?.id) return;
    setLoading(true);
    
    const settings = {
      userId: user.id,
      apiKey: apiKey || undefined,
      voiceId: voiceId || undefined,
      ttsProvider: ttsProvider || undefined
    };

    ttsService.updateSettings(settings)
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
      <p className="text-sm text-gray-600">Configure text-to-speech settings for your AI assistant.</p>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            TTS Provider
          </label>
          <select
            className="w-full border rounded px-3 py-2"
            value={ttsProvider}
            onChange={e => handleProviderChange(e.target.value)}
            disabled={loading}
          >
            {providers.map(provider => (
              <option key={provider.id} value={provider.id} disabled={!provider.available}>
                {provider.name} {!provider.available ? '(Unavailable)' : ''}
              </option>
            ))}
          </select>
        </div>

        {ttsProvider === 'elevenlabs' && (
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
              placeholder="Enter your ElevenLabs API key"
            />
          </div>
        )}

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Voice</label>
          <select
            className="w-full border rounded px-3 py-2"
            value={voiceId}
            onChange={e => setVoiceId(e.target.value)}
            disabled={loading || availableVoices.length === 0}
          >
            {availableVoices.length === 0 ? (
              <option value="">Loading voices...</option>
            ) : (
              availableVoices.map(voice => (
                <option key={voice} value={voice}>
                  {voice}
                </option>
              ))
            )}
          </select>
        </div>

        {ttsProvider === 'fishspeech' && (
          <div className="space-y-4">
            <div className="bg-blue-50 p-3 rounded">
              <p className="text-sm text-blue-700">
                Fish Speech TTS provides high-quality voice synthesis. You can upload custom voices or use existing ones.
              </p>
            </div>

            {/* Voice Management Section */}
            <div className="border rounded-lg p-4">
              <div className="flex items-center justify-between mb-3">
                <h3 className="text-lg font-medium">Voice Management</h3>
                <button
                  onClick={() => setShowUploadModal(true)}
                  className="flex items-center space-x-2 px-3 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
                >
                  <Upload size={16} />
                  <span>Upload Voice</span>
                </button>
              </div>

              {/* Voice List */}
              <div className="space-y-2">
                {voiceDetails.map((voice) => (
                  <div key={voice.name} className="border rounded p-3 bg-gray-50">
                    <div className="flex items-center justify-between">
                      <div className="flex-1">
                        <h4 className="font-medium">{voice.name}</h4>
                        <p className="text-sm text-gray-600 truncate">
                          {voice.transcript?.substring(0, 100)}
                          {voice.transcript?.length > 100 ? '...' : ''}
                        </p>
                        <div className="flex items-center space-x-4 text-xs text-gray-500 mt-1">
                          <span>Audio: {voice.hasAudioFile ? '✓' : '✗'}</span>
                          <span>Embedding: {voice.hasEmbedding ? '✓' : '✗'}</span>
                          {voice.audioFileSize && (
                            <span>Size: {(voice.audioFileSize / 1024 / 1024).toFixed(1)}MB</span>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <button
                          onClick={() => setShowVoiceDetails(showVoiceDetails === voice.name ? null : voice.name)}
                          className="p-2 text-gray-500 hover:text-gray-700"
                        >
                          <Edit size={16} />
                        </button>
                        <button
                          onClick={() => handleDeleteVoice(voice.name)}
                          className="p-2 text-red-500 hover:text-red-700"
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                    </div>
                    
                    {showVoiceDetails === voice.name && (
                      <div className="mt-3 pt-3 border-t bg-white p-3 rounded">
                        <h5 className="font-medium mb-2">Voice Details</h5>
                        <p className="text-sm text-gray-700 mb-2">
                          <strong>Transcript:</strong> {voice.transcript}
                        </p>
                        {voice.createdAt && (
                          <p className="text-sm text-gray-500">
                            Created: {new Date(voice.createdAt).toLocaleString()}
                          </p>
                        )}
                      </div>
                    )}
                  </div>
                ))}
                
                {voiceDetails.length === 0 && (
                  <p className="text-gray-500 text-center py-4">
                    No custom voices uploaded yet. Click "Upload Voice" to add your first voice.
                  </p>
                )}
              </div>
            </div>
          </div>
        )}
      </div>

      <div className="pt-4 flex justify-end">
        <button className="btn btn-primary" onClick={save} disabled={loading}>
          {loading ? 'Saving...' : 'Save Changes'}
        </button>
        {saveSuccess && <div className="text-green-600 text-sm ml-4">Saved!</div>}
      </div>

      {/* Upload Modal */}
      {showUploadModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 w-full max-w-md">
            <h3 className="text-lg font-medium mb-4">Upload New Voice</h3>
            
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Voice Name
                </label>
                <input
                  type="text"
                  className="w-full border rounded px-3 py-2"
                  value={uploadData.voiceName}
                  onChange={e => setUploadData(prev => ({ ...prev, voiceName: e.target.value }))}
                  placeholder="Enter a unique voice name"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Audio File
                </label>
                <input
                  type="file"
                  accept="audio/wav,audio/mp3,audio/mpeg"
                  onChange={handleFileUpload}
                  className="w-full border rounded px-3 py-2"
                />
                <p className="text-xs text-gray-500 mt-1">
                  Supported formats: WAV, MP3. Max size: 50MB.
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Transcript
                </label>
                <textarea
                  className="w-full border rounded px-3 py-2 h-24"
                  value={uploadData.transcript}
                  onChange={e => setUploadData(prev => ({ ...prev, transcript: e.target.value }))}
                  placeholder="Enter the text that matches the audio file"
                />
                <p className="text-xs text-gray-500 mt-1">
                  This should match exactly what is spoken in the audio file.
                </p>
              </div>

              {uploadError && (
                <div className="text-red-600 text-sm bg-red-50 p-2 rounded">
                  {uploadError}
                </div>
              )}
            </div>

            <div className="flex justify-end space-x-3 mt-6">
              <button
                onClick={() => {
                  setShowUploadModal(false);
                  setUploadData({ voiceName: '', transcript: '', audioFile: null });
                  setUploadError('');
                }}
                className="px-4 py-2 text-gray-600 hover:text-gray-800"
                disabled={uploadLoading}
              >
                Cancel
              </button>
              <button
                onClick={handleUploadVoice}
                disabled={uploadLoading || !uploadData.audioFile || !uploadData.voiceName || !uploadData.transcript}
                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
              >
                {uploadLoading ? 'Uploading...' : 'Upload Voice'}
              </button>
            </div>
          </div>
        </div>
      )}
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
