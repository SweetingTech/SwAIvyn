// src/services/agentService.ts
import axios from "axios";

export interface Agent {
  id: string;
  name?: string;
  status?: string;
  userId?: string;
  description?: string;
  type?: string;
  lastRun?: string | null;
  tasksCompleted?: number;
  enabled?: boolean;
  startedAt?: string | null;
  finishedAt?: string | null;
  meta?: Record<string, unknown>;
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

// Base URL for the BFF API. Default to relative so Vite proxy handles it in dev.
// If VITE_API_BASE_URL is provided (e.g., full http URL), it will be used.
const rawBase = (import.meta as any)?.env?.VITE_API_BASE_URL || '';
const API_BASE = typeof rawBase === 'string' ? rawBase.replace(/\/$/, '') : '';

/**
 * Fetch all agents from SwAIvyn backend
 */
export const getAgents = async (): Promise<Agent[]> => {
  const response = await axios.get<Agent[]>(`${API_BASE}/api/agents`);
  return response.data;
};

/**
 * Tell the backend to start a specific agent
 */
export const startAgent = async (id: string): Promise<void> => {
  await axios.post(`${API_BASE}/api/agents/${id}/start`);
};

/**
 * Tell the backend to stop a specific agent
 */
export const stopAgent = async (id: string): Promise<void> => {
  await axios.post(`${API_BASE}/api/agents/${id}/stop`);
};

/**
 * Fetch the available agent catalog from the Workers orchestrator via the BFF proxy
 */
export const getAgentCatalog = async (): Promise<AgentCatalogItem[]> => {
  const response = await axios.get<AgentCatalogItem[]>(`${API_BASE}/api/agents/catalog`);
  return response.data;
};

/**
 * Create a new agent definition in the workers orchestrator via the BFF
 */
export const createAgentDefinition = async (yaml: string, agentId?: string) => {
  const suffix = agentId ? `?agentId=${encodeURIComponent(agentId)}` : '';
  const response = await axios.post(`${API_BASE}/api/agents${suffix}`, yaml, {
    headers: { 'Content-Type': 'application/x-yaml' },
  });
  return response.data;
};

/**
 * Update an existing agent definition
 */
export const updateAgentDefinition = async (id: string, yaml: string) => {
  const response = await axios.put(`${API_BASE}/api/agents/${encodeURIComponent(id)}`, yaml, {
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
  const response = await axios.delete(`${API_BASE}/api/agents/${encodeURIComponent(id)}`, {
    data: yaml,
    headers,
  });
  return response.data;
};
