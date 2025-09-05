import { motion } from 'framer-motion';
import { useState, useEffect } from 'react';
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
import InlineSpinner from '../components/ui/InlineSpinner';
import ttsService from '../services/ttsService';

interface SystemStatus {
  llmEngine: string;
  llmModel: string;
  llmConnected: boolean;
  ttsProvider: string;
  ttsVoice: string;
  ttsConnected: boolean;
  charactersLoaded: number;
  memoryItems: number;
  chatSessions: number;
  uptime: string;
  lastActivity: string;
}

const DashboardPage = () => {
  const { user } = useInitialization();
  const [systemStatus, setSystemStatus] = useState<SystemStatus>({
    llmEngine: 'Loading...',
    llmModel: 'Loading...',
    llmConnected: false,
    ttsProvider: 'Loading...',
    ttsVoice: 'Loading...',
    ttsConnected: false,
    charactersLoaded: 0,
    memoryItems: 0,
    chatSessions: 0,
    uptime: '0m',
    lastActivity: 'Never'
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadSystemStatus();
    // Refresh status every 30 seconds
    const interval = setInterval(loadSystemStatus, 30000);

    // Listen for LLM settings changes from chat page
    const handleLlmSettingsChange = (event: CustomEvent) => {
      console.log('🔍 Dashboard: LLM settings changed, refreshing status:', event.detail);
      loadSystemStatus();
    };

    window.addEventListener('llmSettingsChanged', handleLlmSettingsChange as EventListener);

    return () => {
      clearInterval(interval);
      window.removeEventListener('llmSettingsChanged', handleLlmSettingsChange as EventListener);
    };
  }, []);

  const loadSystemStatus = async () => {
    try {
      console.log('🔍 Dashboard: Loading system status...');

      // Get comprehensive system status from new dashboard API
      const statusResponse = await fetch('/api/dashboard/status');

      if (statusResponse.ok) {
        const statusData = await statusResponse.json();
        console.log('🔍 Dashboard: Status data received:', statusData);

        setSystemStatus({
          llmEngine: statusData.llm?.engine || 'Unknown',
          llmModel: statusData.llm?.model || 'Not selected',
          llmConnected: statusData.llm?.connected || false,
          ttsProvider: statusData.tts?.provider || 'Unknown',
          ttsVoice: statusData.tts?.voice || 'Not selected',
          ttsConnected: statusData.tts?.connected || false,
          charactersLoaded: statusData.metrics?.characterCount || 0,
          memoryItems: statusData.metrics?.memoryCount || 0,
          chatSessions: statusData.metrics?.conversationCount || 0,
          uptime: calculateUptime(),
          lastActivity: new Date().toLocaleTimeString()
        });
      } else {
        console.error('🔍 Dashboard: Failed to load status, falling back to individual APIs');

        // Fallback to individual API calls if dashboard API fails
        const userId = user?.id || '00000000-0000-0000-0000-000000000001';

        // Get LLM settings
        let llmData = { engine: 'Unknown', model: 'Unknown' };
        let llmConnected = false;

        try {
          const llmResponse = await fetch(`/api/settings/llm?userId=${userId}`);
          if (llmResponse.ok) {
            llmData = await llmResponse.json();

            // Test LLM connection
            if (llmData.engine === 'ollama') {
              const testResponse = await fetch('/api/llm/ollama/models');
              llmConnected = testResponse.ok;
            } else if (llmData.engine === 'lmstudio') {
              const testResponse = await fetch('/api/llm/lmstudio/models');
              llmConnected = testResponse.ok;
            } else if (llmData.engine === 'vllm') {
              const testResponse = await fetch('/api/llm/models?engine=vllm');
              llmConnected = testResponse.ok;
            }
          }
        } catch (error) {
          console.error('Failed to get LLM settings:', error);
        }

        // Get character count
        let charactersLoaded = 0;
        try {
          const charResponse = await fetch(`/api/character/user/${userId}`);
          if (charResponse.ok) {
            const characters = await charResponse.json();
            charactersLoaded = Array.isArray(characters) ? characters.length : 0;
          }
        } catch (error) {
          console.error('Failed to get character count:', error);
        }

        setSystemStatus({
          llmEngine: llmData.engine,
          llmModel: llmData.model,
          llmConnected,
          ttsProvider: 'Unknown', // Fallback when dashboard API fails
          ttsVoice: 'Unknown',
          ttsConnected: false,
          charactersLoaded,
          memoryItems: 0, // Will be available when dashboard API works
          chatSessions: 0, // Will be available when dashboard API works
          uptime: calculateUptime(),
          lastActivity: new Date().toLocaleTimeString()
        });
      }
    } catch (error) {
      console.error('Error loading system status:', error);

      // Set default values on error
      setSystemStatus({
        llmEngine: 'Error',
        llmModel: 'Error',
        llmConnected: false,
        ttsProvider: 'Error',
        ttsVoice: 'Error',
        ttsConnected: false,
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
              status={systemStatus.llmConnected ? 'connected' : 'disconnected'}
              statusIcon={systemStatus.llmConnected ? <CheckCircle size={16} /> : <XCircle size={16} />}
            />

            {/* TTS Status */}
            <StatusCard
              title="TTS Provider"
              value={systemStatus.ttsProvider}
              subtitle={systemStatus.ttsVoice}
              icon={<Mic size={24} />}
              status={systemStatus.ttsConnected ? 'connected' : 'disconnected'}
              statusIcon={systemStatus.ttsConnected ? <CheckCircle size={16} /> : <XCircle size={16} />}
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

        {/* Voice Settings Section */}
        <div className="mt-8">
          <h2 className="text-lg font-medium text-gray-800 mb-4">Voice Settings</h2>
          <VoiceSettingsCard userId={user?.id} />
        </div>

        {/* Quick Actions */}
        <div className="mt-8">
          <h2 className="text-lg font-medium text-gray-800 mb-4">Quick Actions</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <QuickActionCard
              title="Start Chat"
              description="Begin a new conversation"
              icon={<MessageSquare size={20} />}
              href="/chat/new"
            />
            <QuickActionCard
              title="Manage Characters"
              description="Edit AI personalities"
              icon={<Bot size={20} />}
              href="/settings"
            />
            <QuickActionCard
              title="View Memory"
              description="Browse stored memories"
              icon={<Brain size={20} />}
              href="/memory"
            />
            <QuickActionCard
              title="System Settings"
              description="Configure the system"
              icon={<Database size={20} />}
              href="/settings"
            />
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
