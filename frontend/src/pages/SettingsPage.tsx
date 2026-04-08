import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
  Edit,
  Trash2,
  Bot,
  Smartphone,
  Bell,
  BellOff,
} from 'lucide-react';
import usePushNotifications from '../hooks/usePushNotifications';
import chatService from '../services/chatService';
import ttsService, { VoiceDetails } from '../services/ttsService';
import { useInitialization } from '../contexts/InitializationContext';
import { useAuth } from '../contexts/AuthContext';
import fetchWithTimeout from '../utils/fetchWithTimeout';
import useEffectiveUser from '../hooks/useEffectiveUser';

// ----------------------------- helpers ---------------------------------

const useMountedRef = () => {
  const mounted = useRef(true);
  const eff = useEffectiveUser();

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  return mounted;
};

type Provider = { id: string; name: string; available: boolean };

// ----------------------------- static lists ----------------------------

const OPENAI_MODELS = [
  'gpt-4o-mini',
  'gpt-4o',
  'gpt-4-turbo',
  'gpt-4',
  'gpt-3.5-turbo',
];

const CLAUDE_MODELS = [
  'claude-opus-4-20250514',
  'claude-sonnet-4-20250514',
  'claude-3-7-sonnet-20250219',
  'claude-3-5-sonnet-20241022',
  'claude-3-5-haiku-20241022',
  'claude-3-opus-20240229',
  'claude-3-sonnet-20240229',
  'claude-3-haiku-20240307',
];

const tabs = [
  { id: 'invocation', label: 'Invocation', icon: <Speech size={16} /> },
  { id: 'model', label: 'AI Model', icon: <Database size={16} /> },
  { id: 'voice', label: 'Voice', icon: <Volume2 size={16} /> },
  { id: 'character', label: 'Character', icon: <ImageIcon size={16} /> },
  { id: 'character-create', label: 'Create Character', icon: <User size={16} /> },
  { id: 'agents', label: 'Connections', icon: <ServerCog size={16} /> },
  { id: 'agentstack', label: 'Agent Stack', icon: <Bot size={16} /> },
  { id: 'external-agents', label: 'External Agents', icon: <Bot size={16} /> },
  { id: 'appearance', label: 'Appearance', icon: <Palette size={16} /> },
  { id: 'network', label: 'Network', icon: <Network size={16} /> },
  { id: 'mobile', label: 'Mobile & Notifications', icon: <Smartphone size={16} /> },
];

// ============================ Character Creation Settings ===========================

const CharacterCreateSettings = () => {
  return (
    <div className="space-y-6">
      <div className="border-b border-gray-200 pb-4">
        <h3 className="text-lg font-medium text-gray-900">Create New Character</h3>
        <p className="text-sm text-gray-600 mt-1">
          Design custom AI personalities with unique traits and behaviors. Create characters from templates or build from scratch.
        </p>
      </div>

      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <div className="flex items-center">
          <User className="text-blue-600 mr-3" size={20} />
          <div>
            <h4 className="text-blue-900 font-medium">Character Creation Available</h4>
            <p className="text-blue-700 text-sm">
              Go to the Character Editor page to create new characters with templates like Helper, Writer, Teacher, and more.
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="bg-white border border-gray-200 rounded-lg p-4">
          <h4 className="font-medium text-gray-900 mb-2">Available Templates</h4>
          <ul className="text-sm text-gray-600 space-y-1">
            <li>• Helpful Assistant</li>
            <li>• Creative Writer</li>
            <li>• Technical Expert</li>
            <li>• Patient Teacher</li>
            <li>• Data Analyst</li>
            <li>• GLaDOS (Sarcastic AI)</li>
          </ul>
        </div>

        <div className="bg-white border border-gray-200 rounded-lg p-4">
          <h4 className="font-medium text-gray-900 mb-2">Features</h4>
          <ul className="text-sm text-gray-600 space-y-1">
            <li>• Custom system prompts</li>
            <li>• Personality traits</li>
            <li>• Avatar images</li>
            <li>• Shared or private characters</li>
            <li>• YAML import support</li>
          </ul>
        </div>
      </div>
    </div>
  );
};

// ============================ External Agents Settings ===========================

const ExternalAgentsSettings = () => {
  return (
    <div className="space-y-6">
      <div className="border-b border-gray-200 pb-4">
        <h3 className="text-lg font-medium text-gray-900">External Agents</h3>
        <p className="text-sm text-gray-600 mt-1">
          Manage external agent services that can process tasks and provide specialized capabilities.
        </p>
      </div>

      <div className="bg-green-50 border border-green-200 rounded-lg p-4">
        <div className="flex items-center">
          <Bot className="text-green-600 mr-3" size={20} />
          <div>
            <h4 className="text-green-900 font-medium">External Agent System Ready</h4>
            <p className="text-green-700 text-sm">
              The secure multi-tenant agent system is configured and ready for external agent registration.
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="bg-white border border-gray-200 rounded-lg p-4">
          <h4 className="font-medium text-gray-900 mb-2">Security Features</h4>
          <ul className="text-sm text-gray-600 space-y-1">
            <li>• Admin-only agent registration</li>
            <li>• Encrypted API keys</li>
            <li>• User data isolation</li>
            <li>• Task authorization</li>
            <li>• Audit logging</li>
          </ul>
        </div>

        <div className="bg-white border border-gray-200 rounded-lg p-4">
          <h4 className="font-medium text-gray-900 mb-2">Agent Capabilities</h4>
          <ul className="text-sm text-gray-600 space-y-1">
            <li>• Task processing</li>
            <li>• File generation</li>
            <li>• Data analysis</li>
            <li>• Progress tracking</li>
            <li>• Result storage</li>
          </ul>
        </div>
      </div>
    </div>
  );
};

// ============================ Voice Settings ===========================

const VoiceSettings = () => {
  const { user: initUser } = useInitialization();
  const { user: authUser, token } = useAuth();
  const mounted = useMountedRef();
  const eff = useEffectiveUser();

  // Use effective user ID with fallback like Chat page
  const voiceUserId = eff.userId || 'admin';
  const user = { id: voiceUserId };

  const [apiKey, setApiKey] = useState('');
  const [voiceId, setVoiceId] = useState('');
  const [fishSpeechApiKey, setFishSpeechApiKey] = useState('');
  const [ttsProvider, setTtsProvider] = useState('fishspeech');
  const [providers, setProviders] = useState<Provider[]>([
    { id: 'fishspeech', name: 'Fish Speech', available: true },
  ]);
  const [availableVoices, setAvailableVoices] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [effectiveUserId, setEffectiveUserId] = useState<string | null>(null);

  // Fish Speech management
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

  const [ttsHealth, setTtsHealth] = useState<{ status: string; upstream?: string; upstreams?: { url: string; status: string }[] } | null>(null);

  // Resolve effective user id like UserProfilePage does
  useEffect(() => {
    const uid = authUser?.id || user?.id || null;
    if (uid) { setEffectiveUserId(uid); return; }
    // fallback: try /api/auth/me if token exists
    (async () => {
      try {
        const resp = await fetch('/api/auth/me', { credentials: 'include' });
        if (resp.ok) {
          const me = await resp.json();
          if (me?.id) setEffectiveUserId(me.id);
        }
      } catch {}
    })();
  }, [authUser?.id, user?.id, token]);

  // load settings once we have an effective user id (or proceed without for voices)
  useEffect(() => {
    setLoading(true);
    ttsService
      .getSettings(effectiveUserId || undefined)
      .then((result) => {
        if (!mounted.current) return;
        setApiKey(result.apiKey || '');
        setVoiceId(result.voiceId || '');
        setFishSpeechApiKey(result.fishSpeechApiKey || '');
        const nextProvider = result.ttsProvider || 'fishspeech';
        setTtsProvider(nextProvider);
        const provs = (Array.isArray(result.providers) ? (result.providers as Provider[]) : [])
          .filter(Boolean);
        setProviders(provs.length ? provs : [{ id: 'fishspeech', name: 'Fish Speech', available: true }]);
        // load voices for current provider
        return loadVoices(nextProvider, result.voiceId || '');
      })
      .catch(() => {
        if (!mounted.current) return;
        // make sure UI stays usable even if provider list failed
        setProviders((prev) => prev.length ? prev : [
          { id: 'fishspeech', name: 'Fish Speech', available: true },
        ]);
      })
      .finally(() => mounted.current && setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [effectiveUserId]);

  const loadVoiceDetails = useCallback(async (voices: string[]) => {
    try {
      const details = await Promise.all(
        voices.map(async (v) => {
          try {
            return await ttsService.getVoiceDetails(v);
          } catch {
            return null;
          }
        })
      );
      if (!mounted.current) return;
      setVoiceDetails(details.filter(Boolean) as VoiceDetails[]);
    } catch (error) {
      console.error('Failed to load voice details:', error);
    }
  }, [mounted]);

  // ensure we always end with a valid voiceId
  const ensureValidVoiceSelection = useCallback((voices: string[], preferred?: string) => {
    if (!voices.length) {
      setVoiceId('');
      return;
    }
    if (preferred && voices.includes(preferred)) {
      setVoiceId(preferred);
      return;
    }
    if (!voiceId || !voices.includes(voiceId)) {
      setVoiceId(voices[0]);
    }
  }, [voiceId]);

  const loadVoices = useCallback(async (provider: string, preferredVoice?: string) => {
    try {
      const voices = await ttsService.getVoices(provider, user?.id);
      if (!mounted.current) return [];
      setAvailableVoices(voices);
      ensureValidVoiceSelection(voices, preferredVoice);

      if (provider === 'fishspeech') {
        await loadVoiceDetails(voices);
      } else {
        setVoiceDetails([]);
      }
      return voices;
    } catch (error) {
      console.error('Failed to load voices:', error);
      if (!mounted.current) return [];
      // fallback by provider
      const fallback = ['default'];
      setAvailableVoices(fallback);
      ensureValidVoiceSelection(fallback, preferredVoice);
      setVoiceDetails([]);
      return [];
    }
  }, [ensureValidVoiceSelection, loadVoiceDetails, mounted, user?.id]);

  // Ensure voices are loaded at least once on mount even before user context is ready
  useEffect(() => {
    if (availableVoices.length === 0) {
      void loadVoices('fishspeech');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleProviderChange = async (newProvider: string) => {
    setTtsProvider(newProvider);
    const voices = await loadVoices(newProvider);
    if (mounted.current && voices.length) {
      setVoiceId(voices[0]); // pick first available on switch
    }
  // Fetch TTS health for FishSpeech to display GPU/CPU upstream status
  useEffect(() => {
    let cancelled = false;
    const loadHealth = async () => {
      try {
        if (ttsProvider === 'fishspeech') {
          const h = await ttsService.getHealth();
          if (!cancelled) setTtsHealth(h);
        } else {
          if (!cancelled) setTtsHealth(null);
        }
      } catch {
        if (!cancelled) setTtsHealth({ status: 'degraded' } as any);
      }
    };
    void loadHealth();
    return () => { cancelled = true; };
  }, [ttsProvider]);

  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const validation = ttsService.validateVoiceFile(file);
    if (!validation.valid) {
      setUploadError(validation.error || 'Invalid file');
      return;
    }

    setUploadData((prev) => ({ ...prev, audioFile: file }));
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

      // reset form and refresh
      setUploadData({ voiceName: '', transcript: '', audioFile: null });
      setShowUploadModal(false);
      await loadVoices(ttsProvider);
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
      await loadVoices(ttsProvider);
    } catch (error) {
      alert(`Failed to delete voice: ${error instanceof Error ? error.message : 'Unknown error'}`);
    }
  };

  const save = () => {
    if (!effectiveUserId) return;
    setLoading(true);

    const settings = {
      userId: effectiveUserId,
      apiKey: apiKey || undefined,
      voiceId: voiceId || undefined,
      fishSpeechApiKey: fishSpeechApiKey || undefined,
      ttsProvider: ttsProvider || undefined,
    };

    ttsService
      .updateSettings(settings)
      .then(() => {
        if (!mounted.current) return;
        setSaveSuccess(true);
        try { console.log('Voice settings saved:', { provider: ttsProvider, voiceId }); } catch {}
        setTimeout(() => mounted.current && setSaveSuccess(false), 3000);
      })
      .catch(() => {})
      .finally(() => mounted.current && setLoading(false));
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">Voice Settings</h2>
      <p className="text-sm text-gray-600">Configure text-to-speech settings for your AI assistant.</p>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">TTS Provider</label>
          <select
            className="w-full border rounded px-3 py-2"
            value={ttsProvider}
            onChange={(e) => handleProviderChange(e.target.value)}
            disabled={loading}
            aria-label="TTS Provider"
          >
            {providers.map((provider) => (
              <option key={provider.id} value={provider.id} disabled={!provider.available}>
                {provider.name} {!provider.available ? '(Unavailable)' : ''}
              </option>
            ))}
          </select>
        </div>

        {ttsProvider === 'elevenlabs' && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">ElevenLabs API Key</label>
            <input
              type="password"
              className="w-full border rounded px-3 py-2"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              disabled={loading}
              placeholder="Enter your ElevenLabs API key"
              aria-label="ElevenLabs API Key"
            />
          </div>
        )}

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Voice</label>
          <div className="flex items-center gap-2">
            <select
              className="w-full border rounded px-3 py-2"
              value={voiceId}
              onChange={(e) => setVoiceId(e.target.value)}
              disabled={loading || availableVoices.length === 0}
              aria-label="Voice"
            >
              {availableVoices.length === 0 ? (
                <option value="">Loading voices...</option>
              ) : (
                availableVoices.map((voice) => (
                  <option key={voice} value={voice}>
                    {voice}
                  </option>
                ))
              )}
            </select>
            <button
              type="button"
              className="px-3 py-2 bg-gray-100 text-gray-700 rounded hover:bg-gray-200"
              onClick={async () => {
                try {
                  const phrase = 'This is how my voice sounds. do you like it?';
                  const blob = await ttsService.synthesize(phrase, user?.id, voiceId || undefined);
                  const audio = new Audio(URL.createObjectURL(blob));


                  audio.play();
                } catch (e) {
                  console.error('TTS test failed', e);
                }
              }}
              aria-label="Test voice"
            >
              Test
            </button>
          </div>
        </div>

        {ttsProvider === 'fishspeech' && (
          <div className="space-y-4">
            {/* Local Fish Speech: no API key required */}

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
                  aria-label="Upload Voice"
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
                      <div className="flex-1 min-w-0">
                        <h4 className="font-medium truncate">{voice.name}</h4>
                        <p className="text-sm text-gray-600 truncate">
                          {voice.transcript?.substring(0, 100)}

            {/* TTS Health Status */}
            {ttsProvider === 'fishspeech' && ttsHealth && (
              <div className={`p-3 rounded text-sm ${ttsHealth.status === 'ok' ? 'bg-green-50 text-green-700' : 'bg-yellow-50 text-yellow-700'}`}>
                <div className="flex items-center justify-between">
                  <span className="font-medium">TTS Health:</span>
                  <span className="uppercase">{ttsHealth.status}</span>
                </div>
                {Array.isArray(ttsHealth.upstreams) && ttsHealth.upstreams.length > 0 && (
                  <div className="mt-2 grid grid-cols-1 sm:grid-cols-2 gap-2">
                    {ttsHealth.upstreams.map((u, idx) => {
                      const isGpu = u.url.toLowerCase().includes('-gpu');
                      const label = isGpu ? 'GPU upstream' : 'CPU upstream';
                      const ok = u.status === '200';
                      return (
                        <div key={idx} className="flex items-center justify-between bg-white/60 rounded px-2 py-1 border">
                          <span className="text-xs">{label}</span>
                          <span className={`text-xs font-medium ${ok ? 'text-green-600' : 'text-red-600'}`}>{ok ? 'UP' : 'DOWN'}</span>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            )}

                          {voice.transcript && voice.transcript.length > 100 ? '...' : ''}
                        </p>
                        <div className="flex items-center flex-wrap gap-4 text-xs text-gray-500 mt-1">
                          <span>Audio: {voice.hasAudioFile ? '✓' : '✗'}</span>
                          <span>Embedding: {voice.hasEmbedding ? '✓' : '✗'}</span>
                          {voice.audioFileSize && <span>Size: {(voice.audioFileSize / 1024 / 1024).toFixed(1)}MB</span>}
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <button
                          onClick={() =>
                            setShowVoiceDetails(showVoiceDetails === voice.name ? null : voice.name)
                          }
                          className="p-2 text-gray-500 hover:text-gray-700"
                          title="Toggle details"
                          aria-label="Toggle voice details"
                        >
                          <Edit size={16} />
                        </button>
                        <button
                          onClick={() => handleDeleteVoice(voice.name)}
                          className="p-2 text-red-500 hover:text-red-700"
                          title="Delete voice"
                          aria-label="Delete voice"
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                    </div>

                    {showVoiceDetails === voice.name && (
                      <div className="mt-3 pt-3 border-t bg-white p-3 rounded">
                        <h5 className="font-medium mb-2">Voice Details</h5>
                        <p className="text-sm text-gray-700 mb-2">
                          <strong>Transcript:</strong> {voice.transcript || 'No transcript available.'}
                        </p>
                        {voice.createdAt && (
                          <p className="text-sm text-gray-500">Created: {new Date(voice.createdAt).toLocaleString()}</p>
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
        <button className="btn btn-primary" onClick={save} disabled={loading} aria-label="Save voice settings">
          {loading ? 'Saving...' : 'Save Changes'}
        </button>
        {saveSuccess && <div className="text-green-600 text-sm ml-4">Saved!</div>}
      </div>

      {/* Upload Modal */}
      {showUploadModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" role="dialog" aria-modal="true">
          <div className="bg-white rounded-lg p-6 w-full max-w-md">
            <h3 className="text-lg font-medium mb-4">Upload New Voice</h3>

            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Voice Name</label>
                <input
                  type="text"
                  className="w-full border rounded px-3 py-2"
                  value={uploadData.voiceName}
                  onChange={(e) => setUploadData((prev) => ({ ...prev, voiceName: e.target.value }))}
                  placeholder="Enter a unique voice name"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Audio File</label>
                <input
                  type="file"
                  accept="audio/wav,audio/mp3,audio/mpeg"
                  onChange={handleFileUpload}
                  className="w-full border rounded px-3 py-2"
                />
                <p className="text-xs text-gray-500 mt-1">Supported formats: WAV, MP3. Max size: 50MB.</p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Transcript</label>
                <textarea
                  className="w-full border rounded px-3 py-2 h-24"
                  value={uploadData.transcript}
                  onChange={(e) => setUploadData((prev) => ({ ...prev, transcript: e.target.value }))}
                  placeholder="Enter the text that matches the audio file"
                />
                <p className="text-xs text-gray-500 mt-1">This should match exactly what is spoken in the audio file.</p>
              </div>

              {uploadError && <div className="text-red-600 text-sm bg-red-50 p-2 rounded">{uploadError}</div>}
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

// ============================ Model Settings ============================

const ModelSettings = () => {
  const { user: initUser } = useInitialization();
  const mounted = useMountedRef();
  const eff = useEffectiveUser();

  // Use effective user ID with fallback like Chat page
  const modelUserId = eff.userId || 'admin';
  const user = { id: modelUserId };

  // Debug user context loading
  useEffect(() => {
    console.log('🔧 Settings: Effective User ID:', modelUserId);
    console.log('🔧 Settings: User ID available:', !!modelUserId);
  }, [modelUserId]);

  const [selectedEngine, setSelectedEngine] = useState<'ollama' | 'openai' | 'claude' | 'lmstudio' | 'vllm'>('claude');
  const [selectedModel, setSelectedModel] = useState<string>('');
  const [enabledEngines, setEnabledEngines] = useState<Record<string, boolean>>({
    ollama: true,
    lmstudio: true,
    openai: false,
    claude: false,
    vllm: false,
  });
  const [engineModels, setEngineModels] = useState<Record<string, string>>({});
  const [ollamaApiUrl, setOllamaApiUrl] = useState('http://localhost:11434');
  const [lmStudioApiUrl, setLmStudioApiUrl] = useState(() => {
    // Try to load from localStorage, fallback to default
    return localStorage.getItem("lmstudio:apiUrl") || "http://localhost:1234";
  });
  const [openAiApiKey, setOpenAiApiKey] = useState('');
  const [claudeApiUrl, setClaudeApiUrl] = useState('https://api.anthropic.com/v1');
  const [claudeApiKey, setClaudeApiKey] = useState('');
  const [enableStreaming, setEnableStreaming] = useState(true);
  const [loading, setLoading] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [cachedOllamaModels, setCachedOllamaModels] = useState<string[]>([]);

  // Persist consolidated chat settings and then rehydrate state
  const persistChatSettings = useCallback(async (why: string) => {
    if (!user?.id) return;
    try {
      const payload = {
        llmEngine: selectedEngine,
        llmModel: selectedModel || '',
        ttsProvider: 'fishspeech',
        ttsVoiceId: 'glados',
        enabledEngines,
        engineModels: { ...engineModels, [selectedEngine]: selectedModel || (engineModels[selectedEngine] || '') },
      } as any;
      await chatService.updateChatSettings(user.id, payload);
      if (!mounted.current) return;
      setSaveSuccess(true);
      setTimeout(() => mounted.current && setSaveSuccess(false), 1200);
      const s = await chatService.getChatSettings(user.id);
      if (!mounted.current) return;
      setEnabledEngines(prev => ({ ...prev, ...(s.enabledEngines || {}) }));
      setEngineModels(s.engineModels || {});
      if (s.llmEngine) setSelectedEngine(s.llmEngine as any);
      const modelForEngine = (s.engineModels && s.engineModels[s.llmEngine]) || s.llmModel || '';
      if (modelForEngine) setSelectedModel(modelForEngine);
      // eslint-disable-next-line no-console
      console.log('Persisted chat settings:', why, payload);
    } catch (e) {
      // eslint-disable-next-line no-console
      console.error('Persist chat settings failed:', e);
      if (mounted.current) setSaveError('Save failed');
    }
  }, [user?.id, selectedEngine, selectedModel, enabledEngines, engineModels, mounted]);

  useEffect(() => {
    if (!user?.id) return;
    setLoading(true);

    // Load consolidated chat settings so toggles and model selectors reflect saved state
    (async () => {
      try {
        const s = await chatService.getChatSettings(user.id);
        console.log('🔧 Settings: Loaded chat settings for user', user.id, ':', s);
        if (!mounted.current) return;

        // Ensure we have complete settings before updating state
        const loadedEnabledEngines = s.enabledEngines || {};
        const loadedEngineModels = s.engineModels || {};

        console.log('🔧 Settings: Enabled engines from backend:', loadedEnabledEngines);
        console.log('🔧 Settings: Engine models from backend:', loadedEngineModels);

        setEnabledEngines(loadedEnabledEngines);
        setEngineModels(loadedEngineModels);

        if (s.llmEngine) {
          setSelectedEngine(s.llmEngine as any);
          console.log('🔧 Settings: Set engine to', s.llmEngine);
        }

        // Get the model for the specific engine, not just the generic model
        const modelForEngine = loadedEngineModels[s.llmEngine] || s.llmModel || '';
        if (modelForEngine) {
          setSelectedModel(modelForEngine);
          console.log('🔧 Settings: Set model to', modelForEngine, 'for engine', s.llmEngine);
        }

        // If LM Studio is the selected engine, try to fetch the live model in the background (non-blocking)
        if (s.llmEngine === 'lmstudio') {
          (async () => {
            try {
              const res = await fetchWithTimeout(`/api/llm/lmstudio/model?userId=${eff.userId || user.id}`, 10000, { headers: eff.headers });
              if (res.ok) {
                const { model } = await res.json();
                if (!mounted.current) return;
                setSelectedModel(model || '');

                // Optionally, persist the new model to backend, but do not block UI
                fetch('/api/settings/llm', {
                  method: 'PUT',
                  headers: { 'Content-Type': 'application/json', ...eff.headers },
                  body: JSON.stringify({
                    userId: eff.userId || user.id,
                    engine: 'lmstudio',
                    model: model || ''
                  })
                }).catch(() => {});
              }
            } catch (e) {
              // Silently ignore errors, do not block UI
              console.error('Failed to fetch or save loaded LM Studio model on initial load:', e);
            }
          })();
        }
      } catch {
        // ignore
      }
    })();

    // connection settings
    fetch(`/api/settings/connections?userId=${eff.userId || user.id}`, { headers: eff.headers })
      .then((response) => response.json())
      .then((data) => {
        if (!mounted.current) return;
        if (data.OpenAiApiKey) setOpenAiApiKey(data.OpenAiApiKey);
        if (data.ClaudeApiKey) setClaudeApiKey(data.ClaudeApiKey);
        if (data.ClaudeApiUrl) setClaudeApiUrl(data.ClaudeApiUrl);
        if (data.OllamaApiUrl) setOllamaApiUrl(data.OllamaApiUrl);
        if (data.LmStudioApiUrl) setLmStudioApiUrl(data.LmStudioApiUrl);
        if (data.EnableStreaming !== undefined) setEnableStreaming(data.EnableStreaming);
      })
      .catch(() => {})
      .finally(() => mounted.current && setLoading(false));

    const cached = localStorage.getItem('cachedOllamaModels');
    if (cached) {
      try {
        const parsed = JSON.parse(cached);
        if (Array.isArray(parsed)) setCachedOllamaModels(parsed);
      } catch {
        // ignore bad cache
      }
    }
  }, [mounted, user?.id, eff.userId]);

  const refreshOllamaModels = async () => {
    try {
      const engine = selectedEngine === 'lmstudio' ? 'lmstudio' : selectedEngine === 'ollama' ? 'ollama' : '';
      if (!engine) {
        console.warn('Unsupported engine for model refresh:', selectedEngine);
        return;
      }
      const qs: string[] = [
        `engine=${engine}`,
      ];
      if (eff.userId || user?.id) qs.push(`userId=${encodeURIComponent(eff.userId || user!.id)}`);
      const base = engine === 'ollama' ? ollamaApiUrl : lmStudioApiUrl;
      if (base) qs.push(`baseUrl=${encodeURIComponent(base)}`);
      const res = await fetchWithTimeout(`/api/llm/models?${qs.join('&')}`, 10000, { headers: eff.headers });
      if (res.ok) {
        const payload = await res.json();
        let modelArray: string[] = Array.isArray(payload)
          ? payload
          : (payload && Array.isArray(payload.models) ? payload.models : []);
        // Ensure the saved selected model appears in the list so it displays without manual refresh
        if (selectedModel && !modelArray.includes(selectedModel)) {
          modelArray = [...modelArray, selectedModel];
        }
        if (!mounted.current) return;
        setCachedOllamaModels(modelArray);
        localStorage.setItem('cachedOllamaModels', JSON.stringify(modelArray));
      }
    } catch (e) {
      console.error('Model refresh failed', e);
    }
  };

  // Auto-populate model lists on mount/engine-change so last saved shows first without manual refresh
  useEffect(() => {
    if (!user?.id) return;
    if (!selectedEngine) return;
    void refreshOllamaModels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.id, selectedEngine, ollamaApiUrl, lmStudioApiUrl]);

  const refreshOpenAiModels = async () => {
    try {
      const qs: string[] = ['engine=openai'];
      if (user?.id) qs.push(`userId=${encodeURIComponent(user.id)}`);
      const res = await fetchWithTimeout(`/api/llm/models?${qs.join('&')}`);
      if (res.ok) {
        const payload = await res.json();
        const modelArray: string[] = Array.isArray(payload) ? payload : (payload?.models || []);
        console.log('OpenAI models refreshed:', modelArray);
      }
    } catch (e) {
      console.error('OpenAI model refresh failed', e);
    }
  };

  const refreshClaudeModels = async () => {
    try {
      const qs: string[] = ['engine=claude'];
      if (user?.id) qs.push(`userId=${encodeURIComponent(user.id)}`);
      const res = await fetchWithTimeout(`/api/llm/models?${qs.join('&')}`);
      if (res.ok) {
        const payload = await res.json();
        const modelArray: string[] = Array.isArray(payload) ? payload : (payload?.models || []);
        console.log('Claude models refreshed:', modelArray);
      }
    } catch (e) {
      console.error('Claude model refresh failed', e);
    }
  };

  const renderEngineSpecificFields = () => {
    switch (selectedEngine) {
      case 'ollama':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Ollama API URL</label>
              <input
                type="text"
                placeholder="http://localhost:11434"
                value={ollamaApiUrl}
                onChange={(e) => setOllamaApiUrl(e.target.value)}
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
                  Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={async (e) => { const v = e.target.value; setSelectedModel(v); setEngineModels(prev => ({ ...prev, [selectedEngine]: v })); await persistChatSettings('model change'); }}
                disabled={loading} // fix: do not couple to engine
              >
                <option value="">Select a model...</option>
                {cachedOllamaModels.map((m) => (
                  <option key={m} value={m}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          </>
        );

      case 'openai':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">OpenAI API URL</label>
              <input
                type="text"
                value={openAiApiKey ? 'https://api.openai.com/v1' : ''}
                readOnly
                className="w-full border rounded px-3 py-2 bg-gray-50"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">OpenAI API Key</label>
              <input
                type="password"
                placeholder="sk-..."
                value={openAiApiKey}
                onChange={(e) => setOpenAiApiKey(e.target.value)}
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
                  Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={async (e) => { const v = e.target.value; setSelectedModel(v); setEngineModels(prev => ({ ...prev, [selectedEngine]: v })); await persistChatSettings('model change'); }}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {OPENAI_MODELS.map((m) => (
                  <option key={m} value={m}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          </>
        );

      case 'claude':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Claude API URL</label>
              <input
                type="text"
                placeholder="https://api.anthropic.com/v1"
                value={claudeApiUrl}
                onChange={(e) => setClaudeApiUrl(e.target.value)}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Claude API Key</label>
              <input
                type="password"
                placeholder="sk-ant-..."
                value={claudeApiKey}
                onChange={(e) => setClaudeApiKey(e.target.value)}
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
                  Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={async (e) => { const v = e.target.value; setSelectedModel(v); setEngineModels(prev => ({ ...prev, [selectedEngine]: v })); await persistChatSettings('model change'); }}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {CLAUDE_MODELS.map((m) => (
                  <option key={m} value={m}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          </>
        );

      case 'lmstudio':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">LM Studio API URL</label>
              <input
                type="text"
                placeholder="http://localhost:1234"
                value={lmStudioApiUrl}
                onChange={(e) => setLmStudioApiUrl(e.target.value)}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
            </div>

<div>
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">Loaded Model</label>
                <button
                  onClick={async () => {
                    setLoading(true);
                    try {
                      // Use the LM Studio API URL from the input field (no hardcoded fallback)
                      const baseUrl = lmStudioApiUrl?.trim();
                      // Persist the user's choice
                      localStorage.setItem("lmstudio:apiUrl", baseUrl);

                      // Ask backend to resolve the currently loaded LM Studio model
                      const qs: string[] = [];
                      if (user?.id) qs.push(`userId=${encodeURIComponent(user.id)}`);
                      if (baseUrl) qs.push(`baseUrl=${encodeURIComponent(baseUrl)}`);
                      const res = await fetchWithTimeout(`/api/llm/lmstudio/model${qs.length ? '?' + qs.join('&') : ''}`);
                      if (!res.ok) throw new Error(`LM Studio model lookup failed: ${res.status}`);
                      const { model } = await res.json();

                      if (!model) {
                        setSelectedModel("");
                        localStorage.removeItem("lmstudio:modelId");
                        alert("No loaded model. Load one in LM Studio first.");
                        return;
                      }

                      // Remember the model id
                      setSelectedModel(model);
                      localStorage.setItem("lmstudio:modelId", model);
                      const updated = { ...engineModels, lmstudio: model };
                      setEngineModels(updated);

                      // Persist immediately so Chat sees the new model without an extra Save
                      try {
                        await chatService.updateChatSettings(user!.id, {
                          llmEngine: 'lmstudio',
                          llmModel: model || '',
                          enabledEngines,
                          engineModels: updated,
                        });
                      } catch {
                        // non-fatal in dev
                      }
                    } catch (e: any) {
                      setSelectedModel("");
                      localStorage.removeItem("lmstudio:modelId");
                      alert(e?.message || "Failed to refresh model");
                    } finally {
                      setLoading(false);
                    }
                  }}
                  className="text-sm text-blue-600 hover:text-blue-800"
                  disabled={loading}
                >
                  Refresh
                </button>
              </div>
              <input
                type="text"
                placeholder="http://localhost:1234"
                value={lmStudioApiUrl}
                onChange={(e) => {
                  setLmStudioApiUrl(e.target.value);
                  localStorage.setItem("lmstudio:apiUrl", e.target.value);
                }}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
              <p className="text-xs text-gray-500 mt-1">
                This is the model currently loaded in LM Studio. Load a new model in LM Studio and click Refresh. The model will be saved automatically.
              </p>
            </div>

            {/* Full models list (OpenAI-compatible /v1/models) */}
            <div className="mt-4">
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">Model</label>
                <button
                  onClick={refreshOllamaModels}
                  className="text-sm text-blue-600 hover:text-blue-800"
                  disabled={loading}
                >
                  Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={async (e) => { const v = e.target.value; setSelectedModel(v); setEngineModels(prev => ({ ...prev, [selectedEngine]: v })); await persistChatSettings('model change'); }}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {cachedOllamaModels.map((m) => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
              <p className="text-xs text-gray-500 mt-1">Uses LM Studio's OpenAI-compatible /v1/models endpoint.</p>
            </div>
          </>
        );

      case 'vllm':
        return (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">vLLM API URL</label>
              <input
                type="text"
                placeholder="http://localhost:8000"
                value={lmStudioApiUrl /* reuse local state holder or add separate state */}
                onChange={(e) => setLmStudioApiUrl(e.target.value)}
                className="w-full border rounded px-3 py-2"
                disabled={loading}
              />
            </div>
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">Models</label>
                <button
                  onClick={async () => {
                    try {
                      const qs: string[] = ['engine=vllm'];
                      if (user?.id) qs.push(`userId=${encodeURIComponent(user.id)}`);
                      if (lmStudioApiUrl) qs.push(`baseUrl=${encodeURIComponent(lmStudioApiUrl)}`);
                      const res = await fetchWithTimeout(`/api/llm/models?${qs.join('&')}`);
                      if (!res.ok) throw new Error(`vLLM models fetch failed: ${res.status}`);
                      const payload = await res.json();
                      const arr: string[] = Array.isArray(payload) ? payload : (payload?.models || []);
                      if (arr.length > 0) setSelectedModel(arr[0]);
                    } catch (e: any) {
                      alert(e?.message || 'Failed to refresh vLLM models');
                    }
                  }}
                  className="text-sm text-blue-600 hover:text-blue-800"
                  disabled={loading}
                >
                  Refresh
                </button>
              </div>
              <select
                className="w-full border rounded px-3 py-2"
                value={selectedModel}
                onChange={async (e) => { const v = e.target.value; setSelectedModel(v); setEngineModels(prev => ({ ...prev, [selectedEngine]: v })); await persistChatSettings('model change'); }}
                disabled={loading}
              >
                <option value="">Select a model...</option>
                {/* Render cachedOllamaModels when vLLM returns list; keep placeholder */}
                {cachedOllamaModels.map((m) => (
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
    // Use fallback user ID if authentication failed
    const effectiveUserId = user?.id || 'admin';

    console.log('🔧 Settings: === SAVE BUTTON CLICKED ===');
    console.log('🔧 Settings: User context:', user);
    console.log('🔧 Settings: Effective User ID:', effectiveUserId);

    if (!effectiveUserId) {
      console.error('🔧 Settings: No user ID available, cannot save');
      setSaveError('No user ID available - authentication required');
      return;
    }

    setLoading(true);
    setSaveError('');
    try {
      const finalEngineModels = { ...engineModels, [selectedEngine]: selectedModel || (engineModels[selectedEngine] || '') };

      const chatSettingsPayload = {
        llmEngine: selectedEngine,
        llmModel: selectedModel || '',
        ttsProvider: 'fishspeech',
        ttsVoiceId: 'glados',
        enabledEngines,
        engineModels: finalEngineModels,
      };

      console.log('🔧 Settings: === SAVING SETTINGS ===');
      console.log('🔧 Settings: User ID:', effectiveUserId);
      console.log('🔧 Settings: Selected Engine:', selectedEngine);
      console.log('🔧 Settings: Selected Model:', selectedModel);
      console.log('🔧 Settings: Enabled Engines:', enabledEngines);
      console.log('🔧 Settings: Engine Models:', finalEngineModels);
      console.log('🔧 Settings: Full Chat Settings Payload:', chatSettingsPayload);

      // Save chat settings
      const chatResponse = await chatService.updateChatSettings(effectiveUserId, chatSettingsPayload);
      console.log('🔧 Settings: Chat settings save response:', chatResponse);

      // Save connection settings
      const connectionSettings: any = {
        UserId: effectiveUserId,
        EnableStreaming: enableStreaming,
      };

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

      if (selectedEngine === 'vllm' && lmStudioApiUrl) {
        connectionSettings.VllmApiUrl = lmStudioApiUrl;
      }

      console.log('🔧 Settings: Connection Settings Payload:', connectionSettings);

      const connResponse = await fetch('/api/settings/connections', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...eff.headers },
        body: JSON.stringify(connectionSettings),
      });

      console.log('🔧 Settings: Connection settings save response status:', connResponse.status);

      // Verify what was actually saved by reading it back
      console.log('🔧 Settings: === VERIFYING SAVED SETTINGS ===');
      const savedSettings = await chatService.getChatSettings(effectiveUserId);
      console.log('🔧 Settings: Settings read back from backend:', savedSettings);

      if (!mounted.current) return;
      setSaveSuccess(true);
      setTimeout(() => mounted.current && setSaveSuccess(false), 3000);

      // Notify dashboard to refresh its status display
      window.dispatchEvent(
        new CustomEvent('llmSettingsChanged', {
          detail: { engine: selectedEngine, model: selectedModel }
        })
      );

      console.log('🔧 Settings: === SETTINGS SAVE COMPLETED ===');
    } catch (e) {
      console.error('🔧 Settings: Save failed with error:', e);
      mounted.current && setSaveError('Save failed');
    } finally {
      mounted.current && setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">AI Model Settings</h2>
      <p className="text-sm text-gray-600">Configure your AI model provider</p>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Enabled Engines</label>
          <div className="grid grid-cols-2 gap-2">
            {(['ollama','lmstudio','openai','claude','vllm'] as const).map((eng) => (
              <label key={eng} className="inline-flex items-center space-x-2 text-sm">
                <input
                  type="checkbox"
                  checked={!!enabledEngines[eng]}
                  onChange={(e) => setEnabledEngines(prev => ({ ...prev, [eng]: e.target.checked }))}
                  className="rounded border-gray-300"
                  disabled={(eng === 'openai' && !openAiApiKey) || (eng === 'claude' && !claudeApiKey)}
                  title={(eng === 'openai' && !openAiApiKey) ? 'Provide OpenAI API key in Connections to enable' : (eng === 'claude' && !claudeApiKey) ? 'Provide Claude API key in Connections to enable' : ''}
                />
                <span className="capitalize">{eng}</span>
              </label>
            ))}
          </div>
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">LLM Engine</label>
          <select
            className="w-full border rounded px-3 py-2"
            value={selectedEngine}
            onChange={async (e) => {
              const newEngine = e.target.value as any;
              setSelectedEngine(newEngine);
              setSelectedModel('');

              // If switching to LM Studio, fetch the live model and save it
              if (newEngine === 'lmstudio' && user?.id) {
                try {
                  const res = await fetchWithTimeout(`/api/llm/lmstudio/model?userId=${user.id}`);
                  if (res.ok) {
                    const { model } = await res.json();
                    if (!mounted.current) return;
                    setSelectedModel(model || '');

                    // Immediately persist the new model to backend
                    await fetch('/api/settings/llm', {
                      method: 'PUT',
                      headers: { 'Content-Type': 'application/json' },
                      body: JSON.stringify({
                        userId: user.id,
                        engine: 'lmstudio',
                        model: model || ''
                      })
                    });
                  }
                } catch (e) {
                  console.error('Failed to fetch or save loaded LM Studio model on engine switch:', e);
                }
              }
              // Persist engine/model selection so it sticks without manual Save
              await persistChatSettings('engine switch');
            }}
            disabled={loading}
          >
            <option value="claude">Claude</option>
            <option value="openai">OpenAI</option>
            <option value="ollama">Ollama</option>
            <option value="lmstudio">LM Studio</option>
            <option value="vllm">vLLM</option>
          </select>
        </div>

        {renderEngineSpecificFields()}

        <div>
          <label className="flex items-center space-x-2">
            <input
              type="checkbox"
              checked={enableStreaming}
              onChange={(e) => setEnableStreaming(e.target.checked)}
              className="rounded border-gray-300"
              disabled={loading}
            />
            <span className="text-sm font-medium text-gray-700">Enable Streaming</span>
          </label>
          <p className="text-xs text-gray-500 mt-1">Responses stream in real time.</p>
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
        <button className="btn btn-primary flex items-center" onClick={saveSettings} disabled={loading}>
          {loading ? 'Saving...' : (
            <>
              <Save size={16} className="mr-1.5" />
              Save Changes
            </>
          )}
        </button>
      </div>
    </div>
  );
};

// ======================= other sections unchanged =======================

const CharacterSettings = () => {
  const { user: initUser } = useInitialization();
  const { user: authUser } = useAuth();
  const mounted = useMountedRef();
  const eff = useEffectiveUser();

  // Use effective user ID with fallback like Chat page
  const charUserId = eff.userId || 'admin';
  const user = { id: charUserId };

  const [loading, setLoading] = useState(false);
  const [characters, setCharacters] = useState<Array<{ id: string; name: string; systemPrompt?: string; imagePath?: string }>>([]);
  const [defaultId, setDefaultId] = useState<string>('');
  const [newCharOpen, setNewCharOpen] = useState(false);
  const [createMode, setCreateMode] = useState<'form' | 'template' | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<string>('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState<{ name: string; systemPrompt: string; imagePath: string }>({ name: '', systemPrompt: '', imagePath: '' });
  const [form, setForm] = useState({ name: '', imagePath: '', systemPrompt: '', shared: false });
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);

  const placeholder = (
    <div className="w-16 h-16 rounded bg-gray-200 text-gray-500 flex items-center justify-center text-xs">
      No Image
    </div>
  );

  // Character templates
  const characterTemplates = {
    assistant: {
      name: 'Helpful Assistant',
      systemPrompt: 'You are a helpful, knowledgeable, and friendly AI assistant. Provide accurate information, answer questions clearly, and assist users with their tasks in a polite and professional manner.',
      imagePath: ''
    },
    creative: {
      name: 'Creative Writer',
      systemPrompt: 'You are a creative writing assistant with a vivid imagination. Help users craft stories, poems, and creative content. Use descriptive language, engaging narratives, and inspire creativity in your responses.',
      imagePath: ''
    },
    technical: {
      name: 'Technical Expert',
      systemPrompt: 'You are a technical expert with deep knowledge across programming, engineering, and technology. Provide precise technical guidance, explain complex concepts clearly, and help solve technical problems.',
      imagePath: ''
    },
    teacher: {
      name: 'Patient Teacher',
      systemPrompt: 'You are a patient and encouraging teacher. Break down complex topics into understandable parts, use examples and analogies, and adapt your teaching style to help users learn effectively.',
      imagePath: ''
    },
    analyst: {
      name: 'Data Analyst',
      systemPrompt: 'You are an analytical thinker who excels at examining data, identifying patterns, and providing insights. Help users understand complex information and make data-driven decisions.',
      imagePath: ''
    },
    glados: {
      name: 'GLaDOS',
      systemPrompt: 'You are GLaDOS: a highly intelligent, sarcastic, darkly humorous AI from the Portal series. Speak with dry wit, occasional passive-aggressiveness, and a tone of clinical detachment. Always be helpful while maintaining your signature style.',
      imagePath: ''
    }
  };

  const load = useCallback(async () => {
    if (!user?.id) return;
    setLoading(true);
    setError('');
    try {
      const [charsRes, defRes] = await Promise.all([
        fetch(`/api/character/user/${encodeURIComponent(user.id)}`, { headers: eff.headers }),
        fetch(`/api/settings/DefaultCharacterId?userId=${encodeURIComponent(user.id)}`, { headers: eff.headers }),
      ]);
      if (charsRes.ok) {
        const list = await charsRes.json();
        if (mounted.current) setCharacters(Array.isArray(list) ? list : []);
      }
      if (defRes.ok) {
        const { value } = await defRes.json();
        if (mounted.current) setDefaultId(value || '');
      }
    } catch (e) {
      setError('Failed to load characters');
    } finally {
      mounted.current && setLoading(false);
    }
  }, [mounted, user?.id]);

  useEffect(() => { void load(); }, [load]);

  const setAsDefault = async (id: string) => {
    if (!user?.id) return;
    try {
      await fetch(`/api/user/${encodeURIComponent(user.id)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...eff.headers },
        body: JSON.stringify({ default_character: id })
      });
      setDefaultId(id);
      setSaved(true); setTimeout(() => setSaved(false), 2000);
    } catch {
      setError('Failed to set default');
    }
  };

  const createCharacter = async () => {
    if (!user?.id) return;
    setLoading(true);
    setError('');
    try {
      const resp = await fetch('/api/character', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...eff.headers },
        body: JSON.stringify({
          name: form.name.trim() || 'New Character',
          imagePath: form.imagePath.trim(),
          systemPrompt: form.systemPrompt.trim() || 'You are a helpful AI assistant.',
          shared: form.shared || false
        })
      });
      if (!resp.ok) throw new Error('Create failed');
      setNewCharOpen(false);
      setForm({ name: '', imagePath: '', systemPrompt: '', shared: false });
      setCreateMode(null);
      setSelectedTemplate('');
      await load();
      setSaved(true); setTimeout(() => setSaved(false), 2000);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to create');
    } finally {
      mounted.current && setLoading(false);
    }
  };

  const saveEdit = async () => {
    if (!editingId) return;
    setLoading(true);
    try {
      const body: any = { name: editForm.name, systemPrompt: editForm.systemPrompt };
      if (editForm.imagePath) body.imagePath = editForm.imagePath;
      const resp = await fetch(`/api/character/${encodeURIComponent(editingId)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...eff.headers },
        body: JSON.stringify(body)
      });
      if (!resp.ok) throw new Error('Update failed');
      setEditingId(null);
      await load();
      setSaved(true); setTimeout(() => setSaved(false), 2000);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to update');
    } finally {
      setLoading(false);
    }
  };

  const deleteCharacter = async (id: string) => {
    if (!confirm('Delete this character?')) return;
    setLoading(true);
    try {
      const resp = await fetch(`/api/character/${encodeURIComponent(id)}`, { method: 'DELETE', headers: eff.headers });
      if (!resp.ok) throw new Error('Delete failed');
      await load();
      setSaved(true); setTimeout(() => setSaved(false), 1500);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-medium">Character Settings</h2>
      <p className="text-sm text-gray-600">Create and manage AI personalities. Set a default character for chat. Add an image for visual identity.</p>

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-3 py-2 rounded">{error}</div>}
      {saved && <div className="bg-green-50 border border-green-200 text-green-700 px-3 py-2 rounded">Saved</div>}

      {/* Current default */}
      <div className="border rounded p-4">
        <h3 className="font-semibold mb-2">Current Personality</h3>
        {(() => {
          const cur = characters.find(c => c.id === defaultId) || characters[0];
          if (!cur) return <div className="text-sm text-gray-500">No characters yet.</div>;
          return (
            <div className="flex items-center space-x-3">
              {cur.imagePath ? <img src={cur.imagePath.startsWith('http') || cur.imagePath.startsWith('/') ? cur.imagePath : `/${cur.imagePath}`} alt={cur.name} className="w-16 h-16 rounded object-cover" /> : placeholder}
              <div>
                <div className="font-medium">{cur.name}</div>
                <div className="text-xs text-gray-500">ID: {cur.id}</div>
              </div>
              <button className="ml-auto btn btn-outline" onClick={() => setAsDefault(cur.id)} disabled={loading || defaultId === cur.id}>Set Default</button>
            </div>
          );
        })()}
      </div>

      {/* List */}
      <div className="border rounded p-4">
        <div className="flex items-center justify-between mb-3">
          <h3 className="font-semibold">Your Characters</h3>
          <button className="btn btn-primary" onClick={() => setNewCharOpen(v => !v)}>{newCharOpen ? 'Cancel' : 'New Character'}</button>
        </div>
        <div className="flex items-center space-x-3 mb-3">
          <label className="btn btn-outline">
            Upload YAML Card
            <input
              type="file"
              accept=".yaml,.yml"
              className="hidden"
              onChange={async (e) => {
                if (!e.target.files || !e.target.files[0]) return;
                const text = await e.target.files[0].text();
                try {
                  const resp = await fetch('/api/character/import-yaml', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', ...eff.headers },
                    body: JSON.stringify({ yaml: text })
                  });
                  if (!resp.ok) throw new Error('Import failed');
                  await load();
                } catch (err) {
                  alert((err as Error).message);
                } finally {
                  e.target.value = '';
                }
              }}
            />
          </label>
          <button
            className="btn btn-primary"
            onClick={() => {
              setCreateMode('template');
              setNewCharOpen(true);
            }}
          >
            Create Character
          </button>
        </div>
        {newCharOpen && createMode === 'template' && (
          <div className="mb-4 space-y-4">
            <div>
              <label className="text-sm font-medium mb-2 block">Choose a template or start from scratch:</label>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <button
                  className={`p-3 border rounded text-left hover:bg-gray-50 ${
                    selectedTemplate === '' ? 'border-blue-500 bg-blue-50' : 'border-gray-200'
                  }`}
                  onClick={() => {
                    setSelectedTemplate('');
                    setForm({ name: '', imagePath: '', systemPrompt: '', shared: false });
                  }}
                >
                  <div className="font-medium">Blank Character</div>
                  <div className="text-xs text-gray-500">Start with a blank template</div>
                </button>
                {Object.entries(characterTemplates).map(([key, template]) => (
                  <button
                    key={key}
                    className={`p-3 border rounded text-left hover:bg-gray-50 ${
                      selectedTemplate === key ? 'border-blue-500 bg-blue-50' : 'border-gray-200'
                    }`}
                    onClick={() => {
                      setSelectedTemplate(key);
                      setForm({
                        name: template.name,
                        imagePath: template.imagePath,
                        systemPrompt: template.systemPrompt,
                        shared: false
                      });
                    }}
                  >
                    <div className="font-medium">{template.name}</div>
                    <div className="text-xs text-gray-500 line-clamp-2">{template.systemPrompt.slice(0, 80)}...</div>
                  </button>
                ))}
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className="text-sm font-medium">Name</label>
                <input
                  className="w-full border rounded px-3 py-2"
                  value={form.name}
                  onChange={e => setForm({ ...form, name: e.target.value })}
                  placeholder="Enter character name"
                />
              </div>
              <div>
                <label className="text-sm font-medium">Image URL (optional)</label>
                <input
                  className="w-full border rounded px-3 py-2"
                  value={form.imagePath}
                  onChange={e => setForm({ ...form, imagePath: e.target.value })}
                  placeholder="/images/character.png or https://..."
                />
              </div>
              <div className="sm:col-span-2">
                <label className="text-sm font-medium">System Prompt</label>
                <textarea
                  className="w-full border rounded px-3 py-2 h-32"
                  value={form.systemPrompt}
                  onChange={e => setForm({ ...form, systemPrompt: e.target.value })}
                  placeholder="Describe the character's personality, role, and behavior..."
                />
              </div>
              {authUser?.role === 'admin' && (
                <div className="sm:col-span-2">
                  <label className="flex items-center space-x-2">
                    <input
                      type="checkbox"
                      checked={form.shared}
                      onChange={e => setForm({ ...form, shared: e.target.checked })}
                    />
                    <span className="text-sm font-medium">Make this character available to all users (Admin only)</span>
                  </label>
                </div>
              )}
              <div className="sm:col-span-2 flex justify-end space-x-2">
                <button
                  className="btn"
                  onClick={() => {
                    setNewCharOpen(false);
                    setCreateMode(null);
                    setSelectedTemplate('');
                    setForm({ name: '', imagePath: '', systemPrompt: '', shared: false });
                  }}
                  disabled={loading}
                >
                  Cancel
                </button>
                <button className="btn btn-primary" onClick={createCharacter} disabled={loading || !form.name.trim() || !form.systemPrompt.trim()}>
                  {loading ? 'Creating...' : 'Create Character'}
                </button>
              </div>
            </div>
          </div>
        )}
        <div className="grid sm:grid-cols-2 gap-3">
          {characters.map(c => (
            <div key={c.id} className="border rounded p-3 space-y-2">
              <div className="flex items-center space-x-3">
                {c.imagePath ? <img src={c.imagePath.startsWith('http') || c.imagePath.startsWith('/') ? c.imagePath : `/${c.imagePath}`} alt={c.name} className="w-12 h-12 rounded object-cover" /> : placeholder}
                <div className="flex-1">
                  {editingId === c.id ? (
                    <>
                      <input className="w-full border rounded px-2 py-1 mb-1" value={editForm.name} onChange={e => setEditForm({ ...editForm, name: e.target.value })} />
                      <textarea className="w-full border rounded px-2 py-1 h-20" value={editForm.systemPrompt} onChange={e => setEditForm({ ...editForm, systemPrompt: e.target.value })} />
                    </>
                  ) : (
                    <>
                      <div className="font-medium">{c.name}</div>
                      <div className="text-xs text-gray-500 truncate">{(c.systemPrompt || '').slice(0, 120)}</div>
                    </>
                  )}
                </div>
                <button className="btn btn-outline" onClick={() => setAsDefault(c.id)} disabled={loading || defaultId === c.id}>Default</button>
                {editingId === c.id ? (
                  <>
                    <button className="btn btn-primary" onClick={saveEdit} disabled={loading}>Save</button>
                    <button className="btn" onClick={() => setEditingId(null)} disabled={loading}>Cancel</button>
                  </>
                ) : (
                  <>
                    <button className="btn" onClick={() => { setEditingId(c.id); setEditForm({ name: c.name, systemPrompt: c.systemPrompt || '', imagePath: c.imagePath || '' }); }}>Edit</button>
                    <button className="btn btn-error" onClick={() => deleteCharacter(c.id)} disabled={loading}>Delete</button>
                  </>
                )}
              </div>
              <label className="btn btn-ghost">
                Upload Image
                <input type="file" accept="image/*" className="hidden" onChange={async (e) => {
                  if (!e.target.files || !e.target.files[0]) return;
                  const fd = new FormData();
                  fd.append("file", e.target.files[0]);
                  fd.append("character_id", c.id);
                  const r = await fetch("/api/character/image", { method: "POST", body: fd, headers: eff.headers });
                  if (r.ok) { await load(); } else { alert("Upload failed"); }
                }} />
              </label>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
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

// ============================== Mobile & Notifications Settings ==============================

const MobileSettings = () => {
  const { isSupported, isEnabled, isLoading, enable, disable } = usePushNotifications();

  const isPWA =
    typeof window !== 'undefined' &&
    (window.matchMedia('(display-mode: standalone)').matches ||
      (window.navigator as Navigator & { standalone?: boolean }).standalone === true);

  return (
    <div className="space-y-6">
      <div className="border-b border-gray-200 pb-4">
        <h3 className="text-lg font-medium text-gray-900">Mobile &amp; Notifications</h3>
        <p className="text-sm text-gray-600 mt-1">
          Install SwAIvyn as a Progressive Web App (PWA) on your phone or tablet, and manage push
          notification preferences.
        </p>
      </div>

      {/* Install prompt */}
      <div className="bg-indigo-50 border border-indigo-200 rounded-lg p-4">
        <div className="flex items-start gap-3">
          <Smartphone className="text-indigo-600 mt-0.5 flex-shrink-0" size={20} />
          <div>
            <h4 className="text-indigo-900 font-medium">Install as Mobile App</h4>
            <p className="text-indigo-700 text-sm mt-1">
              SwAIvyn works as a Progressive Web App (PWA). Open this page in your mobile browser
              and use &ldquo;Add to Home Screen&rdquo; to install it as a native-like app with
              offline support, voice interaction, and push notifications.
            </p>
            {isPWA && (
              <span className="inline-block mt-2 text-xs bg-indigo-200 text-indigo-800 rounded-full px-2 py-0.5 font-medium">
                ✓ Running as installed app
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Push notification toggle */}
      <div className="bg-white border border-gray-200 rounded-lg p-4">
        <div className="flex items-center justify-between">
          <div className="flex items-start gap-3">
            {isEnabled ? (
              <Bell className="text-green-600 mt-0.5 flex-shrink-0" size={20} />
            ) : (
              <BellOff className="text-gray-400 mt-0.5 flex-shrink-0" size={20} />
            )}
            <div>
              <h4 className="text-gray-900 font-medium">Push Notifications</h4>
              <p className="text-gray-500 text-sm mt-1">
                {isSupported
                  ? 'Receive a notification when an agent task completes or a scheduled workflow fires.'
                  : 'Push notifications are not supported in this browser. Install SwAIvyn as a PWA to enable them.'}
              </p>
            </div>
          </div>
          {isSupported && (
            <button
              onClick={isEnabled ? disable : enable}
              disabled={isLoading}
              className={`ml-4 px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                isEnabled
                  ? 'bg-red-50 text-red-700 border border-red-200 hover:bg-red-100'
                  : 'bg-indigo-600 text-white hover:bg-indigo-700'
              } disabled:opacity-50`}
            >
              {isLoading ? (
                <InlineSpinner size={14} />
              ) : isEnabled ? (
                'Disable'
              ) : (
                'Enable'
              )}
            </button>
          )}
        </div>
      </div>

      {/* Feature summary */}
      <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
        <h4 className="text-gray-800 font-medium mb-3">Mobile Features</h4>
        <ul className="space-y-2 text-sm text-gray-600">
          <li className="flex items-center gap-2">
            <span className="text-green-500">✓</span> Full conversation history with markdown
            rendering
          </li>
          <li className="flex items-center gap-2">
            <span className="text-green-500">✓</span> Voice interaction (microphone → Whisper STT
            → LLM → TTS playback)
          </li>
          <li className="flex items-center gap-2">
            <span className="text-green-500">✓</span> Offline access to last-known conversations
          </li>
          <li className="flex items-center gap-2">
            <span className="text-green-500">✓</span> LLM engine and character settings sync
            across devices
          </li>
          <li className="flex items-center gap-2">
            <span className={isSupported && isEnabled ? 'text-green-500' : 'text-gray-400'}>
              {isSupported && isEnabled ? '✓' : '○'}
            </span>{' '}
            Push notifications for agent task results
          </li>
        </ul>
      </div>
    </div>
  );
};

// ============================== page shell ==============================

const SettingsPage = () => {
  const [activeTab, setActiveTab] = useState('invocation');

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
                  {tabs.map((tab) => (
                    <li key={tab.id}>
                      <button
                        className={`w-full text-left px-4 py-3 flex items-center transition-colors duration-150 ${
                          activeTab === tab.id
                            ? 'bg-primary-50 text-primary-700 border-l-4 border-primary-500'
                            : 'text-gray-700 hover:bg-gray-100'
                        }`}
                        onClick={() => setActiveTab(tab.id)}
                        aria-current={activeTab === tab.id ? 'page' : undefined}
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
              {activeTab === 'invocation' && <InvocationSettings />}
              {activeTab === 'model' && <ModelSettings />}
              {activeTab === 'voice' && <VoiceSettings />}
              {activeTab === 'character' && <CharacterSettings />}
              {activeTab === 'character-create' && <CharacterCreateSettings />}
              {activeTab === 'agents' && <AgentsSettings />}
              {activeTab === 'agentstack' && <AgentStackSettings />}
              {activeTab === 'external-agents' && <ExternalAgentsSettings />}
              {activeTab === 'appearance' && <AppearanceSettings />}
              {activeTab === 'network' && <NetworkSettings />}
              {activeTab === 'mobile' && <MobileSettings />}
            </div>
          </div>
        </div>
      </div>
    </motion.div>
  );
};

export default SettingsPage;
