// src/services/agentService.ts
import axios from "axios";

export interface Agent {
  id: string;
  name: string;
  status: string;         // "running" | "stopped"
  lastRun: string | null; // ISO‐string or null
  tasksCompleted: number;
  goal?: string;          // if you stored a goal in the DB
}

// Base URL for your SwAIvyn API (adjust or read from env)
const API_BASE = import.meta.env.VITE_API_BASE_URL || "http://localhost:5000";

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
