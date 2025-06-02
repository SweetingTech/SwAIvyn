import React, { useState, useEffect } from 'react';

interface MemoryItem {
  id: string;
  content: string;
  category: string;
  isShared: boolean;
  createdAt: string;
  lastAccessed: string;
}

interface MemoryBrowserProps {
  userId: string;
}

/**
 * MemoryBrowser component allows users to browse, create, edit, and delete AI memory items.
 * Fully commented and designed for integration with backend APIs.
 */
const MemoryBrowser: React.FC<MemoryBrowserProps> = ({ userId }) => {
  const [memories, setMemories] = useState<MemoryItem[]>([]);
  const [newContent, setNewContent] = useState('');
  const [newCategory, setNewCategory] = useState('');
  const [newIsShared, setNewIsShared] = useState(false);

  // Fetch memories for the single-user app
  useEffect(() => {
    fetch(`/api/memory`)
      .then(res => res.json())
      .then(responseData => setMemories(responseData.memories || []))
      .catch(console.error);
  }, []);

  // Create a new memory
  const createMemory = async () => {
    if (!newContent.trim()) return;
    const response = await fetch(`/api/memory`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        content: newContent,
        category: newCategory,
        isShared: newIsShared
      }),
    });
    if (response.ok) {
      const created = await response.json();
      setMemories(prev => [created, ...prev]);
      setNewContent('');
      setNewCategory('');
      setNewIsShared(false);
    }
  };

  // Delete a memory
  const deleteMemory = async (id: string) => {
    const response = await fetch(`/api/memory/${id}`, { method: 'DELETE' });
    if (response.ok) {
      setMemories(prev => prev.filter(m => m.id !== id));
    }
  };

  return (
    <div className="p-4 max-w-lg mx-auto">
      <h2 className="text-xl font-semibold mb-4">Memory Browser</h2>
      <div className="mb-4">
        <textarea
          placeholder="New memory content"
          value={newContent}
          onChange={e => setNewContent(e.target.value)}
          className="w-full border border-gray-300 rounded px-3 py-2 mb-2"
          rows={3}
        />
        <input
          type="text"
          placeholder="Category"
          value={newCategory}
          onChange={e => setNewCategory(e.target.value)}
          className="w-full border border-gray-300 rounded px-3 py-2 mb-2"
        />
        <label className="inline-flex items-center space-x-2 mb-2">
          <input
            type="checkbox"
            checked={newIsShared}
            onChange={e => setNewIsShared(e.target.checked)}
          />
          <span>Shared</span>
        </label>
        <button
          onClick={createMemory}
          className="bg-primary-500 text-white px-4 py-2 rounded hover:bg-primary-600"
        >
          Add Memory
        </button>
      </div>
      <ul>
        {memories.map(memory => (
          <li key={memory.id} className="border-b py-2">
            <p>{memory.content}</p>
            <p className="text-sm text-gray-500">Category: {memory.category}</p>
            <p className="text-sm text-gray-500">Shared: {memory.isShared ? 'Yes' : 'No'}</p>
            <button
              onClick={() => deleteMemory(memory.id)}
              className="text-red-600 hover:underline mt-1"
            >
              Delete
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
};

export default MemoryBrowser;
