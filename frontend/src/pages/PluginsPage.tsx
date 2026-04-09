import { motion } from 'framer-motion';
import { useCallback, useEffect, useState } from 'react';
import {
  Package,
  Plus,
  Trash2,
  ToggleLeft,
  ToggleRight,
  RefreshCw,
  Activity,
  Search,
  AlertCircle,
  CheckCircle,
  HelpCircle,
  X,
} from 'lucide-react';
import InlineSpinner from '../components/ui/InlineSpinner';
import useEffectiveUser from '../hooks/useEffectiveUser';
import { useAuth } from '../contexts/AuthContext';

// --------------- Types ---------------

interface Plugin {
  id: string;
  name: string;
  version: string;
  description: string | null;
  author: string | null;
  status: string; // installed | enabled | disabled | error
  health_status: string | null; // healthy | unhealthy | unknown | null
  permissions: string[];
  capabilities: string[];
  entry_point: string | null;
  health_endpoint: string | null;
  installed_at: string | null;
  updated_at: string | null;
}

interface PluginManifest {
  manifest_version: string;
  id: string;
  name: string;
  version: string;
  description?: string;
  author?: string;
  entry_point?: string;
  health_endpoint?: string;
  permissions?: string[];
  capabilities?: string[];
}

// --------------- Helpers ---------------

const healthIcon = (status: string | null) => {
  switch (status) {
    case 'healthy':
      return <CheckCircle size={14} className="text-green-500" />;
    case 'unhealthy':
      return <AlertCircle size={14} className="text-red-500" />;
    default:
      return <HelpCircle size={14} className="text-gray-400" />;
  }
};

const statusBadge = (status: string) => {
  const colors: Record<string, string> = {
    enabled: 'bg-green-100 text-green-700',
    installed: 'bg-blue-100 text-blue-700',
    disabled: 'bg-gray-100 text-gray-600',
    error: 'bg-red-100 text-red-700',
  };
  return (
    <span className={`px-2 py-0.5 rounded text-xs font-medium ${colors[status] ?? 'bg-gray-100 text-gray-600'}`}>
      {status}
    </span>
  );
};

// --------------- Install modal ---------------

interface InstallModalProps {
  onClose: () => void;
  onInstalled: () => void;
  headers: Record<string, string>;
}

const BLANK_MANIFEST = `{
  "manifest_version": "1",
  "id": "my-plugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "description": "What this plugin does",
  "author": "Your Name",
  "entry_point": "https://my-plugin.example.com",
  "health_endpoint": "https://my-plugin.example.com/health",
  "permissions": [],
  "capabilities": ["tool-use"]
}`;

const InstallModal = ({ onClose, onInstalled, headers }: InstallModalProps) => {
  const [raw, setRaw] = useState(BLANK_MANIFEST);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const handleInstall = async () => {
    setError(null);
    let manifest: PluginManifest;
    try {
      manifest = JSON.parse(raw);
    } catch {
      setError('Invalid JSON — please fix the manifest and try again.');
      return;
    }

    setSaving(true);
    try {
      const resp = await fetch('/api/plugins/install', {
        method: 'POST',
        headers: { ...headers, 'Content-Type': 'application/json' },
        body: JSON.stringify(manifest),
      });
      if (!resp.ok) {
        const body = await resp.json().catch(() => ({}));
        throw new Error(body.detail ?? `HTTP ${resp.status}`);
      }
      onInstalled();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-2xl">
        <div className="flex items-center justify-between p-4 border-b">
          <h2 className="text-lg font-medium text-gray-900">Install Plugin from Manifest</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X size={20} />
          </button>
        </div>

        <div className="p-4">
          <p className="text-sm text-gray-600 mb-3">
            Paste or edit a plugin manifest (JSON). See{' '}
            <code className="font-mono text-xs bg-gray-100 px-1 py-0.5 rounded">
              docs/plugin-manifest.md
            </code>{' '}
            in the repository for the full spec.
          </p>
          <textarea
            className="w-full h-64 font-mono text-sm border border-gray-300 rounded-md p-3 focus:outline-none focus:ring-1 focus:ring-primary-500"
            value={raw}
            onChange={(e) => setRaw(e.target.value)}
            spellCheck={false}
          />
          {error && (
            <p className="mt-2 text-sm text-red-600 flex items-center gap-1">
              <AlertCircle size={14} /> {error}
            </p>
          )}
        </div>

        <div className="flex justify-end gap-2 p-4 border-t">
          <button className="btn btn-outline" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button className="btn btn-primary" onClick={handleInstall} disabled={saving}>
            {saving ? <InlineSpinner /> : <><Plus size={16} className="mr-1" />Install</>}
          </button>
        </div>
      </div>
    </div>
  );
};

// --------------- Main page ---------------

const PluginsPage = () => {
  const eff = useEffectiveUser();
  const { user } = useAuth();
  const isAdmin = user?.role === 'admin';

  const [plugins, setPlugins] = useState<Plugin[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [showInstallModal, setShowInstallModal] = useState(false);
  const [probingId, setProbingId] = useState<string | null>(null);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [removingId, setRemovingId] = useState<string | null>(null);

  const loadPlugins = useCallback(async () => {
    if (!eff.resolved) return;
    setLoading(true);
    try {
      const resp = await fetch('/api/plugins', { headers: eff.headers });
      if (!resp.ok) throw new Error('Failed to load plugins');
      setPlugins(await resp.json());
    } catch {
      setPlugins([]);
    } finally {
      setLoading(false);
    }
  }, [eff.resolved, eff.headers]);

  useEffect(() => {
    void loadPlugins();
  }, [loadPlugins]);

  const handleToggle = async (plugin: Plugin) => {
    if (!isAdmin) return;
    const nextStatus = plugin.status === 'enabled' ? 'disabled' : 'enabled';
    setTogglingId(plugin.id);
    try {
      const resp = await fetch(`/api/plugins/${plugin.id}/status`, {
        method: 'PATCH',
        headers: { ...eff.headers, 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: nextStatus }),
      });
      if (!resp.ok) throw new Error('Failed to update plugin status');
      setPlugins((prev) =>
        prev.map((p) => (p.id === plugin.id ? { ...p, status: nextStatus } : p))
      );
    } catch {
      // silently ignore; UI stays the same
    } finally {
      setTogglingId(null);
    }
  };

  const handleUninstall = async (plugin: Plugin) => {
    if (!isAdmin) return;
    if (!window.confirm(`Uninstall plugin "${plugin.name}"? This cannot be undone.`)) return;
    setRemovingId(plugin.id);
    try {
      await fetch(`/api/plugins/${plugin.id}`, { method: 'DELETE', headers: eff.headers });
      setPlugins((prev) => prev.filter((p) => p.id !== plugin.id));
    } catch {
      // ignore
    } finally {
      setRemovingId(null);
    }
  };

  const handleProbeHealth = async (plugin: Plugin) => {
    setProbingId(plugin.id);
    try {
      const resp = await fetch(`/api/plugins/${plugin.id}/health`, { headers: eff.headers });
      if (resp.ok) {
        const data = await resp.json();
        setPlugins((prev) =>
          prev.map((p) =>
            p.id === plugin.id ? { ...p, health_status: data.health_status } : p
          )
        );
      }
    } finally {
      setProbingId(null);
    }
  };

  const filtered = plugins.filter(
    (p) =>
      p.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (p.description ?? '').toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <motion.div
      className="min-h-[calc(100vh-64px)] bg-gray-50 p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="max-w-5xl mx-auto">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between mb-6">
          <div>
            <h1 className="text-2xl font-medium text-gray-800">Plugins</h1>
            <p className="text-gray-600">Extend SwAIvyn with third-party capabilities</p>
          </div>
          <div className="flex items-center space-x-2 mt-2 sm:mt-0">
            <button className="btn btn-outline" onClick={loadPlugins}>
              <RefreshCw size={16} className="mr-1.5" />
              Refresh
            </button>
            {isAdmin && (
              <button className="btn btn-primary" onClick={() => setShowInstallModal(true)}>
                <Plus size={16} className="mr-1.5" />
                Install Plugin
              </button>
            )}
          </div>
        </div>

        {/* Search */}
        <div className="mb-6">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" size={18} />
            <input
              type="text"
              placeholder="Search plugins..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-10 pr-4 py-2 w-full border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
            />
          </div>
        </div>

        {/* Plugin list */}
        {loading ? (
          <div className="text-center py-12">
            <InlineSpinner />
            <p className="mt-2 text-gray-500">Loading plugins…</p>
          </div>
        ) : filtered.length === 0 ? (
          <div className="text-center py-12">
            <Package size={48} className="mx-auto text-gray-400 mb-4" />
            <h3 className="text-lg font-medium text-gray-900 mb-2">No plugins found</h3>
            <p className="text-gray-600 mb-4">
              {searchTerm
                ? 'Try adjusting your search criteria.'
                : isAdmin
                ? 'No plugins are installed yet. Click "Install Plugin" to add one.'
                : 'No plugins are currently installed.'}
            </p>
            {isAdmin && !searchTerm && (
              <button className="btn btn-primary" onClick={() => setShowInstallModal(true)}>
                <Plus size={16} className="mr-1.5" />
                Install Plugin
              </button>
            )}
          </div>
        ) : (
          <div className="bg-white rounded-lg shadow-soft divide-y">
            {filtered.map((plugin) => (
              <div key={plugin.id} className="p-4">
                <div className="flex items-start gap-3">
                  <Package size={24} className="text-primary-500 flex-shrink-0 mt-0.5" />
                  <div className="flex-grow min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium text-gray-900">{plugin.name}</span>
                      {statusBadge(plugin.status)}
                      <span className="text-xs text-gray-500">v{plugin.version}</span>
                      {plugin.author && (
                        <span className="text-xs text-gray-400">by {plugin.author}</span>
                      )}
                      <span className="flex items-center gap-1 text-xs text-gray-500">
                        {healthIcon(plugin.health_status)}
                        {plugin.health_status ?? 'unknown'}
                      </span>
                    </div>
                    {plugin.description && (
                      <p className="mt-1 text-sm text-gray-600">{plugin.description}</p>
                    )}
                    {plugin.capabilities.length > 0 && (
                      <div className="flex flex-wrap gap-1 mt-2">
                        {plugin.capabilities.map((cap) => (
                          <span
                            key={cap}
                            className="px-1.5 py-0.5 rounded bg-primary-50 text-primary-700 text-xs"
                          >
                            {cap}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                  {/* Actions */}
                  <div className="flex items-center gap-1 flex-shrink-0">
                    <button
                      title="Check health"
                      className="btn btn-ghost text-xs py-1 px-2"
                      onClick={() => handleProbeHealth(plugin)}
                      disabled={probingId === plugin.id}
                    >
                      {probingId === plugin.id ? (
                        <InlineSpinner />
                      ) : (
                        <Activity size={14} />
                      )}
                    </button>
                    {isAdmin && (
                      <>
                        <button
                          title={plugin.status === 'enabled' ? 'Disable plugin' : 'Enable plugin'}
                          className="btn btn-ghost text-xs py-1 px-2"
                          onClick={() => handleToggle(plugin)}
                          disabled={togglingId === plugin.id}
                        >
                          {togglingId === plugin.id ? (
                            <InlineSpinner />
                          ) : plugin.status === 'enabled' ? (
                            <ToggleRight size={16} className="text-green-600" />
                          ) : (
                            <ToggleLeft size={16} className="text-gray-400" />
                          )}
                        </button>
                        <button
                          title="Uninstall plugin"
                          className="btn btn-ghost text-xs py-1 px-2 text-red-500 hover:text-red-700"
                          onClick={() => handleUninstall(plugin)}
                          disabled={removingId === plugin.id}
                        >
                          {removingId === plugin.id ? <InlineSpinner /> : <Trash2 size={14} />}
                        </button>
                      </>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Non-admin notice */}
        {!isAdmin && (
          <p className="mt-4 text-sm text-gray-400 text-center">
            Plugin installation and management require admin privileges.
          </p>
        )}
      </div>

      {showInstallModal && (
        <InstallModal
          headers={eff.headers}
          onClose={() => setShowInstallModal(false)}
          onInstalled={() => {
            setShowInstallModal(false);
            void loadPlugins();
          }}
        />
      )}
    </motion.div>
  );
};

export default PluginsPage;
