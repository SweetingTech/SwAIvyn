// src/components/AgentsTab.tsx
import React, { useEffect, useState } from "react";
import { getAgents, startAgent, stopAgent, Agent } from "../services/agentService"; // Assuming agentService is in ../services
import { toast } from "react-toastify"; // Assuming react-toastify is installed and configured

const AgentsTab: React.FC = () => {
  const [agents, setAgents] = useState<Agent[]>([]);
  const [loading, setLoading] = useState<boolean>(false);

  // Load all agents from backend
  const loadAgents = async () => {
    setLoading(true);
    try {
      const data = await getAgents();
      setAgents(data);
    } catch (error) {
      console.error("Failed to load agents:", error);
      toast.error("Error loading agents");
    } finally {
      setLoading(false);
    }
  };

  // On mount, fetch once and then poll every 5 seconds
  useEffect(() => {
    loadAgents();
    const interval = setInterval(loadAgents, 5000);
    return () => clearInterval(interval); // Cleanup interval on component unmount
  }, []);

  // Start an agent by ID
  const handleStart = async (id: string) => {
    try {
      await startAgent(id);
      toast.success("Agent start request sent"); // Changed toast message to reflect async nature
      // Immediately refresh the UI to show "running" (or pending) status
      loadAgents();
    } catch (error) {
      console.error("Failed to start agent:", error);
      toast.error("Failed to start agent");
    }
  };

  // Stop an agent by ID
  const handleStop = async (id: string) => {
    try {
      await stopAgent(id);
      toast.success("Agent stop request sent"); // Changed toast message
      loadAgents();
    } catch (error) {
      console.error("Failed to stop agent:", error);
      toast.error("Failed to stop agent");
    }
  };

  return (
    <div className="p-6">
      <h2 className="text-2xl font-semibold mb-4">Agents</h2>

      {loading && agents.length === 0 ? ( // Show loading only on initial load
        <p>Loading agents…</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full border-collapse border border-gray-300">
            <thead className="bg-gray-100">
              <tr>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Name</th>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Status</th>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Last Run</th>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Tasks Completed</th>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Goal</th>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Actions</th>
              </tr>
            </thead>
            <tbody>
              {agents.map((agent) => (
                <tr key={agent.id} className="hover:bg-gray-50 even:bg-white odd:bg-gray-50">
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700">{agent.name}</td>
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700">
                    <span className={`px-2 py-1 text-xs font-semibold rounded-full ${
                      agent.status === "running" ? "bg-blue-100 text-blue-700" :
                      agent.status === "stopped" ? "bg-gray-200 text-gray-700" :
                      agent.status === "error_config" ? "bg-red-100 text-red-700" :
                      agent.status === "error_worker" ? "bg-red-100 text-red-700" :
                      "bg-yellow-100 text-yellow-700" // For other statuses like 'queued' or 'in-progress' if worker sends them
                    }`}>
                      {agent.status}
                    </span>
                  </td>
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700">
                    {agent.lastRun
                      ? new Date(agent.lastRun).toLocaleString()
                      : "N/A"}
                  </td>
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700 text-center">{agent.tasksCompleted}</td>
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700 truncate max-w-xs">{agent.goal || "N/A"}</td>
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700">
                    {agent.status !== "running" ? (
                      <button
                        onClick={() => handleStart(agent.id)}
                        className="bg-green-500 hover:bg-green-600 text-white px-3 py-1 rounded text-xs shadow-sm disabled:opacity-50"
                        disabled={loading || agent.status === "running"} // Disable if global loading or already running
                      >
                        Start
                      </button>
                    ) : (
                      <button
                        onClick={() => handleStop(agent.id)}
                        className="bg-red-500 hover:bg-red-600 text-white px-3 py-1 rounded text-xs shadow-sm disabled:opacity-50"
                        disabled={loading} // Disable if global loading
                      >
                        Stop
                      </button>
                    )}
                  </td>
                </tr>
              ))}
              {agents.length === 0 && !loading && (
                <tr>
                  <td colSpan={6} className="border border-gray-300 px-4 py-4 text-center text-sm text-gray-500">
                    No agents found. You may need to create some in the database or check configuration.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

export default AgentsTab;
