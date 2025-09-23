// src/services/agentService.ts
import axios from "axios";
import { withApiBase } from '../config';

export interface Agent {
  id: string;
  name?: string | null;
  status: string;
  userId?: string | null;
  startedAt?: string | null;
  finishedAt?: string | null;
  updatedAt?: string | null;
  meta?: Record<string, unknown> | null;
  // Additional frontend properties (may come from meta or separate sources)
  description?: string;
  type?: string;
  lastRun?: string | null;
  tasksCompleted?: number;
  enabled?: boolean;
  goal?: string;
}

export interface AgentCatalogItem {
  id: string;
  name: string;
  type: string;
  summary?: string;
  version?: string;
  status?: string;
  capabilities?: Record<string, unknown>;
  file_path?: string;
}

/**
 * Fetch all agents from SwAIvyn backend
 */
export const getAgents = async (): Promise<Agent[]> => {
  const response = await axios.get<Agent[]>(withApiBase('/api/agents'));
  return response.data;
};

/**
 * Tell the backend to start a specific agent
 */
export const startAgent = async (id: string, message = 'Started via API'): Promise<void> => {
  await axios.patch(withApiBase(`/api/agents/${id}`), {
    status: 'working',
    message,
    startedAt: new Date().toISOString(),
  });
};

/**
 * Tell the backend to stop a specific agent
 */
export const stopAgent = async (id: string, status: string = 'paused', message = 'Stopped via API'): Promise<void> => {
  await axios.patch(withApiBase(`/api/agents/${id}`), {
    status,
    message,
    finishedAt: status === 'completed' || status === 'failed' ? new Date().toISOString() : undefined,
  });
};

/**
 * Fetch the available agent catalog from the Workers orchestrator via the BFF proxy
 */
export const getAgentCatalog = async (): Promise<AgentCatalogItem[]> => {
  const response = await axios.get<AgentCatalogItem[]>(withApiBase('/api/agents/catalog'));
  return response.data;
};

/**
 * Create a new agent definition in the workers orchestrator via the BFF
 */
export const createAgentDefinition = async (yaml: string, agentId?: string) => {
  const suffix = agentId ? `?agentId=${encodeURIComponent(agentId)}` : '';
  const response = await axios.post(withApiBase(`/api/agents${suffix}`), yaml, {
    headers: { 'Content-Type': 'application/x-yaml' },
  });
  return response.data;
};

/**
 * Update an existing agent definition
 */
export const updateAgentDefinition = async (id: string, yaml: string) => {
  const response = await axios.put(withApiBase(`/api/agents/${encodeURIComponent(id)}`), yaml, {
    headers: { 'Content-Type': 'application/x-yaml' },
  });
  return response.data;
};

/**
 * Delete an agent definition. Optionally include YAML payload when required by workers.
 */
export const deleteAgentDefinition = async (id: string, yaml?: string) => {
  const headers: Record<string, string> = {};
  if (yaml && yaml.trim().length > 0) {
    headers['Content-Type'] = 'application/x-yaml';
  }
  const response = await axios.delete(withApiBase(`/api/agents/${encodeURIComponent(id)}`), {
    data: yaml,
    headers,
  });
  return response.data;
};