import axios from 'axios';
import { motion } from 'framer-motion';
import { useCallback, useEffect, useMemo, useState } from 'react';
import useEffectiveUser from '../hooks/useEffectiveUser';
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
import AgentForm from '../components/AgentForm';
import type { Agent } from '../services/agentService';
import {
  deleteAgentDefinition,
  getAgents as fetchAgents,
  startAgent as startAgentRequest,
  stopAgent as stopAgentRequest,
} from '../services/agentService';

const AgentsPage = () => {
  const eff = useEffectiveUser();
  const [agents, setAgents] = useState<Agent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [formAgent, setFormAgent] = useState<Agent | null>(null);
  const [formYaml, setFormYaml] = useState<string>('');

  // Fetch agents whenever the user becomes available
  const parseErrorMessage = useCallback((err: unknown, fallback: string) => {
    if (axios.isAxiosError(err)) {
      const data = err.response?.data;
      if (typeof data === 'string' && data.trim().length > 0) {
        return data;
      }
      if (data && typeof data === 'object') {
        const detail = (data as { detail?: unknown }).detail;
        if (typeof detail === 'string') {
          return detail;
        }
        if (detail) {
          try {
            return JSON.stringify(detail);
          } catch {
            return fallback;
          }
        }
      }
      return err.message || fallback;
    }
    if (err instanceof Error) {
      return err.message || fallback;
    }
    return fallback;
  }, []);

  const loadAgents = useCallback(async () => {
    if (!eff.userId) return;
    setLoading(true);
    try {
      const data = await fetchAgents();
      setAgents(Array.isArray(data) ? data : []);
      setError(null);
    } catch (err) {
      console.error('Failed to load agents', err);
      const message = parseErrorMessage(err, 'Failed to load agents.');
      setError(message);
      setAgents([]);
    } finally {
      setLoading(false);
    }
  }, [eff.userId, parseErrorMessage]);

  useEffect(() => {
    if (eff.userId) {
      loadAgents();
    }
  }, [eff.userId, loadAgents]);

  const startAgent = async (id: string) => {
    setError(null);
    try {
      await startAgentRequest(id);
      await loadAgents();
    } catch (err) {
      const message = parseErrorMessage(err, 'Failed to start agent.');
      setError(message);
    }
  };

  const stopAgent = async (id: string) => {
    setError(null);
    try {
      await stopAgentRequest(id);
      await loadAgents();
    } catch (err) {
      const message = parseErrorMessage(err, 'Failed to stop agent.');
      setError(message);
    }
  };

  const deleteAgent = async (id: string) => {
    const confirmed = window.confirm('Are you sure you want to delete this agent?');
    if (!confirmed) return;
    setError(null);
    try {
      await deleteAgentDefinition(id);
      await loadAgents();
      setError(null);
    } catch (err) {
      const message = parseErrorMessage(err, 'Failed to delete agent.');
      setError(message);
    }
  };

  const openCreateForm = () => {
    setFormMode('create');
    setFormAgent(null);
    setFormYaml('');
    setFormOpen(true);
    setError(null);
  };

  const extractAgentYaml = (agent: Agent | null): string => {
    if (!agent || !agent.meta) return '';
    const meta = agent.meta as Record<string, unknown>;
    for (const key of ['yaml', 'definition', 'config']) {
      const value = meta[key];
      if (typeof value === 'string') {
        return value;
      }
    }
    return '';
  };

  const openEditForm = (agent: Agent) => {
    setFormMode('edit');
    setFormAgent(agent);
    setFormYaml(extractAgentYaml(agent));
    setFormOpen(true);
    setError(null);
  };

  const closeForm = () => {
    setFormOpen(false);
    setFormAgent(null);
    setFormYaml('');
  };

  const [searchTerm, setSearchTerm] = useState('');
  const [filterType, setFilterType] = useState<string>('all');

  const filteredAgents = useMemo(() => {
    const searchLower = searchTerm.toLowerCase();
    return agents.filter((agent) => {
      const name = (agent.name || agent.id || '').toLowerCase();
      const description = (agent.description || '').toLowerCase();
      const matchesSearch = name.includes(searchLower) || description.includes(searchLower);
      const normalizedType = (agent.type || '').toLowerCase();
      const matchesType = filterType === 'all' || normalizedType === filterType;
      return matchesSearch && matchesType;
    });
  }, [agents, filterType, searchTerm]);

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
      case 'running':
      case 'active':
        return 'text-green-600 bg-green-50';
      case 'completed':
        return 'text-blue-600 bg-blue-50';
      case 'failed':
      case 'error':
        return 'text-red-600 bg-red-50';
      case 'pending':
        return 'text-amber-600 bg-amber-50';
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
            <button className="btn btn-primary mt-2 sm:mt-0 flex items-center" onClick={openCreateForm}>
              <Plus size={16} className="mr-1.5" />
              Create Agent
            </button>
          </div>
        </div>

        {error && (
          <div className="mb-6 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        )}

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
            filteredAgents.map((agent) => {
              const agentType = (agent.type || '').toLowerCase();
              const status = (agent.status || (agent.enabled ? 'running' : 'inactive') || '').toLowerCase();
              const description = agent.description || '';
              const lastRun = agent.lastRun || agent.finishedAt || agent.startedAt || '—';
              const tasksCompleted = typeof agent.tasksCompleted === 'number' ? agent.tasksCompleted : 0;
              const isRunning = typeof agent.enabled === 'boolean' ? agent.enabled : status === 'running';
              const displayStatus = status ? status.charAt(0).toUpperCase() + status.slice(1) : 'Inactive';
              const displayType = agentType ? agentType.charAt(0).toUpperCase() + agentType.slice(1) : 'Custom';
              return (
                <div key={agent.id} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                  {/* Header */}
                  <div className="flex items-start justify-between mb-4">
                    <div className="flex items-center space-x-3">
                      <div className={`p-2 rounded-lg ${getTypeColor(agentType)}`}>{getTypeIcon(agentType)}</div>
                      <div>
                        <h3 className="font-medium text-gray-900">{agent.name || agent.id}</h3>
                        <div className="flex items-center space-x-2 mt-1">
                          <span className={`px-2 py-1 text-xs font-medium rounded-full ${getStatusColor(status)}`}>
                            {displayStatus}
                          </span>
                          <span className={`px-2 py-1 text-xs font-medium rounded-full ${getTypeColor(agentType)}`}>
                            {displayType}
                          </span>
                        </div>
                      </div>
                    </div>
                    <div className="flex items-center space-x-1">
                      <button
                        className="p-1 text-gray-400 hover:text-gray-600 rounded"
                        onClick={() => openEditForm(agent)}
                        title="Edit agent definition"
                      >
                        <Edit3 size={14} />
                      </button>
                      <button className="p-1 text-gray-400 hover:text-gray-600 rounded">
                        <Settings size={14} />
                      </button>
                    </div>
                </div>

                {/* Description */}
                <p className="text-sm text-gray-600 mb-4">{description || 'No description provided.'}</p>

                {/* Stats */}
                <div className="grid grid-cols-2 gap-4 mb-4">
                  <div>
                    <p className="text-xs text-gray-500">Last Run</p>
                    <p className="text-sm font-medium text-gray-900">{lastRun || '—'}</p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">Tasks Completed</p>
                    <p className="text-sm font-medium text-gray-900">{tasksCompleted}</p>
                  </div>
                </div>

                {/* Controls */}
                <div className="flex items-center justify-between pt-4 border-t border-gray-200">
                  <div className="flex items-center space-x-2">
                    <button
                      onClick={() => (isRunning ? stopAgent(agent.id) : startAgent(agent.id))}
                      className={`p-2 rounded-md ${
                        isRunning
                          ? 'text-green-600 bg-green-50 hover:bg-green-100'
                          : 'text-gray-600 bg-gray-50 hover:bg-gray-100'
                      }`}
                      title={isRunning ? 'Pause Agent' : 'Start Agent'}
                    >
                      {isRunning ? <Pause size={16} /> : <Play size={16} />}
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
              );
            })}
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
            <button className="btn btn-primary" onClick={openCreateForm}>
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

      <AgentForm
        open={formOpen}
        mode={formMode}
        agentId={formMode === 'edit' ? formAgent?.id : undefined}
        initialYaml={formMode === 'edit' ? formYaml : ''}
        onClose={closeForm}
        onSaved={loadAgents}
      />
    </motion.div>
  );
};

export default AgentsPage;
