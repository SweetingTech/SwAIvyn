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
  Zap,
  HardDrive,
  Network,
  FileText
} from 'lucide-react';
import { useInitialization } from '../contexts/InitializationContext';

interface SystemStatus {
  llmEngine: string;
  llmModel: string;
  llmConnected: boolean;
  charactersLoaded: number;
  memoryItems: number;
  conversationChunks: number;
  chatSessions: number;
  neo4jConnected: boolean;
  weaviateConnected: boolean;
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
    conversationChunks: 0,
    chatSessions: 0,
    neo4jConnected: false,
    weaviateConnected: false,
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
      // Use user from initialization context
      const userId = user?.id || '00000000-0000-0000-0000-000000000001';
      console.log('🔍 Dashboard: About to call LLM settings API with userId:', userId);
      const llmUrl = `/api/settings/llm?userId=${userId}`;
      console.log('🔍 Dashboard: Full URL:', llmUrl);
      const llmResponse = await fetch(llmUrl);
      console.log('🔍 Dashboard: LLM API response status:', llmResponse.status);
      console.log('🔍 Dashboard: LLM API response ok:', llmResponse.ok);
      const llmData = llmResponse.ok ? await llmResponse.json() : { engine: 'Unknown', model: 'Unknown' };
      console.log('🔍 Dashboard: LLM API response data:', llmData);

      // Get the actual current model from the respective service
      let actualModel = 'Unknown';
      let llmConnected = false;

      try {
        if (llmData.engine === 'ollama') {
          const testResponse = await fetch('/api/llm/ollama/models');
          llmConnected = testResponse.ok;
          if (testResponse.ok) {
            // For Ollama, use the model from database settings
            actualModel = llmData.model || 'Not selected';
          }
        } else if (llmData.engine === 'lmstudio') {
          const testResponse = await fetch('/api/llm/lmstudio/models');
          llmConnected = testResponse.ok;
          if (testResponse.ok) {
            // For LM Studio, get the actual loaded model from the API
            const modelsData = await testResponse.json();
            if (modelsData.data && modelsData.data.length > 0) {
              actualModel = modelsData.data[0].id; // Use the first (current) model
            } else {
              actualModel = 'No model loaded';
            }
          }
        }
      } catch {
        llmConnected = false;
        actualModel = 'Connection failed';
      }

      // Get character count - try global characters first, then user-specific
      let charactersLoaded = 0;
      try {
        // First try global characters (UserId = Guid.Empty)
        const globalCharResponse = await fetch('/api/character/global');
        if (globalCharResponse.ok) {
          const globalCharacters = await globalCharResponse.json();
          charactersLoaded += Array.isArray(globalCharacters) ? globalCharacters.length : 0;
        }

        // Then try user-specific characters
        const userCharResponse = await fetch(`/api/character/user/${userId}`);
        if (userCharResponse.ok) {
          const userCharacters = await userCharResponse.json();
          charactersLoaded += Array.isArray(userCharacters) ? userCharacters.length : 0;
        }
      } catch {
        charactersLoaded = 0;
      }

      // Get memory counts from the memory API
      let memoryItems = 0;
      let conversationChunks = 0;
      try {
        const memoryResponse = await fetch('/api/memory');
        if (memoryResponse.ok) {
          const memoryData = await memoryResponse.json();
          if (memoryData.memories && Array.isArray(memoryData.memories)) {
            // Count explicit memories (not conversation chunks)
            memoryItems = memoryData.memories.filter(m => m.category !== 'conversation-chunk').length;
            // Count conversation chunks separately
            conversationChunks = memoryData.memories.filter(m => m.category === 'conversation-chunk').length;
          }
        }
      } catch {
        memoryItems = 0;
        conversationChunks = 0;
      }

      // Get vector store health status
      let neo4jConnected = false;
      let weaviateConnected = false;
      try {
        const healthResponse = await fetch('/api/healthcheck');
        if (healthResponse.ok) {
          const healthData = await healthResponse.json();
          neo4jConnected = healthData.services?.neo4j?.status === 'online';
          weaviateConnected = healthData.services?.weaviate?.status === 'online';
        }
      } catch {
        // Fallback to individual health checks
        try {
          const neo4jResponse = await fetch('/api/healthcheck/neo4j');
          neo4jConnected = neo4jResponse.ok;
        } catch {}

        try {
          const weaviateResponse = await fetch('/api/healthcheck/weaviate');
          weaviateConnected = weaviateResponse.ok;
        } catch {}
      }

      // Get chat sessions count
      let chatSessions = 0;
      try {
        const conversationsResponse = await fetch(`/api/conversation/user/${userId}`);
        if (conversationsResponse.ok) {
          const conversations = await conversationsResponse.json();
          chatSessions = Array.isArray(conversations) ? conversations.length : 0;
        }
      } catch {
        chatSessions = 0;
      }

      setSystemStatus({
        llmEngine: llmData.engine || 'Unknown',
        llmModel: llmData.model || 'Not selected', // Use the database model setting
        llmConnected,
        charactersLoaded,
        memoryItems,
        conversationChunks,
        chatSessions,
        neo4jConnected,
        weaviateConnected,
        uptime: calculateUptime(),
        lastActivity: new Date().toLocaleTimeString()
      });
    } catch (error) {
      console.error('Error loading system status:', error);
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
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600"></div>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
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

            {/* Memory Items (Neo4j) */}
            <StatusCard
              title="Memories"
              value={systemStatus.memoryItems.toString()}
              subtitle="Explicit memories (Neo4j)"
              icon={<Brain size={24} />}
              status={systemStatus.neo4jConnected ? 'connected' : 'disconnected'}
              statusIcon={systemStatus.neo4jConnected ? <CheckCircle size={16} /> : <XCircle size={16} />}
            />

            {/* Conversation Chunks (Weaviate) */}
            <StatusCard
              title="Conversations"
              value={systemStatus.conversationChunks.toString()}
              subtitle="Chat history (Weaviate)"
              icon={<MessageSquare size={24} />}
              status={systemStatus.weaviateConnected ? 'connected' : 'disconnected'}
              statusIcon={systemStatus.weaviateConnected ? <CheckCircle size={16} /> : <XCircle size={16} />}
            />

            {/* Neo4j Status */}
            <StatusCard
              title="Neo4j"
              value={systemStatus.neo4jConnected ? 'Online' : 'Offline'}
              subtitle="Brain memories & graph"
              icon={<Network size={24} />}
              status={systemStatus.neo4jConnected ? 'connected' : 'disconnected'}
              statusIcon={systemStatus.neo4jConnected ? <CheckCircle size={16} /> : <XCircle size={16} />}
            />

            {/* Weaviate Status */}
            <StatusCard
              title="Weaviate"
              value={systemStatus.weaviateConnected ? 'Online' : 'Offline'}
              subtitle="Documents & conversations"
              icon={<FileText size={24} />}
              status={systemStatus.weaviateConnected ? 'connected' : 'disconnected'}
              statusIcon={systemStatus.weaviateConnected ? <CheckCircle size={16} /> : <XCircle size={16} />}
            />

            {/* Chat Sessions */}
            <StatusCard
              title="Chat Sessions"
              value={systemStatus.chatSessions.toString()}
              subtitle="Total conversations"
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

        {/* System Architecture Overview */}
        <div className="mt-8">
          <h2 className="text-lg font-medium text-gray-800 mb-4">System Architecture</h2>
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              <div className="text-center">
                <div className="p-3 rounded-lg bg-blue-50 text-blue-600 mx-auto w-fit mb-3">
                  <HardDrive size={32} />
                </div>
                <h3 className="font-medium text-gray-900 mb-2">SQLite</h3>
                <p className="text-sm text-gray-600">Source of truth for all structured data, settings, and metadata</p>
              </div>
              <div className="text-center">
                <div className="p-3 rounded-lg bg-green-50 text-green-600 mx-auto w-fit mb-3">
                  <Network size={32} />
                </div>
                <h3 className="font-medium text-gray-900 mb-2">Neo4j</h3>
                <p className="text-sm text-gray-600">Brain memories with vector search and graph relationships</p>
              </div>
              <div className="text-center">
                <div className="p-3 rounded-lg bg-purple-50 text-purple-600 mx-auto w-fit mb-3">
                  <FileText size={32} />
                </div>
                <h3 className="font-medium text-gray-900 mb-2">Weaviate</h3>
                <p className="text-sm text-gray-600">Document knowledge and conversation history storage</p>
              </div>
            </div>
            <div className="mt-6 text-center">
              <p className="text-sm text-gray-500">
                <strong>Three-Database Harmony:</strong> Each database serves a specific purpose for optimal performance and data organization
              </p>
            </div>
          </div>
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
    </a>
  );
};

export default DashboardPage;
