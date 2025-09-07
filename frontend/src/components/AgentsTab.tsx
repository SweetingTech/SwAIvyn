// src/components/AgentsTab.tsx
import React, { useEffect, useState } from "react";
import { getAgentCatalog, AgentCatalogItem } from "../services/agentService";
import { toast } from "react-toastify"; // Assuming react-toastify is installed and configured

const AgentsTab: React.FC = () => {
  const [agents, setAgents] = useState<AgentCatalogItem[]>([]);
  const [loading, setLoading] = useState<boolean>(false);

  // Load all agents from backend
  const loadAgents = async () => {
    setLoading(true);
    try {
      const data = await getAgentCatalog();
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

  // Catalog view only (no start/stop from here). Could add run triggers later via BFF proxy.

  return (
    <div className="p-6">
      <h2 className="text-2xl font-semibold mb-4">Agents Catalog</h2>

      {loading && agents.length === 0 ? ( // Show loading only on initial load
        <p>Loading agents…</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full border-collapse border border-gray-300">
            <thead className="bg-gray-100">
              <tr>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Name</th>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Type</th>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">Summary</th>
                <th className="border border-gray-300 px-4 py-2 text-left text-sm font-medium text-gray-700">File</th>
              </tr>
            </thead>
            <tbody>
              {agents.map((agent) => (
                <tr key={agent.id} className="hover:bg-gray-50 even:bg-white odd:bg-gray-50">
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700">{agent.name || agent.id}</td>
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700">{agent.type || 'yaml'}</td>
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700 truncate max-w-md">{agent.summary || '—'}</td>
                  <td className="border border-gray-300 px-4 py-2 text-sm text-gray-700">{agent.file_path || '—'}</td>
                </tr>
              ))}
              {agents.length === 0 && !loading && (
                <tr>
                  <td colSpan={6} className="border border-gray-300 px-4 py-4 text-center text-sm text-gray-500">
                    No agents found in workers. Ensure the container is running and agents YAMLs exist.
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
