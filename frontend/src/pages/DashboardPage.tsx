import { motion } from 'framer-motion';
import { useState, useEffect, useMemo } from 'react';
import {
  Activity,
  Database,
  MessageSquare,
  Brain,
  Bot,
  CheckCircle,
  XCircle,
  Clock,
  Users,
  Mic,
  Volume2,
  Save,
  Play
} from 'lucide-react';
import { useInitialization } from '../contexts/InitializationContext';
import useEffectiveUser from '../hooks/useEffectiveUser';
import InlineSpinner from '../components/ui/InlineSpinner';
import ttsService from '../services/ttsService';

interface SystemStatus {
  llmEngine: string;
  llmModel: string;
  ttsProvider: string;
  ttsVoice: string;
  charactersLoaded: number;
  memoryItems: number;
  chatSessions: number;
  uptime: string;
  lastActivity: string;
}

const DashboardPage = () => {
  const { user } = useInitialization();
  const eff = useEffectiveUser();
  const [systemStatus, setSystemStatus] = useState<SystemStatus>({
    llmEngine: 'Loading...',
    llmModel: 'Loading...',
    ttsProvider: 'Loading...',
    ttsVoice: 'Loading...',
    charactersLoaded: 0,
    memoryItems: 0,
    chatSessions: 0,
    uptime: '0m',
    lastActivity: 'Never'
  });
  const [loading, setLoading] = useState(true);
  const [agents, setAgents] = useState<{ running: any[]; completed: number; failed: number; pending: number }>({ running: [], completed: 0, failed: 0, pending: 0 });
  const traefikUrl = useMemo(() => {
    // Check environment variable first
    const envUrl = (import.meta as any).env?.VITE_TRAEFIK_URL;
    if (envUrl) return envUrl;
    
    // Dynamic detection based on current host
    const currentHost = window.location.hostname;
    const currentPort = window.location.port;
    
    // If we're on localhost:5000 (development), Traefik is likely on port 80
    if (currentHost === 'localhost' && currentPort === '5000') {
      return 'http://traefik.localhost:80';
    }
    
    // If we're on a different host (like Replit), try the same port
    if (currentPort && currentPort !== '80' && currentPort !== '443') {
      return `http://traefik.${currentHost}:${currentPort}`;
    }
    
    // Default fallback for localhost development
    return 'http://traefik.localhost:80';
  }, []);

  useEffect(() => {
    loadSystemStatus();
    // Refresh status every 30 seconds
    const interval = setInterval(loadSystemStatus, 30000);

    return () => {
      clearInterval(interval);
    };
  }, [eff.userId]);

  const loadSystemStatus = async () => {
    try {
      console.log('🔍 Dashboard: Loading system status...');

      const uid = eff.userId ? encodeURIComponent(eff.userId) : '';
      const url = uid ? `/api/dashboard/status?userId=${uid}` : '/api/dashboard/status';
      const statusResponse = await fetch(url, { headers: eff.headers });

      if (statusResponse.ok) {
        const statusData = await statusResponse.json();
        console.log('🔍 Dashboard: Status data received:', statusData);
        setAgents(statusData.agents || { running: [], completed: 0, failed: 0, pending: 0 });
        setSystemStatus({
          llmEngine: statusData.llm?.engine || 'Unknown',
          llmModel: statusData.llm?.model || 'Not selected',
          ttsProvider: statusData.tts?.provider || 'Unknown',
          ttsVoice: statusData.tts?.voice || 'Not selected',
          charactersLoaded: statusData.metrics?.characterCount || 0,
          memoryItems: statusData.metrics?.memoryCount || 0,
          chatSessions: statusData.metrics?.conversationCount || 0,
          uptime: calculateUptime(),
          lastActivity: new Date().toLocaleTimeString()
        });
      } else {
        console.error('🔍 Dashboard: Failed to load status');
      }
    } catch (error) {
      console.error('Error loading system status:', error);

      // Set default values on error
      setSystemStatus({
        llmEngine: 'Error',
        llmModel: 'Error',
        ttsProvider: 'Error',
        ttsVoice: 'Error',
        charactersLoaded: 0,
        memoryItems: 0,
        chatSessions: 0,
        uptime: calculateUptime(),
        lastActivity: 'Error'
      });
    } finally {
      setLoading(false);
    }
  };

  const calculateUptime = () => {
    // Simple uptime calculation - in a real app this would come from the backend
    const startTime = sessionStorage.getItem('appStartTime');
    if (!startTime) {
      const now = Date.now().toString();
      sessionStorage.setItem('appStartTime', now);
      return '0m';
    }

    const elapsed = Date.now() - parseInt(startTime);
    const minutes = Math.floor(elapsed / 60000);
    const hours = Math.floor(minutes / 60);

    if (hours > 0) {
      return `${hours}h ${minutes % 60}m`;
    }
    return `${minutes}m`;
  };

  return (
    <motion.div
      className="min-h-[calc(100vh-64px)] bg-gray-50 p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="max-w-7xl mx-auto">
        <div className="mb-6">
          <h1 className="text-2xl font-medium text-gray-800">Dashboard</h1>
          <p className="text-gray-600">System overview and status</p>
        </div>

        {loading ? (
          <div className="flex items-center justify-center h-64">
            <InlineSpinner />
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {/* LLM Status */}
            <StatusCard
              title="LLM Engine"
              value={systemStatus.llmEngine}
              subtitle={systemStatus.llmModel}
              icon={<Bot size={24} />}
              status={'info'}
            />

            {/* TTS Selection (persisted) */}
            <StatusCard
              title="TTS Provider"
              value={systemStatus.ttsProvider}
              subtitle={systemStatus.ttsVoice}
              icon={<Mic size={24} />}
              status={'info'}
            />

            {/* Characters */}
            <StatusCard
              title="Characters"
              value={systemStatus.charactersLoaded.toString()}
              subtitle="Loaded characters"
              icon={<Users size={24} />}
              status="info"
            />

            {/* Memory Items */}
            <StatusCard
              title="Memory"
              value={systemStatus.memoryItems.toString()}
              subtitle="Stored memories"
              icon={<Brain size={24} />}
              status="info"
            />

            {/* Chat Sessions */}
            <StatusCard
              title="Chat Sessions"
              value={systemStatus.chatSessions.toString()}
              subtitle="Active sessions"
              icon={<MessageSquare size={24} />}
              status="info"
            />

            {/* Uptime */}
            <StatusCard
              title="Uptime"
              value={systemStatus.uptime}
              subtitle="Session duration"
              icon={<Clock size={24} />}
              status="info"
            />

            {/* Last Activity */}
            <StatusCard
              title="Last Activity"
              value={systemStatus.lastActivity}
              subtitle="Most recent action"
              icon={<Activity size={24} />}
              status="info"
            />
          </div>        )}

        {/* Agents Summary (reported by workers) */}
        <div className="mt-8">
          <h2 className="text-lg font-medium text-gray-800 mb-4">Agents</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatusCard title="Running" value={String(agents.running.length)} subtitle="Active agents" icon={<Bot size={24} />} status="info" />
            <StatusCard title="Completed" value={String(agents.completed)} subtitle="Finished runs" icon={<CheckCircle size={24} />} status="info" />
            <StatusCard title="Failed" value={String(agents.failed)} subtitle="Error runs" icon={<XCircle size={24} />} status="info" />
            <StatusCard title="Pending" value={String(agents.pending)} subtitle="Queued runs" icon={<Clock size={24} />} status="info" />
          </div>
          {agents.running.length > 0 && (
            <div className="mt-4 bg-white rounded-lg border border-gray-200">
              <div className="p-4 border-b text-sm font-medium text-gray-700">Currently Running</div>
              <ul className="divide-y">
                {agents.running.map((a:any) => (
                  <li key={a.id} className="p-4 text-sm flex items-center justify-between">
                    <div>
                      <div className="font-medium text-gray-800">{a.name || a.id}</div>
                      <div className="text-gray-500">Started {a.startedAt || ''}</div>
                    </div>
                    <span className="px-2 py-1 text-xs rounded bg-green-50 text-green-700">running</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>

        {/* Traefik Dashboard */}
        <div className="mt-8">
          <h2 className="text-lg font-medium text-gray-800 mb-4">Traefik</h2>
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            <iframe src={traefikUrl} title="Traefik" className="w-full" style={{ height: '600px', border: '0' }} />
          </div>
        </div>
      </div>
    </motion.div>
  );
};

interface StatusCardProps {
  title: string;
  value: string;
  subtitle: string;
  icon: React.ReactNode;
  status?: 'connected' | 'disconnected' | 'info';
  statusIcon?: React.ReactNode;
}

const StatusCard = ({ title, value, subtitle, icon, status = 'info', statusIcon }: StatusCardProps) => {
  const getStatusColor = () => {
    switch (status) {
      case 'connected':
        return 'text-green-600';
      case 'disconnected':
        return 'text-red-600';
      default:
        return 'text-blue-600';
    }
  };

  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-4">
        <div className={`p-2 rounded-lg bg-gray-50 ${getStatusColor()}`}>
          {icon}
        </div>
        {statusIcon && (
          <div className={getStatusColor()}>
            {statusIcon}
          </div>
        )}
      </div>
      <div>
        <h3 className="text-sm font-medium text-gray-600 mb-1">{title}</h3>
        <p className="text-2xl font-semibold text-gray-900 mb-1">{value}</p>
        <p className="text-sm text-gray-500">{subtitle}</p>
      </div>
    </div>
  );
};

interface QuickActionCardProps {
  title: string;
  description: string;
  icon: React.ReactNode;
  href: string;
}

const QuickActionCard = ({ title, description, icon, href }: QuickActionCardProps) => {
  return (
    <a
      href={href}
      className="block bg-white rounded-lg shadow-sm border border-gray-200 p-4 hover:shadow-md transition-shadow"
    >
      <div className="flex items-center mb-3">
        <div className="p-2 rounded-lg bg-primary-50 text-primary-600 mr-3">
          {icon}
        </div>
        <h3 className="font-medium text-gray-900">{title}</h3>
      </div>
      <p className="text-sm text-gray-600">{description}</p>
    </a>  );
};

interface VoiceSettingsCardProps {
  userId?: string;
}

const VoiceSettingsCard = ({ userId }: VoiceSettingsCardProps) => {
  const [voiceSettings, setVoiceSettings] = useState({
    provider: 'Loading...',
    voice: 'Loading...',
    apiKey: '',
  });
  const [availableVoices, setAvailableVoices] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);

  useEffect(() => {
    if (!userId) return;
    loadVoiceSettings();
  }, [userId]);

  const loadVoiceSettings = async () => {
    if (!userId) return;
    
    try {
      setLoading(true);
      const settings = await ttsService.getSettings(userId);
      
      setVoiceSettings({
        provider: settings.ttsProvider || 'elevenlabs',
        voice: settings.voiceId || 'Rachel',
        apiKey: settings.apiKey || '',
      });

      // Load available voices for the current provider
      const voices = await ttsService.getVoices(settings.ttsProvider || 'elevenlabs', userId);
      setAvailableVoices(voices);
    } catch (error) {
      console.error('Failed to load voice settings:', error);
      // Set fallback values
      setVoiceSettings({
        provider: 'elevenlabs',
        voice: 'Rachel',
        apiKey: '',
      });
      setAvailableVoices(['Rachel', 'Domi', 'Bella', 'Antoni']);
    } finally {
      setLoading(false);
    }
  };

  const handleProviderChange = async (newProvider: string) => {
    setVoiceSettings(prev => ({ ...prev, provider: newProvider }));
    
    try {
      const voices = await ttsService.getVoices(newProvider, userId);
      setAvailableVoices(voices);
      
      // Reset to first available voice when switching providers
      if (voices.length > 0) {
        setVoiceSettings(prev => ({ ...prev, voice: voices[0] }));
      }
    } catch (error) {
      console.error('Failed to load voices for provider:', error);
      setAvailableVoices(['default']);
      setVoiceSettings(prev => ({ ...prev, voice: 'default' }));
    }
  };

  const handleVoiceChange = (newVoice: string) => {
    setVoiceSettings(prev => ({ ...prev, voice: newVoice }));
  };

  const handleSave = async () => {
    if (!userId) return;
    
    setSaving(true);
    setSaveSuccess(false);
    
    try {
      await ttsService.updateSettings({
        userId,
        ttsProvider: voiceSettings.provider,
        voiceId: voiceSettings.voice,
        apiKey: voiceSettings.apiKey,
      });
      
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch (error) {
      console.error('Failed to save voice settings:', error);
    } finally {
      setSaving(false);
    }
  };

  const formatVoiceName = (voice: string) => {
    switch (voice.toLowerCase()) {
      case 'glados':
        return 'GLaDOS';
      case 'jazzy':
        return 'Jazzy';
      case 'scarlet':
        return 'Scarlet';
      default:
        return voice.charAt(0).toUpperCase() + voice.slice(1);
    }
  };

  if (loading) {
    return (
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <div className="flex items-center justify-center">
          <InlineSpinner />
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
      <div className="flex items-center mb-4">
        <Volume2 size={20} className="text-blue-600 mr-2" />
        <h3 className="text-lg font-medium text-gray-900">Voice Configuration</h3>
      </div>
      
      <div className="space-y-4">
        {/* TTS Provider Selection */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            TTS Provider
          </label>
          <select
            value={voiceSettings.provider}
            onChange={(e) => handleProviderChange(e.target.value)}
            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          >
            <option value="elevenlabs">ElevenLabs</option>
            <option value="fishspeech">Fish Speech</option>
            <option value="openai">OpenAI</option>
          </select>
        </div>

        {/* Voice Selection */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Voice
          </label>
          <select
            value={voiceSettings.voice}
            onChange={(e) => handleVoiceChange(e.target.value)}
            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          >
            {availableVoices.map(voice => (
              <option key={voice} value={voice}>
                {formatVoiceName(voice)}
              </option>
            ))}
          </select>
        </div>

        {/* API Key (if using ElevenLabs or OpenAI) */}
        {(voiceSettings.provider === 'elevenlabs' || voiceSettings.provider === 'openai') && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              API Key
            </label>
            <input
              type="password"
              value={voiceSettings.apiKey}
              onChange={(e) => setVoiceSettings(prev => ({ ...prev, apiKey: e.target.value }))}
              placeholder={`Enter your ${voiceSettings.provider === 'elevenlabs' ? 'ElevenLabs' : 'OpenAI'} API key`}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            />
          </div>
        )}

        {/* Save Button */}
        <div className="flex items-center justify-between pt-4">
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {saving ? (
              <InlineSpinner />
            ) : (
              <>
                <Save size={16} className="mr-2" />
                Save Settings
              </>
            )}
          </button>
          
          {saveSuccess && (
            <div className="flex items-center text-green-600">
              <CheckCircle size={16} className="mr-1" />
              <span className="text-sm">Settings saved!</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default DashboardPage;
