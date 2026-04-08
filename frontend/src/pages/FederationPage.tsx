import { useState, useEffect, useCallback } from 'react';
import axios from 'axios';
import { motion } from 'framer-motion';
import {
  Network,
  Plus,
  Trash2,
  RefreshCw,
  Send,
  Mail,
  Calendar,
  Globe,
  Radio,
  ChevronDown,
  ChevronUp,
  ArrowUpRight,
  ArrowDownLeft,
  Bot,
} from 'lucide-react';
import { toast } from 'react-toastify';
import useEffectiveUser from '../hooks/useEffectiveUser';

const API = '/api/federation';

// ─── Types ────────────────────────────────────────────────────────────────────

interface Peer {
  id: string;
  name: string;
  url: string;
  status: 'pending' | 'connected' | 'unreachable';
  discovered_via?: string;
  last_seen?: string;
  created_at: string;
}

interface FedMessage {
  id: string;
  peer_id?: string;
  direction: 'in' | 'out';
  message_type: 'user' | 'ai_task' | 'ai_result';
  from_address?: string;
  to_address?: string;
  subject?: string;
  body: string;
  status: string;
  created_at: string;
}

interface EmailAccount {
  id: string;
  label: string;
  host: string;
  port: string;
  username: string;
  use_ssl: boolean;
  status: string;
  last_synced?: string;
}

interface EmailMessage {
  id: string;
  subject?: string;
  from_addr?: string;
  date?: string;
  body_text?: string;
  is_read: boolean;
}

interface CalAccount {
  id: string;
  label: string;
  url: string;
  type: string;
  color?: string;
  status: string;
  last_synced?: string;
}

interface CalEvent {
  id: string;
  summary?: string;
  location?: string;
  start_dt?: string;
  end_dt?: string;
  all_day: boolean;
  description?: string;
}

interface BrowseEntry {
  id: string;
  url: string;
  title?: string;
  visited_at: string;
}

// ─── Utility ──────────────────────────────────────────────────────────────────

const StatusDot = ({ status }: { status: string }) => {
  const color =
    status === 'connected' ? 'bg-green-500' :
    status === 'unreachable' || status === 'error' ? 'bg-red-500' :
    'bg-yellow-400';
  return <span className={`inline-block w-2.5 h-2.5 rounded-full ${color} mr-1.5`} />;
};

const fmtDate = (s?: string) => {
  if (!s) return '—';
  try { return new Date(s).toLocaleString(); } catch { return s; }
};

// ─── Tab: Peers ───────────────────────────────────────────────────────────────

const PeersTab = () => {
  const [peers, setPeers] = useState<Peer[]>([]);
  const [loading, setLoading] = useState(false);
  const [discovering, setDiscovering] = useState(false);
  const [discovered, setDiscovered] = useState<string[]>([]);
  const [showAdd, setShowAdd] = useState(false);
  const [form, setForm] = useState({ name: '', url: '', api_key: '' });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await axios.get<Peer[]>(`${API}/peers`);
      setPeers(data);
    } catch {
      toast.error('Failed to load peers');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleDiscover = async () => {
    setDiscovering(true);
    setDiscovered([]);
    try {
      const { data } = await axios.post<{ discovered: string[] }>(`${API}/peers/discover`);
      setDiscovered(data.discovered || []);
      if ((data.discovered || []).length === 0) {
        toast.info('No peers found on local network');
      } else {
        toast.success(`Discovered ${data.discovered.length} peer(s)`);
      }
    } catch (e: any) {
      toast.error(e?.response?.data?.detail || 'Discovery failed');
    } finally {
      setDiscovering(false);
    }
  };

  const handleAdd = async () => {
    if (!form.name || !form.url) { toast.error('Name and URL are required'); return; }
    try {
      await axios.post(`${API}/peers`, form);
      toast.success('Peer added');
      setShowAdd(false);
      setForm({ name: '', url: '', api_key: '' });
      load();
    } catch (e: any) {
      toast.error(e?.response?.data?.detail || 'Failed to add peer');
    }
  };

  const handlePing = async (id: string) => {
    try {
      const { data } = await axios.post<{ status: string }>(`${API}/peers/${id}/ping`);
      toast.info(`Peer status: ${data.status}`);
      load();
    } catch {
      toast.error('Ping failed');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Remove this peer?')) return;
    try {
      await axios.delete(`${API}/peers/${id}`);
      toast.success('Peer removed');
      load();
    } catch {
      toast.error('Failed to remove peer');
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-2 flex-wrap">
        <button
          onClick={load}
          className="flex items-center gap-1 px-3 py-1.5 text-sm bg-gray-100 hover:bg-gray-200 rounded"
        >
          <RefreshCw size={14} /> Refresh
        </button>
        <button
          onClick={handleDiscover}
          disabled={discovering}
          className="flex items-center gap-1 px-3 py-1.5 text-sm bg-blue-100 hover:bg-blue-200 text-blue-700 rounded disabled:opacity-50"
        >
          <Radio size={14} /> {discovering ? 'Scanning…' : 'Discover LAN Peers'}
        </button>
        <button
          onClick={() => setShowAdd(v => !v)}
          className="flex items-center gap-1 px-3 py-1.5 text-sm bg-green-100 hover:bg-green-200 text-green-700 rounded"
        >
          <Plus size={14} /> Add Peer
        </button>
      </div>

      {discovered.length > 0 && (
        <div className="bg-blue-50 border border-blue-200 rounded p-3">
          <p className="text-sm font-medium text-blue-800 mb-2">Discovered on local network:</p>
          <ul className="space-y-1">
            {discovered.map(url => (
              <li key={url} className="text-sm text-blue-700 flex items-center justify-between">
                <span>{url}</span>
                <button
                  onClick={() => {
                    let hostname = url;
                    try { hostname = new URL(url).hostname; } catch { /* keep raw url as name */ }
                    setForm({ name: hostname, url, api_key: '' });
                  }}
                  className="ml-2 text-xs px-2 py-0.5 bg-blue-600 text-white rounded hover:bg-blue-700"
                >
                  Add
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {showAdd && (
        <div className="bg-gray-50 border rounded p-4 space-y-3">
          <h3 className="font-medium text-gray-800">Add Peer Instance</h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className="block text-xs text-gray-600 mb-1">Name</label>
              <input
                className="w-full border rounded px-2 py-1.5 text-sm"
                placeholder="My Home Server"
                value={form.name}
                onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
              />
            </div>
            <div>
              <label className="block text-xs text-gray-600 mb-1">URL</label>
              <input
                className="w-full border rounded px-2 py-1.5 text-sm"
                placeholder="https://peer.example.com"
                value={form.url}
                onChange={e => setForm(f => ({ ...f, url: e.target.value }))}
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-xs text-gray-600 mb-1">API Key (leave blank to auto-generate)</label>
              <input
                className="w-full border rounded px-2 py-1.5 text-sm font-mono"
                placeholder="Optional shared key"
                value={form.api_key}
                onChange={e => setForm(f => ({ ...f, api_key: e.target.value }))}
              />
            </div>
          </div>
          <div className="flex gap-2">
            <button onClick={handleAdd} className="px-4 py-1.5 bg-green-600 text-white text-sm rounded hover:bg-green-700">Add</button>
            <button onClick={() => setShowAdd(false)} className="px-4 py-1.5 bg-gray-200 text-sm rounded hover:bg-gray-300">Cancel</button>
          </div>
        </div>
      )}

      {loading ? (
        <p className="text-gray-500 text-sm">Loading…</p>
      ) : peers.length === 0 ? (
        <p className="text-gray-500 text-sm">No peers configured. Use Discover or Add Peer to get started.</p>
      ) : (
        <div className="space-y-2">
          {peers.map(peer => (
            <div key={peer.id} className="flex items-center justify-between bg-white border rounded p-3 shadow-sm">
              <div className="flex items-center gap-2">
                <Network size={18} className="text-gray-400" />
                <div>
                  <div className="flex items-center gap-1">
                    <StatusDot status={peer.status} />
                    <span className="font-medium text-sm text-gray-800">{peer.name}</span>
                    <span className="text-xs text-gray-400 ml-1">({peer.discovered_via || 'manual'})</span>
                  </div>
                  <a href={peer.url} target="_blank" rel="noopener noreferrer"
                    className="text-xs text-blue-600 hover:underline flex items-center gap-0.5">
                    {peer.url} <ArrowUpRight size={10} />
                  </a>
                  {peer.last_seen && <p className="text-xs text-gray-400">Last seen: {fmtDate(peer.last_seen)}</p>}
                </div>
              </div>
              <div className="flex gap-2">
                <button onClick={() => handlePing(peer.id)}
                  className="text-xs px-2 py-1 bg-blue-100 text-blue-700 rounded hover:bg-blue-200">Ping</button>
                <button onClick={() => handleDelete(peer.id)}
                  className="text-xs p-1 text-red-400 hover:text-red-600">
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

// ─── Tab: Messages ────────────────────────────────────────────────────────────

const MessagesTab = () => {
  const [messages, setMessages] = useState<FedMessage[]>([]);
  const [peers, setPeers] = useState<Peer[]>([]);
  const [loading, setLoading] = useState(false);
  const [showCompose, setShowCompose] = useState(false);
  const [composeType, setComposeType] = useState<'user' | 'ai_task'>('user');
  const [form, setForm] = useState({ peer_id: '', to_address: '', subject: '', body: '' });
  const [expanded, setExpanded] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [{ data: msgs }, { data: ps }] = await Promise.all([
        axios.get<FedMessage[]>(`${API}/messages`),
        axios.get<Peer[]>(`${API}/peers`),
      ]);
      setMessages(msgs);
      setPeers(ps);
    } catch {
      toast.error('Failed to load messages');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleSend = async () => {
    if (!form.peer_id || !form.body) {
      toast.error('Peer and body are required');
      return;
    }
    if (composeType === 'user' && !form.to_address) {
      toast.error('Recipient address is required for user messages');
      return;
    }
    try {
      const endpoint = composeType === 'ai_task' ? `${API}/ai-task` : `${API}/messages`;
      const payload = composeType === 'ai_task'
        ? { peer_id: form.peer_id, task_prompt: form.body, context: form.subject || undefined }
        : { ...form, message_type: 'user' };
      const { data } = await axios.post<{ status: string }>(endpoint, payload);
      toast.success(`Message ${data.status}`);
      setShowCompose(false);
      setForm({ peer_id: '', to_address: '', subject: '', body: '' });
      load();
    } catch (e: any) {
      toast.error(e?.response?.data?.detail || 'Send failed');
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await axios.delete(`${API}/messages/${id}`);
      setMessages(ms => ms.filter(m => m.id !== id));
    } catch {
      toast.error('Failed to delete message');
    }
  };

  const typeIcon = (t: string) => {
    if (t === 'ai_task') return <Bot size={14} className="text-purple-500" />;
    if (t === 'ai_result') return <Bot size={14} className="text-green-500" />;
    return <Mail size={14} className="text-blue-500" />;
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <button onClick={load} className="flex items-center gap-1 px-3 py-1.5 text-sm bg-gray-100 hover:bg-gray-200 rounded">
          <RefreshCw size={14} /> Refresh
        </button>
        <button onClick={() => { setComposeType('user'); setShowCompose(v => !v); }}
          className="flex items-center gap-1 px-3 py-1.5 text-sm bg-blue-100 hover:bg-blue-200 text-blue-700 rounded">
          <Send size={14} /> Send Message
        </button>
        <button onClick={() => { setComposeType('ai_task'); setShowCompose(v => !v); }}
          className="flex items-center gap-1 px-3 py-1.5 text-sm bg-purple-100 hover:bg-purple-200 text-purple-700 rounded">
          <Bot size={14} /> Delegate AI Task
        </button>
      </div>

      {showCompose && (
        <div className="bg-gray-50 border rounded p-4 space-y-3">
          <h3 className="font-medium text-gray-800">
            {composeType === 'ai_task' ? 'Delegate AI Task to Peer' : 'Send Federated Message'}
          </h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className="block text-xs text-gray-600 mb-1">Peer Instance</label>
              <select
                className="w-full border rounded px-2 py-1.5 text-sm"
                value={form.peer_id}
                onChange={e => setForm(f => ({ ...f, peer_id: e.target.value }))}
              >
                <option value="">Select peer…</option>
                {peers.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </div>
            {composeType === 'user' && (
              <div>
                <label className="block text-xs text-gray-600 mb-1">To (user@host)</label>
                <input
                  className="w-full border rounded px-2 py-1.5 text-sm"
                  placeholder="alice@peer.example.com"
                  value={form.to_address}
                  onChange={e => setForm(f => ({ ...f, to_address: e.target.value }))}
                />
              </div>
            )}
            <div className="sm:col-span-2">
              <label className="block text-xs text-gray-600 mb-1">
                {composeType === 'ai_task' ? 'Context / Subject (optional)' : 'Subject'}
              </label>
              <input
                className="w-full border rounded px-2 py-1.5 text-sm"
                placeholder={composeType === 'ai_task' ? 'Task context…' : 'Subject…'}
                value={form.subject}
                onChange={e => setForm(f => ({ ...f, subject: e.target.value }))}
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-xs text-gray-600 mb-1">
                {composeType === 'ai_task' ? 'Task Prompt' : 'Message'}
              </label>
              <textarea
                className="w-full border rounded px-2 py-1.5 text-sm h-24 resize-none"
                placeholder={composeType === 'ai_task' ? 'Describe the AI task…' : 'Write your message…'}
                value={form.body}
                onChange={e => setForm(f => ({ ...f, body: e.target.value }))}
              />
            </div>
          </div>
          <div className="flex gap-2">
            <button onClick={handleSend} className="px-4 py-1.5 bg-blue-600 text-white text-sm rounded hover:bg-blue-700">Send</button>
            <button onClick={() => setShowCompose(false)} className="px-4 py-1.5 bg-gray-200 text-sm rounded hover:bg-gray-300">Cancel</button>
          </div>
        </div>
      )}

      {loading ? <p className="text-sm text-gray-500">Loading…</p> :
        messages.length === 0 ? <p className="text-sm text-gray-500">No federated messages yet.</p> :
        <div className="space-y-2">
          {messages.map(msg => (
            <div key={msg.id} className="bg-white border rounded p-3 shadow-sm">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-2">
                  {msg.direction === 'out'
                    ? <ArrowUpRight size={14} className="text-blue-500" />
                    : <ArrowDownLeft size={14} className="text-green-500" />}
                  {typeIcon(msg.message_type)}
                  <div>
                    <span className="text-sm font-medium text-gray-800">
                      {msg.subject || (msg.message_type === 'ai_task' ? 'AI Task' : 'Message')}
                    </span>
                    <div className="text-xs text-gray-400">
                      {msg.direction === 'out'
                        ? `To: ${msg.to_address || '—'}`
                        : `From: ${msg.from_address || '—'}`}
                      {' · '}{fmtDate(msg.created_at)}
                      {' · '}
                      <span className={`font-medium ${msg.status === 'sent' || msg.status === 'received' ? 'text-green-600' : msg.status === 'failed' ? 'text-red-600' : 'text-yellow-600'}`}>
                        {msg.status}
                      </span>
                    </div>
                  </div>
                </div>
                <div className="flex gap-1">
                  <button onClick={() => setExpanded(expanded === msg.id ? null : msg.id)}
                    className="text-gray-400 hover:text-gray-600 p-1">
                    {expanded === msg.id ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                  </button>
                  <button onClick={() => handleDelete(msg.id)}
                    className="text-red-300 hover:text-red-500 p-1"><Trash2 size={14} /></button>
                </div>
              </div>
              {expanded === msg.id && (
                <div className="mt-2 pt-2 border-t text-sm text-gray-700 whitespace-pre-wrap break-words">
                  {msg.body}
                </div>
              )}
            </div>
          ))}
        </div>
      }
    </div>
  );
};

// ─── Tab: Email ───────────────────────────────────────────────────────────────

const EmailTab = () => {
  const [accounts, setAccounts] = useState<EmailAccount[]>([]);
  const [messages, setMessages] = useState<EmailMessage[]>([]);
  const [selAccount, setSelAccount] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState<string | null>(null);
  const [showAdd, setShowAdd] = useState(false);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [form, setForm] = useState({ label: '', host: '', port: '993', username: '', password: '', use_ssl: true });

  const loadAccounts = useCallback(async () => {
    try {
      const { data } = await axios.get<EmailAccount[]>(`${API}/email/accounts`);
      setAccounts(data);
      if (data.length > 0 && !selAccount) setSelAccount(data[0].id);
    } catch { toast.error('Failed to load email accounts'); }
  }, [selAccount]);

  const loadMessages = useCallback(async (accountId: string) => {
    if (!accountId) return;
    setLoading(true);
    try {
      const { data } = await axios.get<EmailMessage[]>(`${API}/email/messages`, { params: { account_id: accountId } });
      setMessages(data);
    } catch { toast.error('Failed to load messages'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { loadAccounts(); }, [loadAccounts]);
  useEffect(() => { if (selAccount) loadMessages(selAccount); }, [selAccount, loadMessages]);

  const handleAdd = async () => {
    if (!form.label || !form.host || !form.username) { toast.error('Label, host and username are required'); return; }
    try {
      await axios.post(`${API}/email/accounts`, form);
      toast.success('Account added');
      setShowAdd(false);
      setForm({ label: '', host: '', port: '993', username: '', password: '', use_ssl: true });
      loadAccounts();
    } catch (e: any) { toast.error(e?.response?.data?.detail || 'Failed to add account'); }
  };

  const handleSync = async (id: string) => {
    setSyncing(id);
    try {
      const { data } = await axios.post<{ synced: number; saved: number }>(`${API}/email/sync/${id}`);
      toast.success(`Synced ${data.synced} messages (${data.saved} new)`);
      loadAccounts();
      if (id === selAccount) loadMessages(id);
    } catch (e: any) { toast.error(e?.response?.data?.detail || 'Sync failed'); }
    finally { setSyncing(null); }
  };

  const handleDeleteAccount = async (id: string) => {
    if (!window.confirm('Remove this email account?')) return;
    try {
      await axios.delete(`${API}/email/accounts/${id}`);
      toast.success('Account removed');
      loadAccounts();
      if (id === selAccount) { setSelAccount(''); setMessages([]); }
    } catch { toast.error('Failed to remove account'); }
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-2 flex-wrap">
        <button onClick={() => setShowAdd(v => !v)}
          className="flex items-center gap-1 px-3 py-1.5 text-sm bg-green-100 hover:bg-green-200 text-green-700 rounded">
          <Plus size={14} /> Add IMAP Account
        </button>
      </div>

      {showAdd && (
        <div className="bg-gray-50 border rounded p-4 space-y-3">
          <h3 className="font-medium text-gray-800">Add Email Account (IMAP)</h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {[
              { key: 'label', label: 'Label', placeholder: 'Personal Gmail' },
              { key: 'host', label: 'IMAP Host', placeholder: 'imap.gmail.com' },
              { key: 'port', label: 'Port', placeholder: '993' },
              { key: 'username', label: 'Username / Email', placeholder: 'you@gmail.com' },
            ].map(({ key, label, placeholder }) => (
              <div key={key}>
                <label className="block text-xs text-gray-600 mb-1">{label}</label>
                <input className="w-full border rounded px-2 py-1.5 text-sm"
                  placeholder={placeholder}
                  value={(form as any)[key]}
                  onChange={e => setForm(f => ({ ...f, [key]: e.target.value }))} />
              </div>
            ))}
            <div>
              <label className="block text-xs text-gray-600 mb-1">Password</label>
              <input type="password" className="w-full border rounded px-2 py-1.5 text-sm"
                value={form.password}
                onChange={e => setForm(f => ({ ...f, password: e.target.value }))} />
            </div>
            <div className="flex items-center gap-2 pt-4">
              <input type="checkbox" id="use_ssl" checked={form.use_ssl}
                onChange={e => setForm(f => ({ ...f, use_ssl: e.target.checked }))} />
              <label htmlFor="use_ssl" className="text-sm text-gray-700">Use SSL</label>
            </div>
          </div>
          <div className="flex gap-2">
            <button onClick={handleAdd} className="px-4 py-1.5 bg-green-600 text-white text-sm rounded hover:bg-green-700">Add</button>
            <button onClick={() => setShowAdd(false)} className="px-4 py-1.5 bg-gray-200 text-sm rounded hover:bg-gray-300">Cancel</button>
          </div>
        </div>
      )}

      {accounts.length > 0 && (
        <div className="flex flex-wrap gap-2 items-center">
          <span className="text-xs text-gray-500">Account:</span>
          {accounts.map(acct => (
            <div key={acct.id} className="flex items-center gap-1">
              <button
                onClick={() => setSelAccount(acct.id)}
                className={`flex items-center gap-1 px-3 py-1 text-sm rounded border ${selAccount === acct.id ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-gray-700 border-gray-300 hover:bg-gray-50'}`}
              >
                <StatusDot status={acct.status} /> {acct.label}
              </button>
              <button onClick={() => handleSync(acct.id)} disabled={syncing === acct.id}
                className="p-1 text-gray-400 hover:text-blue-600" title="Sync">
                <RefreshCw size={12} className={syncing === acct.id ? 'animate-spin' : ''} />
              </button>
              <button onClick={() => handleDeleteAccount(acct.id)}
                className="p-1 text-gray-300 hover:text-red-500" title="Remove">
                <Trash2 size={12} />
              </button>
            </div>
          ))}
        </div>
      )}

      {accounts.length === 0 ? (
        <p className="text-sm text-gray-500">No email accounts configured. Add an IMAP account to mirror your inbox.</p>
      ) : loading ? (
        <p className="text-sm text-gray-500">Loading messages…</p>
      ) : messages.length === 0 ? (
        <p className="text-sm text-gray-500">No messages. Click the sync button to fetch from server.</p>
      ) : (
        <div className="space-y-2">
          {messages.map(msg => (
            <div key={msg.id} className="bg-white border rounded p-3 shadow-sm">
              <div className="flex items-start justify-between cursor-pointer"
                onClick={() => setExpanded(expanded === msg.id ? null : msg.id)}>
                <div>
                  <div className={`text-sm font-medium ${msg.is_read ? 'text-gray-600' : 'text-gray-900'}`}>
                    {msg.subject || '(no subject)'}
                  </div>
                  <div className="text-xs text-gray-400">{msg.from_addr} · {fmtDate(msg.date)}</div>
                </div>
                {expanded === msg.id ? <ChevronUp size={14} className="text-gray-400 mt-0.5" /> : <ChevronDown size={14} className="text-gray-400 mt-0.5" />}
              </div>
              {expanded === msg.id && msg.body_text && (
                <div className="mt-2 pt-2 border-t text-sm text-gray-700 whitespace-pre-wrap break-words max-h-60 overflow-auto">
                  {msg.body_text}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

// ─── Tab: Calendar ────────────────────────────────────────────────────────────

const CalendarTab = () => {
  const [accounts, setAccounts] = useState<CalAccount[]>([]);
  const [events, setEvents] = useState<CalEvent[]>([]);
  const [selAccount, setSelAccount] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState<string | null>(null);
  const [showAdd, setShowAdd] = useState(false);
  const [form, setForm] = useState({ label: '', url: '', username: '', password: '', type: 'caldav', color: '#4f8ef7' });

  const loadAccounts = useCallback(async () => {
    try {
      const { data } = await axios.get<CalAccount[]>(`${API}/calendar/accounts`);
      setAccounts(data);
      if (data.length > 0 && !selAccount) setSelAccount(data[0].id);
    } catch { toast.error('Failed to load calendar accounts'); }
  }, [selAccount]);

  const loadEvents = useCallback(async (accountId: string) => {
    if (!accountId) return;
    setLoading(true);
    try {
      const { data } = await axios.get<CalEvent[]>(`${API}/calendar/events`, { params: { account_id: accountId } });
      setEvents(data);
    } catch { toast.error('Failed to load events'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { loadAccounts(); }, [loadAccounts]);
  useEffect(() => { if (selAccount) loadEvents(selAccount); }, [selAccount, loadEvents]);

  const handleAdd = async () => {
    if (!form.label || !form.url) { toast.error('Label and URL are required'); return; }
    try {
      await axios.post(`${API}/calendar/accounts`, form);
      toast.success('Calendar added');
      setShowAdd(false);
      setForm({ label: '', url: '', username: '', password: '', type: 'caldav', color: '#4f8ef7' });
      loadAccounts();
    } catch (e: any) { toast.error(e?.response?.data?.detail || 'Failed to add calendar'); }
  };

  const handleSync = async (id: string) => {
    setSyncing(id);
    try {
      const { data } = await axios.post<{ synced: number }>(`${API}/calendar/sync/${id}`);
      toast.success(`Synced ${data.synced} event(s)`);
      loadAccounts();
      if (id === selAccount) loadEvents(id);
    } catch (e: any) { toast.error(e?.response?.data?.detail || 'Sync failed'); }
    finally { setSyncing(null); }
  };

  const handleDeleteAccount = async (id: string) => {
    if (!window.confirm('Remove this calendar?')) return;
    try {
      await axios.delete(`${API}/calendar/accounts/${id}`);
      toast.success('Calendar removed');
      loadAccounts();
      if (id === selAccount) { setSelAccount(''); setEvents([]); }
    } catch { toast.error('Failed to remove calendar'); }
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <button onClick={() => setShowAdd(v => !v)}
          className="flex items-center gap-1 px-3 py-1.5 text-sm bg-green-100 hover:bg-green-200 text-green-700 rounded">
          <Plus size={14} /> Add Calendar
        </button>
      </div>

      {showAdd && (
        <div className="bg-gray-50 border rounded p-4 space-y-3">
          <h3 className="font-medium text-gray-800">Add CalDAV / iCal Calendar</h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className="block text-xs text-gray-600 mb-1">Label</label>
              <input className="w-full border rounded px-2 py-1.5 text-sm" placeholder="Work Calendar"
                value={form.label} onChange={e => setForm(f => ({ ...f, label: e.target.value }))} />
            </div>
            <div>
              <label className="block text-xs text-gray-600 mb-1">Type</label>
              <select className="w-full border rounded px-2 py-1.5 text-sm" value={form.type}
                onChange={e => setForm(f => ({ ...f, type: e.target.value }))}>
                <option value="caldav">CalDAV</option>
                <option value="ical">iCal (read-only URL)</option>
              </select>
            </div>
            <div className="sm:col-span-2">
              <label className="block text-xs text-gray-600 mb-1">URL</label>
              <input className="w-full border rounded px-2 py-1.5 text-sm"
                placeholder={form.type === 'ical' ? 'https://calendar.google.com/…/basic.ics' : 'https://dav.example.com/calendars/user/'}
                value={form.url} onChange={e => setForm(f => ({ ...f, url: e.target.value }))} />
            </div>
            {form.type === 'caldav' && <>
              <div>
                <label className="block text-xs text-gray-600 mb-1">Username</label>
                <input className="w-full border rounded px-2 py-1.5 text-sm"
                  value={form.username} onChange={e => setForm(f => ({ ...f, username: e.target.value }))} />
              </div>
              <div>
                <label className="block text-xs text-gray-600 mb-1">Password</label>
                <input type="password" className="w-full border rounded px-2 py-1.5 text-sm"
                  value={form.password} onChange={e => setForm(f => ({ ...f, password: e.target.value }))} />
              </div>
            </>}
            <div>
              <label className="block text-xs text-gray-600 mb-1">Color</label>
              <div className="flex items-center gap-2">
                <input type="color" className="h-8 w-14 border rounded cursor-pointer"
                  value={form.color} onChange={e => setForm(f => ({ ...f, color: e.target.value }))} />
                <span className="text-xs text-gray-500">{form.color}</span>
              </div>
            </div>
          </div>
          <div className="flex gap-2">
            <button onClick={handleAdd} className="px-4 py-1.5 bg-green-600 text-white text-sm rounded hover:bg-green-700">Add</button>
            <button onClick={() => setShowAdd(false)} className="px-4 py-1.5 bg-gray-200 text-sm rounded hover:bg-gray-300">Cancel</button>
          </div>
        </div>
      )}

      {accounts.length > 0 && (
        <div className="flex flex-wrap gap-2 items-center">
          <span className="text-xs text-gray-500">Calendar:</span>
          {accounts.map(acct => (
            <div key={acct.id} className="flex items-center gap-1">
              <button
                onClick={() => setSelAccount(acct.id)}
                className={`flex items-center gap-1 px-3 py-1 text-sm rounded border ${selAccount === acct.id ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-gray-700 border-gray-300 hover:bg-gray-50'}`}
                style={selAccount !== acct.id && acct.color ? { borderLeftColor: acct.color, borderLeftWidth: 3 } : {}}
              >
                <StatusDot status={acct.status} /> {acct.label}
                <span className="text-xs ml-1 opacity-60">{acct.type}</span>
              </button>
              <button onClick={() => handleSync(acct.id)} disabled={syncing === acct.id}
                className="p-1 text-gray-400 hover:text-blue-600" title="Sync">
                <RefreshCw size={12} className={syncing === acct.id ? 'animate-spin' : ''} />
              </button>
              <button onClick={() => handleDeleteAccount(acct.id)}
                className="p-1 text-gray-300 hover:text-red-500" title="Remove">
                <Trash2 size={12} />
              </button>
            </div>
          ))}
        </div>
      )}

      {accounts.length === 0 ? (
        <p className="text-sm text-gray-500">No calendars configured. Add a CalDAV or iCal URL to sync events.</p>
      ) : loading ? (
        <p className="text-sm text-gray-500">Loading events…</p>
      ) : events.length === 0 ? (
        <p className="text-sm text-gray-500">No events. Click the sync button to fetch from server.</p>
      ) : (
        <div className="space-y-2">
          {events.map(ev => (
            <div key={ev.id} className="bg-white border rounded p-3 shadow-sm flex items-start gap-3">
              <Calendar size={16} className="text-blue-400 mt-0.5 shrink-0" />
              <div>
                <div className="text-sm font-medium text-gray-800">{ev.summary || '(Untitled)'}</div>
                <div className="text-xs text-gray-500">
                  {ev.all_day ? 'All-day · ' : ''}
                  {ev.start_dt ? fmtDate(ev.start_dt) : '—'}
                  {ev.end_dt ? ` → ${fmtDate(ev.end_dt)}` : ''}
                </div>
                {ev.location && <div className="text-xs text-gray-400">📍 {ev.location}</div>}
                {ev.description && (
                  <div className="text-xs text-gray-500 mt-1 line-clamp-2">{ev.description}</div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

// ─── Tab: Web Browse ──────────────────────────────────────────────────────────

const BrowseTab = () => {
  const [history, setHistory] = useState<BrowseEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [browsing, setBrowsing] = useState(false);
  const [url, setUrl] = useState('');
  const [result, setResult] = useState<{ title: string; content_text: string; url: string } | null>(null);

  const loadHistory = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await axios.get<BrowseEntry[]>(`${API}/browse/history`);
      setHistory(data);
    } catch { toast.error('Failed to load history'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { loadHistory(); }, [loadHistory]);

  const handleBrowse = async () => {
    if (!url.trim()) { toast.error('Enter a URL'); return; }
    setBrowsing(true);
    setResult(null);
    try {
      const { data } = await axios.post<{ title: string; content_text: string; url: string }>(`${API}/browse`, { url: url.trim() });
      setResult(data);
      loadHistory();
    } catch (e: any) {
      toast.error(e?.response?.data?.detail || 'Browse failed');
    } finally {
      setBrowsing(false);
    }
  };

  const handleClearHistory = async () => {
    if (!window.confirm('Clear all browse history?')) return;
    try {
      await axios.delete(`${API}/browse/history`);
      setHistory([]);
      toast.success('History cleared');
    } catch { toast.error('Failed to clear history'); }
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <input
          className="flex-1 border rounded px-3 py-1.5 text-sm"
          placeholder="https://example.com"
          value={url}
          onChange={e => setUrl(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleBrowse()}
        />
        <button
          onClick={handleBrowse}
          disabled={browsing}
          className="flex items-center gap-1 px-4 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
        >
          <Globe size={14} /> {browsing ? 'Loading…' : 'Browse'}
        </button>
      </div>

      {result && (
        <div className="bg-white border rounded p-4 shadow-sm">
          <div className="flex items-center justify-between mb-2">
            <div>
              <h3 className="font-medium text-gray-800">{result.title || '(no title)'}</h3>
              <a href={result.url} target="_blank" rel="noopener noreferrer"
                className="text-xs text-blue-600 hover:underline flex items-center gap-0.5">
                {result.url} <ArrowUpRight size={10} />
              </a>
            </div>
          </div>
          <div className="text-sm text-gray-700 whitespace-pre-wrap max-h-80 overflow-auto border-t pt-2">
            {result.content_text || '(no content extracted)'}
          </div>
        </div>
      )}

      <div className="flex items-center justify-between">
        <h3 className="text-sm font-medium text-gray-700">Browse History</h3>
        {history.length > 0 && (
          <button onClick={handleClearHistory} className="text-xs text-red-400 hover:text-red-600">Clear history</button>
        )}
      </div>

      {loading ? <p className="text-sm text-gray-500">Loading…</p> :
        history.length === 0 ? <p className="text-sm text-gray-500">No browse history yet.</p> :
        <div className="space-y-1">
          {history.map(entry => (
            <div key={entry.id} className="flex items-center gap-2 py-1.5 px-2 rounded hover:bg-gray-50 cursor-pointer group"
              onClick={() => setUrl(entry.url)}>
              <Globe size={13} className="text-gray-400 shrink-0" />
              <div className="flex-1 min-w-0">
                <div className="text-sm text-gray-800 truncate">{entry.title || entry.url}</div>
                <div className="text-xs text-gray-400 truncate">{entry.url} · {fmtDate(entry.visited_at)}</div>
              </div>
            </div>
          ))}
        </div>
      }
    </div>
  );
};

// ─── Main Page ────────────────────────────────────────────────────────────────

type TabId = 'peers' | 'messages' | 'email' | 'calendar' | 'browse';

const TABS: { id: TabId; label: string; icon: React.ReactNode }[] = [
  { id: 'peers', label: 'Peers', icon: <Network size={15} /> },
  { id: 'messages', label: 'Messages', icon: <Send size={15} /> },
  { id: 'email', label: 'Email (IMAP)', icon: <Mail size={15} /> },
  { id: 'calendar', label: 'Calendar', icon: <Calendar size={15} /> },
  { id: 'browse', label: 'Web Browse', icon: <Globe size={15} /> },
];

const FederationPage = () => {
  const { resolved } = useEffectiveUser();
  const [activeTab, setActiveTab] = useState<TabId>('peers');

  if (!resolved) {
    return <div className="p-6 text-gray-500">Loading…</div>;
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      className="max-w-5xl mx-auto px-4 py-6"
    >
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
          <Network className="text-primary-600" size={26} />
          Federation &amp; Integrations
        </h1>
        <p className="text-gray-500 text-sm mt-1">
          Connect SwAIvyn instances, exchange AI tasks, sync email &amp; calendars, and browse the web.
        </p>
      </div>

      {/* Tab bar */}
      <div className="flex gap-1 border-b mb-6 overflow-x-auto">
        {TABS.map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors whitespace-nowrap ${
              activeTab === tab.id
                ? 'border-primary-600 text-primary-700'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      <div>
        {activeTab === 'peers' && <PeersTab />}
        {activeTab === 'messages' && <MessagesTab />}
        {activeTab === 'email' && <EmailTab />}
        {activeTab === 'calendar' && <CalendarTab />}
        {activeTab === 'browse' && <BrowseTab />}
      </div>
    </motion.div>
  );
};

export default FederationPage;
