import { motion } from 'framer-motion';
import { useState, useEffect } from 'react';
import { useInitialization } from '../contexts/InitializationContext';
import {
  Bot,
  Plus,
  Settings,
  Play,
  Pause,
  Trash2,
  Edit3,
  Activity,
  Clock,
  Zap,
  Brain,
  MessageSquare,
  Search,
} from 'lucide-react';
import InlineSpinner from '../components/ui/InlineSpinner';

interface Agent {
  id: string;
  userId: string;
  name: string;
  description: string;
  type: string;
  status: string;
  lastRun: string | null;
  tasksCompleted: number;
  enabled: boolean;
}

const AgentsPage = () => {
  const { user } = useInitialization();
  const [agents, setAgents] = useState<Agent[]>([]);
  const [loading, setLoading] = useState(true);

  // Fetch agents whenever the user becomes available
  useEffect(() => {
    if (user?.id) {
      loadAgents();
    }
  }, [user?.id]);

  const loadAgents = async () => {
    if (!user?.id) return;
    setLoading(true);

    try {
      const resp = await fetch(`/api/agents?userId=${user.id}`);
      if (resp.ok) {
        const data: Agent[] = await resp.json();
        setAgents(data);
      } else {
        setAgents([]);
      }
    } catch (err) {
      console.error('Failed to load agents', err);
      setAgents([]);
    } finally {
      setLoading(false);
    }
  };

  const startAgent = async (id: string) => {
    await fetch(`/api/agents/${id}/start`, { method: 'POST' });
    loadAgents();
  };

  const stopAgent = async (id: string) => {
    await fetch(`/api/agents/${id}/stop`, { method: 'POST' });
    loadAgents();
  };

  const deleteAgent = async (id: string) => {
    await fetch(`/api/agents/${id}`, { method: 'DELETE' });
    setAgents((prev) => prev.filter((a) => a.id !== id));
  };

  const [searchTerm, setSearchTerm] = useState('');
  const [filterType, setFilterType] = useState<string>('all');

  const filteredAgents = agents.filter((agent) => {
    const matchesSearch =
      agent.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      agent.description.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesType = filterType === 'all' || agent.type === filterType;
    return matchesSearch && matchesType;
  });

  const getTypeIcon = (type: string) => {
    switch (type) {
      case 'task':
        return <Clock size={16} />;
      case 'monitoring':
        return <Activity size={16} />;
      case 'analysis':
        return <Brain size={16} />;
      case 'communication':
        return <MessageSquare size={16} />;
      default:
        return <Bot size={16} />;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'active':
        return 'text-green-600 bg-green-50';
      case 'inactive':
        return 'text-gray-600 bg-gray-50';
      case 'error':
        return 'text-red-600 bg-red-50';
      default:
        return 'text-gray-600 bg-gray-50';
    }
  };

  const getTypeColor = (type: string) => {
    switch (type) {
      case 'task':
        return 'text-blue-600 bg-blue-50';
      case 'monitoring':
        return 'text-purple-600 bg-purple-50';
      case 'analysis':
        return 'text-orange-600 bg-orange-50';
      case 'communication':
        return 'text-green-600 bg-green-50';
      default:
        return 'text-gray-600 bg-gray-50';
    }
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
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h1 className="text-2xl font-medium text-gray-800">Native Agents</h1>
              <p className="text-gray-600">Manage built-in AI agents and automation</p>
            </div>
            <button className="btn btn-primary mt-2 sm:mt-0 flex items-center">
              <Plus size={16} className="mr-1.5" />
              Create Agent
            </button>
          </div>
        </div>

        {/* Filters & Search */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-4 mb-6">
          <div className="flex flex-col sm:flex-row sm:items-center space-y-3 sm:space-y-0 sm:space-x-4">
            <div className="flex-1">
              <div className="relative">
                <Search size={16} className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                <input
                  type="text"
                  placeholder="Search agents..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
            </div>
            <div>
              <select
                value={filterType}
                onChange={(e) => setFilterType(e.target.value)}
                className="border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
              >
                <option value="all">All Types</option>
                <option value="task">Task</option>
                <option value="monitoring">Monitoring</option>
                <option value="analysis">Analysis</option>
                <option value="communication">Communication</option>
              </select>
            </div>
          </div>
        </div>

        {/* Agents Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {loading && (
            <div className="col-span-full text-center py-12">
              <InlineSpinner />
            </div>
          )}

          {!loading &&
            filteredAgents.map((agent) => (
              <div key={agent.id} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                {/* Header */}
                <div className="flex items-start justify-between mb-4">
                  <div className="flex items-center space-x-3">
                    <div className={`p-2 rounded-lg ${getTypeColor(agent.type)}`}>{getTypeIcon(agent.type)}</div>
                    <div>
                      <h3 className="font-medium text-gray-900">{agent.name}</h3>
                      <div className="flex items-center space-x-2 mt-1">
                        <span className={`px-2 py-1 text-xs font-medium rounded-full ${getStatusColor(agent.status)}`}>
                          {agent.status}
                        </span>
                        <span className={`px-2 py-1 text-xs font-medium rounded-full ${getTypeColor(agent.type)}`}>
                          {agent.type}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center space-x-1">
                    <button className="p-1 text-gray-400 hover:text-gray-600 rounded">
                      <Edit3 size={14} />
                    </button>
                    <button className="p-1 text-gray-400 hover:text-gray-600 rounded">
                      <Settings size={14} />
                    </button>
                  </div>
                </div>

                {/* Description */}
                <p className="text-sm text-gray-600 mb-4">{agent.description}</p>

                {/* Stats */}
                <div className="grid grid-cols-2 gap-4 mb-4">
                  <div>
                    <p className="text-xs text-gray-500">Last Run</p>
                    <p className="text-sm font-medium text-gray-900">{agent.lastRun || '—'}</p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">Tasks Completed</p>
                    <p className="text-sm font-medium text-gray-900">{agent.tasksCompleted}</p>
                  </div>
                </div>

                {/* Controls */}
                <div className="flex items-center justify-between pt-4 border-t border-gray-200">
                  <div className="flex items-center space-x-2">
                    <button
                      onClick={() => (agent.enabled ? stopAgent(agent.id) : startAgent(agent.id))}
                      className={`p-2 rounded-md ${
                        agent.enabled
                          ? 'text-green-600 bg-green-50 hover:bg-green-100'
                          : 'text-gray-600 bg-gray-50 hover:bg-gray-100'
                      }`}
                      title={agent.enabled ? 'Pause Agent' : 'Start Agent'}
                    >
                      {agent.enabled ? <Pause size={16} /> : <Play size={16} />}
                    </button>
                    <button
                      onClick={() => deleteAgent(agent.id)}
                      className="p-2 rounded-md text-red-600 bg-red-50 hover:bg-red-100"
                      title="Delete Agent"
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                  <button className="text-sm text-primary-600 hover:text-primary-700 font-medium">View Details</button>
                </div>
              </div>
            ))}
        </div>

        {/* Empty State */}
        {!loading && filteredAgents.length === 0 && (
          <div className="text-center py-12">
            <Bot size={48} className="mx-auto text-gray-400 mb-4" />
            <h3 className="text-lg font-medium text-gray-900 mb-2">No agents found</h3>
            <p className="text-gray-600 mb-4">
              {searchTerm || filterType !== 'all'
                ? 'Try adjusting your search or filter criteria.'
                : 'Create your first agent to get started with automation.'}
            </p>
            <button className="btn btn-primary">
              <Plus size={16} className="mr-1.5" />
              Create Agent
            </button>
          </div>
        )}

        {/* Coming Soon Notice */}
        <div className="mt-8 bg-blue-50 border border-blue-200 rounded-lg p-6">
          <div className="flex items-center space-x-3">
            <Zap size={24} className="text-blue-600" />
            <div>
              <h3 className="text-lg font-medium text-blue-900">Coming Soon</h3>
              <p className="text-blue-700">
                Native agent functionality is currently in development. This page shows a preview of the planned features. Agents will
                be able to perform automated tasks, monitor system status, and enhance your AI experience.
              </p>
            </div>
          </div>
        </div>
      </div>
    </motion.div>
  );
};

export default AgentsPage;
