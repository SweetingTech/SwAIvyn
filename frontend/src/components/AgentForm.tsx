import { useEffect, useState } from 'react';
import axios from 'axios';
import { Loader2, X } from 'lucide-react';
import { createAgentDefinition, updateAgentDefinition } from '../services/agentService';

type AgentFormMode = 'create' | 'edit';

interface AgentFormProps {
  open: boolean;
  mode: AgentFormMode;
  agentId?: string;
  initialYaml?: string;
  onClose: () => void;
  onSaved: () => void;
}

const AgentForm: React.FC<AgentFormProps> = ({ open, mode, agentId, initialYaml = '', onClose, onSaved }) => {
  const [currentId, setCurrentId] = useState<string>(agentId || '');
  const [yaml, setYaml] = useState<string>(initialYaml || '');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState<boolean>(false);

  useEffect(() => {
    if (open) {
      setCurrentId(agentId || '');
      setYaml(initialYaml || '');
      setError(null);
      setSubmitting(false);
    }
  }, [open, agentId, initialYaml]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);

    const trimmedYaml = yaml.trim();
    if (!trimmedYaml) {
      setError('YAML definition is required.');
      return;
    }

    if (mode === 'edit' && !currentId) {
      setError('Agent ID is missing.');
      return;
    }

    setSubmitting(true);
    try {
      if (mode === 'create') {
        await createAgentDefinition(yaml, currentId || undefined);
      } else {
        await updateAgentDefinition(currentId, yaml);
      }
      onSaved();
      onClose();
    } catch (err) {
      if (axios.isAxiosError(err)) {
        const data = err.response?.data;
        if (typeof data === 'string' && data.trim().length > 0) {
          setError(data);
        } else if (data && typeof data === 'object') {
          const detail = (data as { detail?: unknown }).detail;
          if (typeof detail === 'string') {
            setError(detail);
          } else if (detail) {
            setError(JSON.stringify(detail));
          } else {
            setError(err.message || 'Request failed.');
          }
        } else {
          setError(err.message || 'Request failed.');
        }
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError('Unexpected error occurred.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4" onClick={onClose}>
      <div
        className="w-full max-w-2xl rounded-lg bg-white shadow-xl"
        role="dialog"
        aria-modal="true"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
          <h2 className="text-lg font-medium text-gray-900">{mode === 'create' ? 'Create Agent' : 'Edit Agent'}</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-gray-500 transition hover:bg-gray-100 hover:text-gray-700"
            aria-label="Close agent form"
          >
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 px-4 py-4">
          {mode === 'create' ? (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700" htmlFor="agent-id">
                Agent ID (optional)
              </label>
              <input
                id="agent-id"
                type="text"
                value={currentId}
                onChange={(event) => setCurrentId(event.target.value)}
                placeholder="If omitted, the YAML definition must include an id field"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-200"
              />
              <p className="mt-1 text-xs text-gray-500">The orchestrator will use this identifier when saving the agent.</p>
            </div>
          ) : (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Agent ID</label>
              <input
                type="text"
                value={currentId}
                disabled
                className="w-full cursor-not-allowed rounded-md border border-gray-200 bg-gray-100 px-3 py-2 text-sm text-gray-600"
              />
            </div>
          )}

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700" htmlFor="agent-yaml">
              Agent YAML definition
            </label>
            <textarea
              id="agent-yaml"
              value={yaml}
              onChange={(event) => setYaml(event.target.value)}
              rows={14}
              placeholder="name: Example Agent\ndescription: |\n  Describe your agent here..."
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono shadow-sm focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-200"
            />
            <p className="mt-1 text-xs text-gray-500">Paste the YAML definition that the workers orchestrator should store.</p>
          </div>

          {error && (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
          )}

          <div className="flex items-center justify-end space-x-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-100"
              disabled={submitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary inline-flex items-center justify-center px-4 py-2 text-sm font-medium"
              disabled={submitting}
            >
              {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {mode === 'create' ? 'Create Agent' : 'Save Changes'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AgentForm;
