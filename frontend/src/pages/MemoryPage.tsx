import { motion } from 'framer-motion';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  BookOpen,
  Calendar,
  Download,
  Filter,
  Globe,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  ShieldAlert,
  Trash2,
  Upload,
  User,
} from 'lucide-react';
import { toast } from 'react-toastify';
import InlineSpinner from '../components/ui/InlineSpinner';
import MemorySyncStatus from '../components/MemorySyncStatus';
import { useAuth } from '../contexts/AuthContext';
import useEffectiveUser from '../hooks/useEffectiveUser';

interface MemoryItem {
  id: string;
  userId: string;
  content: string;
  category: string;
  isShared: boolean;
  annotation?: string | null;
  createdAt: string;
  updatedAt: string;
}

interface MemoryDraft {
  content: string;
  category: string;
  isShared: boolean;
  annotation: string;
}

interface AdminMemoryStat {
  userId: string;
  count: number;
  lastUpdated?: string | null;
}

const PAGE_SIZE = 20;
const MEMORY_CATEGORIES = ['All', 'Personal', 'Facts', 'Events', 'Shared'];

const emptyDraft = (): MemoryDraft => ({
  content: '',
  category: 'Personal',
  isShared: false,
  annotation: '',
});

const MemoryPage = () => {
  const eff = useEffectiveUser();
  const { user } = useAuth();
  const [memories, setMemories] = useState<MemoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('All');
  const [page, setPage] = useState(1);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [showEditor, setShowEditor] = useState(false);
  const [editingMemoryId, setEditingMemoryId] = useState<string | null>(null);
  const [draft, setDraft] = useState<MemoryDraft>(emptyDraft);
  const [submitting, setSubmitting] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [importing, setImporting] = useState(false);
  const [clearingAll, setClearingAll] = useState(false);
  const [adminStats, setAdminStats] = useState<AdminMemoryStat[]>([]);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const isAdmin = user?.role === 'admin';

  const filteredCategory = useMemo(() => {
    if (selectedCategory === 'Shared') {
      return 'All';
    }
    return selectedCategory;
  }, [selectedCategory]);

  const visibleMemories = useMemo(() => {
    if (selectedCategory !== 'Shared') {
      return memories;
    }
    return memories.filter((memory) => memory.isShared);
  }, [memories, selectedCategory]);

  const loadMemories = useCallback(async (showRefresh = false) => {
    if (!eff.userId) {
      setLoading(false);
      return;
    }

    if (showRefresh) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }

    try {
      const params = new URLSearchParams({
        page: String(page),
        page_size: String(PAGE_SIZE),
      });
      if (searchTerm.trim()) {
        params.set('q', searchTerm.trim());
      }
      if (filteredCategory !== 'All') {
        params.set('category', filteredCategory);
      }

      const response = await fetch(`/api/memory/${encodeURIComponent(eff.userId)}?${params.toString()}`, {
        headers: eff.headers,
      });

      if (!response.ok) {
        throw new Error(`Failed to load memories (${response.status})`);
      }

      const data: MemoryItem[] = await response.json();
      setMemories(data || []);
      setHasNextPage((data || []).length === PAGE_SIZE);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to load memories: ${message}`, { toastId: 'memory-load-failed' });
      setMemories([]);
      setHasNextPage(false);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [eff.headers, eff.userId, filteredCategory, page, searchTerm]);

  const loadAdminStats = useCallback(async () => {
    if (!isAdmin) {
      setAdminStats([]);
      return;
    }

    try {
      const response = await fetch('/api/admin/memory/stats', { headers: eff.headers });
      if (!response.ok) {
        throw new Error(`Failed to load admin stats (${response.status})`);
      }
      const data: AdminMemoryStat[] = await response.json();
      setAdminStats(data || []);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to load memory stats: ${message}`, { toastId: 'memory-admin-stats' });
    }
  }, [eff.headers, isAdmin]);

  useEffect(() => {
    loadMemories();
  }, [loadMemories]);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      if (page !== 1) {
        setPage(1);
      } else {
        loadMemories();
      }
    }, 250);

    return () => window.clearTimeout(timeout);
  }, [loadMemories, page, searchTerm]);

  useEffect(() => {
    loadAdminStats();
  }, [loadAdminStats]);

  const openCreateModal = () => {
    setEditingMemoryId(null);
    setDraft(emptyDraft());
    setShowEditor(true);
  };

  const openEditModal = (memory: MemoryItem) => {
    setEditingMemoryId(memory.id);
    setDraft({
      content: memory.content,
      category: memory.category || 'Personal',
      isShared: memory.isShared,
      annotation: memory.annotation || '',
    });
    setShowEditor(true);
  };

  const closeEditor = () => {
    if (submitting) return;
    setShowEditor(false);
    setEditingMemoryId(null);
    setDraft(emptyDraft());
  };

  const saveMemory = async () => {
    if (!eff.userId || !draft.content.trim()) {
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        userId: eff.userId,
        content: draft.content.trim(),
        category: draft.category,
        isShared: draft.isShared,
        annotation: draft.annotation.trim(),
      };

      const response = await fetch(
        editingMemoryId ? `/api/memory/${editingMemoryId}` : '/api/memory',
        {
          method: editingMemoryId ? 'PUT' : 'POST',
          headers: {
            'Content-Type': 'application/json',
            ...eff.headers,
          },
          body: JSON.stringify(payload),
        }
      );

      if (!response.ok) {
        const detail = await response.text();
        throw new Error(detail || 'Request failed');
      }

      toast.success(editingMemoryId ? 'Memory updated' : 'Memory created');
      closeEditor();
      await loadMemories(true);
      if (isAdmin) {
        await loadAdminStats();
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to save memory: ${message}`, { toastId: 'memory-save-failed' });
    } finally {
      setSubmitting(false);
    }
  };

  const deleteMemory = async (memoryId: string) => {
    if (!window.confirm('Delete this memory?')) {
      return;
    }

    try {
      const response = await fetch(`/api/memory/${memoryId}`, {
        method: 'DELETE',
        headers: eff.headers,
      });

      if (!response.ok) {
        throw new Error(`Delete failed (${response.status})`);
      }

      toast.success('Memory deleted');
      await loadMemories(true);
      if (isAdmin) {
        await loadAdminStats();
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to delete memory: ${message}`, { toastId: `memory-delete-${memoryId}` });
    }
  };

  const toggleShared = async (memory: MemoryItem) => {
    try {
      const response = await fetch(`/api/memory/${memory.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          ...eff.headers,
        },
        body: JSON.stringify({ isShared: !memory.isShared }),
      });

      if (!response.ok) {
        throw new Error(`Update failed (${response.status})`);
      }

      toast.success(memory.isShared ? 'Memory made private' : 'Memory shared');
      await loadMemories(true);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to update sharing: ${message}`, { toastId: `memory-share-${memory.id}` });
    }
  };

  const exportMemories = async () => {
    if (!eff.userId) return;
    setExporting(true);
    try {
      const response = await fetch(`/api/memory/user/${encodeURIComponent(eff.userId)}/export`, {
        headers: eff.headers,
      });
      if (!response.ok) {
        throw new Error(`Export failed (${response.status})`);
      }
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `memories-${eff.userId}.json`;
      anchor.click();
      URL.revokeObjectURL(url);
      toast.success('Memory export downloaded');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to export memories: ${message}`, { toastId: 'memory-export-failed' });
    } finally {
      setExporting(false);
    }
  };

  const importMemories = async (file: File) => {
    if (!eff.userId) return;
    setImporting(true);
    try {
      const text = await file.text();
      const response = await fetch(`/api/memory/user/${encodeURIComponent(eff.userId)}/import`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...eff.headers,
        },
        body: text,
      });
      if (!response.ok) {
        const detail = await response.text();
        throw new Error(detail || `Import failed (${response.status})`);
      }
      const result = await response.json();
      toast.success(`Imported ${result.imported ?? 0} memories`);
      await loadMemories(true);
      if (isAdmin) {
        await loadAdminStats();
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to import memories: ${message}`, { toastId: 'memory-import-failed' });
    } finally {
      setImporting(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const clearAllMemories = async () => {
    if (!eff.userId) return;
    if (!window.confirm('Delete all memories for this user? This cannot be undone.')) {
      return;
    }

    setClearingAll(true);
    try {
      const response = await fetch(`/api/memory/user/${encodeURIComponent(eff.userId)}/all`, {
        method: 'DELETE',
        headers: eff.headers,
      });
      if (!response.ok) {
        throw new Error(`Clear failed (${response.status})`);
      }
      const result = await response.json();
      toast.success(`Deleted ${result.deleted ?? 0} memories`);
      setPage(1);
      await loadMemories(true);
      if (isAdmin) {
        await loadAdminStats();
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Failed to clear memories: ${message}`, { toastId: 'memory-clear-failed' });
    } finally {
      setClearingAll(false);
    }
  };

  return (
    <motion.div
      className="min-h-[calc(100vh-64px)] bg-gray-50 p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="max-w-6xl mx-auto space-y-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <h1 className="text-2xl font-medium text-gray-800">Memory</h1>
            <p className="text-gray-600">Search, edit, share, import, and export your AI memories.</p>
          </div>

          <div className="flex flex-wrap gap-2">
            <button className="btn btn-primary" onClick={openCreateModal}>
              <Plus size={16} className="mr-1.5" />
              Add Memory
            </button>
            <button className="btn btn-ghost border border-gray-300" onClick={() => loadMemories(true)} disabled={refreshing}>
              <RefreshCw size={16} className={`mr-1.5 ${refreshing ? 'animate-spin' : ''}`} />
              Refresh
            </button>
            <button className="btn btn-ghost border border-gray-300" onClick={exportMemories} disabled={exporting || !eff.userId}>
              <Download size={16} className="mr-1.5" />
              {exporting ? 'Exporting...' : 'Export'}
            </button>
            <button
              className="btn btn-ghost border border-gray-300"
              onClick={() => fileInputRef.current?.click()}
              disabled={importing || !eff.userId}
            >
              <Upload size={16} className="mr-1.5" />
              {importing ? 'Importing...' : 'Import'}
            </button>
            <button className="btn btn-ghost border border-red-200 text-red-600" onClick={clearAllMemories} disabled={clearingAll || !eff.userId}>
              <Trash2 size={16} className="mr-1.5" />
              {clearingAll ? 'Clearing...' : 'Clear All'}
            </button>
            <input
              ref={fileInputRef}
              type="file"
              accept="application/json,.json"
              className="hidden"
              onChange={(event) => {
                const file = event.target.files?.[0];
                if (file) {
                  void importMemories(file);
                }
              }}
            />
          </div>
        </div>

        {eff.userId && <MemorySyncStatus userId={eff.userId} />}

        {isAdmin && adminStats.length > 0 && (
          <div className="bg-white rounded-lg shadow-soft border p-4">
            <div className="flex items-center gap-2 mb-3">
              <ShieldAlert size={18} className="text-amber-600" />
              <h2 className="text-sm font-semibold text-gray-800">Admin Summary</h2>
            </div>
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              {adminStats.slice(0, 4).map((stat) => (
                <div key={stat.userId} className="rounded-lg border border-gray-200 bg-gray-50 p-3">
                  <div className="text-xs text-gray-500 truncate">{stat.userId}</div>
                  <div className="text-xl font-semibold text-gray-900">{stat.count}</div>
                  <div className="text-xs text-gray-500">{formatDate(stat.lastUpdated || '')}</div>
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="bg-white rounded-lg shadow-soft">
          <div className="p-4 border-b space-y-3">
            <div className="flex flex-col gap-2 lg:flex-row">
              <div className="relative flex-grow">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" size={18} />
                <input
                  type="text"
                  placeholder="Search memory content or annotations..."
                  value={searchTerm}
                  onChange={(event) => setSearchTerm(event.target.value)}
                  className="pl-10 pr-4 py-2 w-full border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
                />
              </div>
              <div className="flex items-center rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-gray-600">
                <Filter size={16} className="mr-2" />
                {visibleMemories.length} item{visibleMemories.length === 1 ? '' : 's'} on this page
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              {MEMORY_CATEGORIES.map((category) => (
                <button
                  key={category}
                  onClick={() => {
                    setSelectedCategory(category);
                    setPage(1);
                  }}
                  className={`px-3 py-1.5 text-sm font-medium rounded-md ${
                    selectedCategory === category
                      ? 'bg-primary-50 text-primary-700'
                      : 'text-gray-700 hover:bg-gray-100'
                  }`}
                >
                  {category === 'Personal' && <User size={14} className="inline mr-1" />}
                  {category === 'Facts' && <BookOpen size={14} className="inline mr-1" />}
                  {category === 'Events' && <Calendar size={14} className="inline mr-1" />}
                  {category === 'Shared' && <Globe size={14} className="inline mr-1" />}
                  {category}
                </button>
              ))}
            </div>
          </div>

          <div className="divide-y">
            {loading ? (
              <div className="p-8 text-center">
                <InlineSpinner />
                <p className="mt-2 text-gray-500">Loading memories...</p>
              </div>
            ) : visibleMemories.length === 0 ? (
              <div className="p-8 text-center">
                <BookOpen size={48} className="mx-auto text-gray-400 mb-4" />
                <h3 className="text-lg font-medium text-gray-900 mb-2">No memories found</h3>
                <p className="text-gray-600 mb-4">
                  {searchTerm || selectedCategory !== 'All'
                    ? 'Try adjusting your search or category filters.'
                    : 'Create a memory, import a backup, or let conversation workflows populate this list.'}
                </p>
                <button className="btn btn-primary" onClick={openCreateModal}>
                  <Plus size={16} className="mr-1.5" />
                  Add Memory
                </button>
              </div>
            ) : (
              visibleMemories.map((memory) => (
                <MemoryRow
                  key={memory.id}
                  memory={memory}
                  onEdit={openEditModal}
                  onDelete={deleteMemory}
                  onToggleShared={toggleShared}
                />
              ))
            )}
          </div>

          <div className="flex items-center justify-between border-t px-4 py-3">
            <span className="text-sm text-gray-500">Page {page}</span>
            <div className="flex gap-2">
              <button
                className="btn btn-ghost border border-gray-300"
                onClick={() => setPage((current) => Math.max(1, current - 1))}
                disabled={page === 1}
              >
                Previous
              </button>
              <button
                className="btn btn-ghost border border-gray-300"
                onClick={() => setPage((current) => current + 1)}
                disabled={!hasNextPage}
              >
                Next
              </button>
            </div>
          </div>
        </div>
      </div>

      {showEditor && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4">
          <div className="w-full max-w-2xl rounded-lg bg-white p-6 shadow-xl">
            <div className="flex items-start justify-between mb-4">
              <div>
                <h2 className="text-xl font-semibold text-gray-900">
                  {editingMemoryId ? 'Edit Memory' : 'Add Memory'}
                </h2>
                <p className="text-sm text-gray-500">Content, category, notes, and sharing are all persisted.</p>
              </div>
              <button onClick={closeEditor} className="text-sm text-gray-500 hover:text-gray-700" disabled={submitting}>
                Close
              </button>
            </div>

            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Content</label>
                <textarea
                  value={draft.content}
                  onChange={(event) => setDraft((current) => ({ ...current, content: event.target.value }))}
                  rows={5}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
                  placeholder="Remember that the user prefers..."
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Category</label>
                  <select
                    value={draft.category}
                    onChange={(event) => setDraft((current) => ({ ...current, category: event.target.value }))}
                    className="w-full rounded-md border border-gray-300 px-3 py-2 focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
                  >
                    <option value="Personal">Personal</option>
                    <option value="Facts">Facts</option>
                    <option value="Events">Events</option>
                    <option value="Shared">Shared</option>
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Annotation</label>
                  <input
                    value={draft.annotation}
                    onChange={(event) => setDraft((current) => ({ ...current, annotation: event.target.value }))}
                    className="w-full rounded-md border border-gray-300 px-3 py-2 focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500"
                    placeholder="Optional note about why this matters"
                  />
                </div>
              </div>

              <label className="flex items-center gap-2 text-sm text-gray-700">
                <input
                  type="checkbox"
                  checked={draft.isShared}
                  onChange={(event) => setDraft((current) => ({ ...current, isShared: event.target.checked }))}
                />
                Share this memory
              </label>
            </div>

            <div className="mt-6 flex justify-end gap-2">
              <button className="btn btn-ghost" onClick={closeEditor} disabled={submitting}>
                Cancel
              </button>
              <button className="btn btn-primary" onClick={saveMemory} disabled={submitting || !draft.content.trim()}>
                {submitting ? 'Saving...' : editingMemoryId ? 'Save Changes' : 'Create Memory'}
              </button>
            </div>
          </div>
        </div>
      )}
    </motion.div>
  );
};

interface MemoryRowProps {
  memory: MemoryItem;
  onEdit: (memory: MemoryItem) => void;
  onDelete: (memoryId: string) => void;
  onToggleShared: (memory: MemoryItem) => void;
}

const MemoryRow = ({ memory, onEdit, onDelete, onToggleShared }: MemoryRowProps) => {
  const icon = getIconForCategory(memory.category);

  return (
    <div className="p-4 hover:bg-gray-50 transition-colors duration-150">
      <div className="flex items-start gap-3">
        <div className="mt-0.5">{icon}</div>
        <div className="flex-grow min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs font-medium text-gray-500 bg-gray-100 px-2 py-0.5 rounded">
              {memory.category}
            </span>
            {memory.isShared && (
              <span className="text-xs font-medium text-secondary-600 bg-secondary-50 px-2 py-0.5 rounded flex items-center">
                <Globe size={12} className="mr-1" />
                Shared
              </span>
            )}
            <span className="ml-auto text-xs text-gray-500">
              Updated {formatDate(memory.updatedAt || memory.createdAt)}
            </span>
          </div>

          <p className="mt-2 text-gray-900 whitespace-pre-wrap break-words">{memory.content}</p>

          {memory.annotation && (
            <p className="mt-2 text-sm text-gray-600 border-l-2 border-gray-200 pl-3">{memory.annotation}</p>
          )}

          <div className="mt-3 flex flex-wrap gap-3 text-xs">
            <button className="text-gray-500 hover:text-gray-800 flex items-center gap-1" onClick={() => onEdit(memory)}>
              <Pencil size={12} />
              Edit
            </button>
            <button className="text-gray-500 hover:text-gray-800 flex items-center gap-1" onClick={() => onToggleShared(memory)}>
              <Globe size={12} />
              {memory.isShared ? 'Make Private' : 'Share'}
            </button>
            <button className="text-red-500 hover:text-red-700 flex items-center gap-1" onClick={() => onDelete(memory.id)}>
              <Trash2 size={12} />
              Delete
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

function getIconForCategory(category: string) {
  switch (category) {
    case 'Personal':
      return <User size={16} className="text-primary-500" />;
    case 'Facts':
      return <BookOpen size={16} className="text-secondary-500" />;
    case 'Events':
      return <Calendar size={16} className="text-accent-500" />;
    case 'Shared':
      return <Globe size={16} className="text-green-500" />;
    default:
      return <BookOpen size={16} className="text-gray-500" />;
  }
}

function formatDate(value: string) {
  if (!value) return 'Unknown';
  try {
    return new Date(value).toLocaleString();
  } catch {
    return value;
  }
}

export default MemoryPage;
