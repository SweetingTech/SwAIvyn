import { motion } from 'framer-motion';
import { useState, useEffect } from 'react';
import {
  Activity,
  Database,
  MessageSquare,
  Brain,
  Bot,
  Wifi,
  WifiOff,
  CheckCircle,
  XCircle,
  Clock,
  Users,
  Zap
} from 'lucide-react';
import { useInitialization } from '../contexts/InitializationContext';
import InlineSpinner from '../components/ui/InlineSpinner';

interface SystemStatus {
  llmEngine: string;
  llmModel: string;
  llmConnected: boolean;
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
    return () => clearInterval(interval);
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
          </div>
        )}

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
    </a>
  );
};

export default DashboardPage;
