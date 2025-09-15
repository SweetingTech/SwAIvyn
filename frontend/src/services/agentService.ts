// src/services/agentService.ts
import axios from "axios";

export interface Agent {
  id: string;
  name?: string | null;
  status: string;
  userId?: string | null;
  startedAt?: string | null;
  finishedAt?: string | null;
  updatedAt?: string | null;
  meta?: Record<string, unknown> | null;
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
export const startAgent = async (id: string, message = 'Started via API'): Promise<void> => {
  await axios.patch(`${API_BASE}/api/agents/${id}`, {
    status: 'working',
    message,
    startedAt: new Date().toISOString(),
  });
};

/**
 * Tell the backend to stop a specific agent
 */
export const stopAgent = async (id: string, status: string = 'paused', message = 'Stopped via API'): Promise<void> => {
  await axios.patch(`${API_BASE}/api/agents/${id}`, {
    status,
    message,
    finishedAt: status === 'completed' || status === 'failed' ? new Date().toISOString() : undefined,
  });
};

/**
 * Fetch the available agent catalog from the Workers orchestrator via the BFF proxy
 */
export const getAgentCatalog = async (): Promise<AgentCatalogItem[]> => {
  const response = await axios.get<AgentCatalogItem[]>(`${API_BASE}/api/agents/catalog`);
  return response.data;
};
