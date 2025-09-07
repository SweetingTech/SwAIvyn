/**
 * ConversationsPage component displays a list of chat conversations for a user,
 * allowing creation, selection, and deletion of conversations.
 */

import { useEffect, useState } from 'react';
import useEffectiveUser from '../hooks/useEffectiveUser';
import ConversationSearch from '../components/ConversationSearch';

interface Conversation {
  id: string;
  userId: string;
  title: string;
  createdAt: string;
  lastUpdated: string;
}

interface ConversationsPageProps {
  userId: string;
  onSelectConversation: (conversationId: string) => void;
}

const ConversationsPage = ({ userId, onSelectConversation }: ConversationsPageProps) => {
  const eff = useEffectiveUser();
  // State to hold the list of conversations
  const [conversations, setConversations] = useState<Conversation[]>([]);

  // State for the new conversation title input
  const [newTitle, setNewTitle] = useState('');

  // State for search query
  const [searchQuery, setSearchQuery] = useState('');

  // Fetch conversations for the user when userId changes
  useEffect(() => {
    fetch(`/api/conversation/${userId}`, { headers: eff.headers })
      .then(res => res.json())
      .then(data => setConversations(data))
      .catch(console.error);
  }, [userId, eff.headers]);

  // Filter conversations by search query (title/content)
  const filteredConversations = conversations.filter(c =>
    c.title.toLowerCase().includes(searchQuery.toLowerCase())
    // Add content search if available: || c.content?.toLowerCase().includes(searchQuery.toLowerCase())
  );

  /**
   * Creates a new conversation with the entered title.
   */
  const createConversation = async () => {
    if (!newTitle.trim()) return;
    const response = await fetch('/api/conversation', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...eff.headers },
      body: JSON.stringify({ userId, title: newTitle }),
    });
    if (response.ok) {
      const created = await response.json();
      setConversations(prev => [created, ...prev]);
      setNewTitle('');
    }
  };

  /**
   * Deletes a conversation by ID.
   * @param id The ID of the conversation to delete.
   */
  const deleteConversation = async (id: string) => {
    const response = await fetch(`/api/conversation/${id}?userId=${encodeURIComponent(userId)}`, { method: 'DELETE', headers: eff.headers });
    if (response.ok) {
      setConversations(prev => prev.filter(c => c.id !== id));
    }
  };

  return (
    <div className="p-4">
      <h2 className="text-xl font-semibold mb-4">Conversations</h2>
      {/* Conversation search UI */}
      <ConversationSearch onSearch={setSearchQuery} />
      <div className="mb-4 flex space-x-2">
        <input
          type="text"
          placeholder="New conversation title"
          value={newTitle}
          onChange={e => setNewTitle(e.target.value)}
          className="flex-grow border border-gray-300 rounded px-3 py-2"
        />
        <button
          onClick={createConversation}
          className="bg-primary-500 text-white px-4 py-2 rounded hover:bg-primary-600"
        >
          Create
        </button>
      </div>
      <ul>
        {filteredConversations.map(convo => (
          <li key={convo.id} className="flex justify-between items-center py-2 border-b">
            <button
              onClick={() => onSelectConversation(convo.id)}
              className="text-left text-blue-600 hover:underline flex-grow"
            >
              {convo.title}
            </button>
            <button
              onClick={() => deleteConversation(convo.id)}
              className="text-red-600 hover:underline ml-4"
            >
              Delete
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
};

export default ConversationsPage;
