import axios from 'axios';
import { motion } from 'framer-motion';
import { useCallback, useEffect, useMemo, useState } from 'react';
import useEffectiveUser from '../hooks/useEffectiveUser';
import {
  Bot,
  Plus,
  Play,
  Pause,
  Trash2,
  Activity,
  Clock,
  Zap,
  Brain,
  MessageSquare,
  Search,
  ChevronDown,
  ChevronUp,
  Edit3,
  Settings,
} from 'lucide-react';
import InlineSpinner from '../components/ui/InlineSpinner';
import AgentForm from '../components/AgentForm';

interface AgentEvent {
  id: string;
  timestamp: string;
  status: string;
  message?: string;
  meta?: Record<string, unknown> | null;
}

interface Agent {
  id: string;
  userId?: string | null;
  name?: string | null;
  status: string;
  startedAt?: string | null;
  finishedAt?: string | null;
  updatedAt?: string | null;
  meta?: Record<string, unknown> | null;
  events?: AgentEvent[];
}

const ACTIVE_STATUSES = new Set(['working', 'running']);
const FAILED_STATUSES = new Set(['failed', 'error']);

const isRecord = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value);

const normalizeStatus = (status?: string | null): string =>
  typeof status === 'string' && status.trim() ? status.trim().toLowerCase() : 'unknown';

const normalizeAgent = (agent: Agent): Agent => ({
  ...agent,
  status: normalizeStatus(agent.status),
  meta: isRecord(agent.meta) ? agent.meta : {},
});

const isActiveStatus = (status: string) => ACTIVE_STATUSES.has(normalizeStatus(status));

const getAgentType = (agent: Agent): string => {
  const raw = agent.meta?.type;
  if (typeof raw === 'string' && raw.trim()) return raw.trim();
  return 'custom';
};

const getAgentDescription = (agent: Agent): string => {
  const meta = agent.meta ?? {};
  const candidates = [meta.description, meta.summary, meta.goal, meta.notes];
  const value = candidates.find((entry) => typeof entry === 'string' && entry.trim());
  return typeof value === 'string' ? value.trim() : 'No description provided yet.';
};

const getAgentDisplayName = (agent: Agent): string => {
  if (agent.name && agent.name.trim()) return agent.name;
  const metaName = agent.meta?.name;
  if (typeof metaName === 'string' && metaName.trim()) return metaName.trim();
  return agent.id;
};

const getTypeIcon = (type: string) => {
  const key = type.toLowerCase();
  switch (key) {
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

const getTypeColor = (type: string) => {
  const key = type.toLowerCase();
  switch (key) {
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

const getStatusColor = (status: string) => {
  const key = normalizeStatus(status);
  if (ACTIVE_STATUSES.has(key)) return 'text-emerald-700 bg-emerald-50';
  if (key === 'pending') return 'text-amber-700 bg-amber-50';
  if (key === 'paused' || key === 'idle') return 'text-blue-700 bg-blue-50';
  if (key === 'completed') return 'text-teal-700 bg-teal-50';
  if (FAILED_STATUSES.has(key)) return 'text-red-700 bg-red-50';
  if (key === 'stopped' || key === 'cancelled') return 'text-slate-700 bg-slate-100';
  return 'text-gray-700 bg-gray-100';
};

const formatTimestamp = (value?: string | null): string => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
};

const humanize = (value: string) => {
  if (!value) return value;
  return value.charAt(0).toUpperCase() + value.slice(1);
};

const isNonEmptyObject = (value: unknown): value is Record<string, unknown> =>
  isRecord(value) && Object.keys(value).length > 0;

const extractAgentYaml = (agent: Agent | null): string => {
  if (!agent || !agent.meta) return '';
  const meta = agent.meta as Record<string, unknown>;
  for (const key of ['yaml', 'definition', 'config']) {
    const value = meta[key];
    if (typeof value === 'string') return value;
  }
  return '';
};

const AgentsPage = () => {
  const eff = useEffectiveUser();

  const [agents, setAgents] = useState<Agent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [filterType, setFilterType] = useState<string>('all');
  const [expandedAgent, setExpandedAgent] = useState<string | null>(null);
  const [eventLogs, setEventLogs] = useState<Record<string, AgentEvent[]>>({});
  const [loadingEvents, setLoadingEvents] = useState<Record<string, boolean>>({});

  const [formOpen, setFormOpen] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [formAgent, setFormAgent] = useState<Agent | null>(null);
  const [formYaml, setFormYaml] = useState<string>('');

  const parseErrorMessage = useCallback((err: unknown, fallback: string) => {
    if (axios.isAxiosError(err)) {
      const data = err.response?.data;
      if (typeof data === 'string' && data.trim().length > 0) return data;
      if (data && typeof data === 'object') {
        const detail = (data as { detail?: unknown }).detail;
        if (typeof detail === 'string') return detail;
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
    if (err instanceof Error) return err.message || fallback;
    return fallback;
  }, []);

  const loadAgents = useCallback(async () => {
    if (!eff.userId) return;
    setLoading(true);
    try {
      const resp = await fetch('/api/agents', { headers: eff.headers });
      if (!resp.ok) throw new Error('Failed to fetch agents');
      const data = (await resp.json()) as Agent[];
      setAgents((Array.isArray(data) ? data : []).map((a) => normalizeAgent(a)));
      setError(null);
    } catch (err) {
      console.error('Failed to load agents', err);
      const message = parseErrorMessage(err, 'Failed to load agents.');
      setError(message);
      setAgents([]);
    } finally {
      setLoading(false);
    }
  }, [eff.headers, eff.userId, parseErrorMessage]);

  useEffect(() => {
    if (eff.userId) void loadAgents();
  }, [eff.userId, loadAgents]);

  const fetchAgentDetail = useCallback(
    async (id: string, force = false) => {
      if (!eff.userId) return;
      if (!force) {
        if (loadingEvents[id]) return;
        if (eventLogs[id]) return;
      }
      setLoadingEvents((prev) => ({ ...prev, [id]: true }));
      try {
        const resp = await fetch(`/api/agents/${id}`, { headers: eff.headers });
        if (resp.ok) {
          const detail = (await resp.json()) as Agent;
          const { events = [], ...rest } = detail;
          setEventLogs((prev) => ({ ...prev, [id]: events }));
          setAgents((prev) => prev.map((a) => (a.id === id ? normalizeAgent({ ...a, ...rest }) : a)));
        }
      } catch (err) {
        console.error('Failed to load agent detail', err);
      } finally {
        setLoadingEvents((prev) => ({ ...prev, [id]: false }));
      }
    },
    [eff.headers, eff.userId, eventLogs, loadingEvents],
  );

  const updateAgent = useCallback(
    async (id: string, payload: Record<string, unknown>) => {
      try {
        await fetch(`/api/agents/${id}`, {
          method: 'PATCH',
          headers: { 'Content-Type': 'application/json', ...eff.headers },
          body: JSON.stringify(payload),
        });
        await loadAgents();
        if (expandedAgent === id) {
          setEventLogs((prev) => {
            const next = { ...prev };
            delete next[id];
            return next;
          });
          await fetchAgentDetail(id, true);
        }
      } catch (err) {
        console.error('Failed to update agent', err);
      }
    },
    [eff.headers, loadAgents, expandedAgent, fetchAgentDetail],
  );

  const toggleAgent = (agent: Agent) => {
    const active = isActiveStatus(agent.status);
    const payload: Record<string, unknown> = {
      status: active ? 'paused' : 'working',
      message: active ? 'Paused from dashboard' : 'Started from dashboard',
    };
    if (!active && !agent.startedAt) {
      payload.startedAt = new Date().toISOString();
    }
    void updateAgent(agent.id, payload);
  };

  const deleteAgent = async (id: string) => {
    try {
      const resp = await fetch(`/api/agents/${id}`, { method: 'DELETE', headers: eff.headers });
      if (!resp.ok) throw new Error('delete failed');
      setAgents((prev) => prev.filter((a) => a.id !== id));
      setEventLogs((prev) => {
        const next = { ...prev };
        delete next[id];
        return next;
      });
      setLoadingEvents((prev) => {
        const next = { ...prev };
        delete next[id];
        return next;
      });
      if (expandedAgent === id) setExpandedAgent(null);
    } catch (err) {
      console.error('Failed to delete agent', err);
    }
  };

  const handleToggleDetails = (id: string) => {
    if (expandedAgent === id) {
      setExpandedAgent(null);
      return;
    }
    setExpandedAgent(id);
    void fetchAgentDetail(id);
  };

  const openCreateForm = () => {
    setFormMode('create');
    setFormAgent(null);
    setFormYaml('');
    setFormOpen(true);
    setError(null);
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

  const typeOptions = useMemo(() => {
    const set = new Set<string>();
    agents.forEach((agent) => set.add(getAgentType(agent)));
    return Array.from(set).sort();
  }, [agents]);

  const filteredAgents = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    return agents.filter((agent) => {
      const name = getAgentDisplayName(agent).toLowerCase();
      const description = getAgentDescription(agent).toLowerCase();
      const matchesSearch =
        !term || name.includes(term) || description.includes(term) || agent.id.toLowerCase().includes(term);
      const type = getAgentType(agent);
      const matchesType = filterType === 'all' || type === filterType;
      return matchesSearch && matchesType;
    });
  }, [agents, searchTerm, filterType]);

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
              <p className="text-gray-600">Monitor agent status and review their activity logs</p>
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

        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-4 mb-6">
          <div className="flex flex-col sm:flex-row sm:items-center space-y-3 sm:space-y-0 sm:space-x-4">
            <div className="flex-1">
              <div className="relative">
                <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
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
                {typeOptions.map((type) => (
                  <option key={type} value={type}>
                    {humanize(type)}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {loading && (
            <div className="col-span-full text-center py-12">
              <InlineSpinner />
            </div>
          )}

          {!loading &&
            filteredAgents.map((agent) => {
              const type = getAgentType(agent);
              const active = isActiveStatus(agent.status);
              const events = eventLogs[agent.id] || [];
              const isExpanded = expandedAgent === agent.id;

              return (
                <div key={agent.id} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6 flex flex-col">
                  <div className="flex items-start justify-between mb-4">
                    <div className="flex items-center space-x-3">
                      <div className={`p-2 rounded-lg ${getTypeColor(type)}`}>{getTypeIcon(type)}</div>
                      <div>
                        <h3 className="font-medium text-gray-900">{getAgentDisplayName(agent)}</h3>
                        <div className="flex items-center space-x-2 mt-1">
                          <span className={`px-2 py-1 text-xs font-medium rounded-full ${getStatusColor(agent.status)}`}>
                            {humanize(normalizeStatus(agent.status))}
                          </span>
                          <span className={`px-2 py-1 text-xs font-medium rounded-full ${getTypeColor(type)}`}>
                            {humanize(type)}
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
                      <button className="p-1 text-gray-400 hover:text-gray-600 rounded" title="Settings">
                        <Settings size={14} />
                      </button>
                    </div>
                  </div>

                  <p className="text-sm text-gray-600 mb-4 line-clamp-3">{getAgentDescription(agent)}</p>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm text-gray-700 mb-4">
                    <div>
                      <p className="text-xs uppercase tracking-wide text-gray-500">Started</p>
                      <p className="font-medium">{formatTimestamp(agent.startedAt)}</p>
                    </div>
                    <div>
                      <p className="text-xs uppercase tracking-wide text-gray-500">Last Update</p>
                      <p className="font-medium">{formatTimestamp(agent.updatedAt)}</p>
                    </div>
                    <div>
                      <p className="text-xs uppercase tracking-wide text-gray-500">Finished</p>
                      <p className="font-medium">{formatTimestamp(agent.finishedAt)}</p>
                    </div>
                  </div>

                  <div className="mt-auto">
                    <div className="flex items-center justify-between pt-4 border-t border-gray-200">
                      <div className="flex items-center space-x-2">
                        <button
                          onClick={() => toggleAgent(agent)}
                          className={`p-2 rounded-md transition-colors ${
                            active
                              ? 'text-emerald-700 bg-emerald-50 hover:bg-emerald-100'
                              : 'text-gray-700 bg-gray-100 hover:bg-gray-200'
                          }`}
                          title={active ? 'Pause Agent' : 'Start Agent'}
                        >
                          {active ? <Pause size={16} /> : <Play size={16} />}
                        </button>
                        <button
                          onClick={() => deleteAgent(agent.id)}
                          className="p-2 rounded-md text-red-600 bg-red-50 hover:bg-red-100"
                          title="Delete Agent"
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                      <button
                        className="text-sm text-primary-600 hover:text-primary-700 font-medium flex items-center"
                        onClick={() => handleToggleDetails(agent.id)}
                      >
                        {isExpanded ? <ChevronUp size={14} className="mr-1" /> : <ChevronDown size={14} className="mr-1" />}
                        Activity Log
                      </button>
                    </div>

                    {isExpanded && (
                      <div className="mt-4 border-t border-gray-200 pt-4">
                        {loadingEvents[agent.id] ? (
                          <div className="flex justify-center py-4">
                            <InlineSpinner />
                          </div>
                        ) : events.length > 0 ? (
                          <ul className="space-y-3 max-h-64 overflow-y-auto pr-1">
                            {[...events].reverse().map((event) => (
                              <li key={event.id} className="border border-gray-200 rounded-md p-3 bg-gray-50">
                                <div className="flex items-start justify-between">
                                  <div>
                                    <p className="text-xs text-gray-500">{formatTimestamp(event.timestamp)}</p>
                                    {event.message && (
                                      <p className="text-sm text-gray-700 mt-1">{event.message}</p>
                                    )}
                                  </div>
                                  <span className={`px-2 py-1 text-xs font-medium rounded-full ${getStatusColor(event.status)}`}>
                                    {humanize(normalizeStatus(event.status))}
                                  </span>
                                </div>
                                {isNonEmptyObject(event.meta) && (
                                  <pre className="mt-2 text-xs text-gray-600 bg-white border border-gray-200 rounded p-2 overflow-x-auto">
                                    {JSON.stringify(event.meta, null, 2)}
                                  </pre>
                                )}
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <p className="text-sm text-gray-600">No events recorded yet.</p>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
        </div>

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

        <div className="mt-8 bg-blue-50 border border-blue-200 rounded-lg p-6">
          <div className="flex items-center space-x-3">
            <Zap size={24} className="text-blue-600" />
            <div>
              <h3 className="text-lg font-medium text-blue-900">Coming Soon</h3>
              <p className="text-blue-700">
                Native agent functionality is evolving. Status tracking and event logs are now available while advanced
                automation features continue to roll out.
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
