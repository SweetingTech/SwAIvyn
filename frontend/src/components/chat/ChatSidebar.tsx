import React, { useState, useEffect } from 'react';
import { Folder, Plus, Edit2, Trash2, MessageSquare, ChevronRight, ChevronDown } from 'lucide-react';
import apiService from '../../services/apiService';

interface ChatSession {
  id: string;
  title: string;
  lastUpdated: string;
  folderId: string | null;
}

interface ChatFolder {
  id: string;
  name: string;
  parentId: string | null;
}

interface ChatSidebarProps {
  userId: string;
  currentSessionId: string | null;
  onSelectSession: (sessionId: string) => void;
  onNewSession: () => void;
}

/**
 * ChatSidebar component displays folders and chat sessions,
 * allowing users to organize and manage their conversations.
 */
const ChatSidebar: React.FC<ChatSidebarProps> = ({
  userId,
  currentSessionId,
  onSelectSession,
  onNewSession
}) => {
  // State for folders and chat sessions
  const [folders, setFolders] = useState<ChatFolder[]>([]);
  const [sessions, setSessions] = useState<ChatSession[]>([]);
  const [expandedFolders, setExpandedFolders] = useState<Record<string, boolean>>({});

  // State for editing
  const [editingFolderId, setEditingFolderId] = useState<string | null>(null);
  const [editingSessionId, setEditingSessionId] = useState<string | null>(null);
  const [editName, setEditName] = useState('');

  // State for creating new folder
  const [isCreatingFolder, setIsCreatingFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState('');
  const [newFolderParentId, setNewFolderParentId] = useState<string | null>(null);

  // Fetch folders and sessions on component mount
  useEffect(() => {
    const fetchData = async () => {
      // IMPORTANT: Skip API calls completely for demo-user-id
      if (!userId || userId === 'demo-user-id') {
        console.warn('Demo user ID detected, skipping API calls and using empty data');
        setFolders([]);
        setSessions([]);
        return;
      }

      try {
        // Fetch folders
        const folderResponse = await apiService.get(`/api/folder/user/${userId}`);
        setFolders(folderResponse.data);

        // Initialize expanded state for root folders
        const expanded: Record<string, boolean> = {};
        folderResponse.data.forEach((folder: ChatFolder) => {
          if (!folder.parentId) {
            expanded[folder.id] = true;
          }
        });
        setExpandedFolders(expanded);

        // Fetch sessions
        const sessionResponse = await apiService.get(`/api/conversation/user/${userId}`);
        setSessions(sessionResponse.data);
      } catch (error) {
        console.error('Error fetching data:', error);
        // Set empty arrays on error
        setFolders([]);
        setSessions([]);
      }
    };

    fetchData();
  }, [userId]);

  // Toggle folder expansion
  const toggleFolder = (folderId: string) => {
    setExpandedFolders(prev => ({
      ...prev,
      [folderId]: !prev[folderId]
    }));
  };

  // Create a new folder
  const createFolder = async () => {
    if (!newFolderName.trim()) return;

    // Skip API call if userId is not valid
    if (!userId || userId === 'demo-user-id') {
      console.warn('Invalid user ID for folder creation, using mock folder');

      // Create a mock folder with a temporary ID
      const mockFolder: ChatFolder = {
        id: `temp-${Date.now()}`,
        name: newFolderName,
        parentId: newFolderParentId
      };

      setFolders(prev => [...prev, mockFolder]);
      setIsCreatingFolder(false);
      setNewFolderName('');
      setNewFolderParentId(null);
      return;
    }

    try {
      const response = await apiService.post('/api/folder', {
        userId,
        name: newFolderName,
        parentId: newFolderParentId
      });

      setFolders(prev => [...prev, response.data]);
      setIsCreatingFolder(false);
      setNewFolderName('');
      setNewFolderParentId(null);
    } catch (error) {
      console.error('Error creating folder:', error);

      // Create a mock folder on error
      const mockFolder: ChatFolder = {
        id: `temp-${Date.now()}`,
        name: newFolderName,
        parentId: newFolderParentId
      };

      setFolders(prev => [...prev, mockFolder]);
      setIsCreatingFolder(false);
      setNewFolderName('');
      setNewFolderParentId(null);
    }
  };

  // Rename a folder
  const renameFolder = async (folderId: string) => {
    if (!editName.trim()) return;

    try {
      await apiService.put(`/api/folder/${folderId}/name`, {
        name: editName
      });

      setFolders(prev => prev.map(folder =>
        folder.id === folderId ? { ...folder, name: editName } : folder
      ));

      setEditingFolderId(null);
      setEditName('');
    } catch (error) {
      console.error('Error renaming folder:', error);
    }
  };

  // Delete a folder
  const deleteFolder = async (folderId: string) => {
    if (!confirm('Delete this folder and all its contents?')) return;

    try {
      await apiService.delete(`/api/folder/${folderId}`);

      // Remove folder from state
      setFolders(prev => prev.filter(folder => folder.id !== folderId));

      // Remove sessions in this folder
      setSessions(prev => prev.filter(session => session.folderId !== folderId));
    } catch (error) {
      console.error('Error deleting folder:', error);
    }
  };

  // Rename a session
  const renameSession = async (sessionId: string) => {
    if (!editName.trim()) return;

    try {
      await apiService.put(`/api/conversation/${sessionId}/title`, {
        title: editName
      });

      setSessions(prev => prev.map(session =>
        session.id === sessionId ? { ...session, title: editName } : session
      ));

      setEditingSessionId(null);
      setEditName('');
    } catch (error) {
      console.error('Error renaming session:', error);
    }
  };

  // Delete a session
  const deleteSession = async (sessionId: string) => {
    if (!confirm('Delete this chat session?')) return;

    try {
      await apiService.delete(`/api/conversation/${sessionId}`);

      // Remove session from state
      setSessions(prev => prev.filter(session => session.id !== sessionId));

      // If current session was deleted, create a new one
      if (sessionId === currentSessionId) {
        onNewSession();
      }
    } catch (error) {
      console.error('Error deleting session:', error);
    }
  };

  // Render folder tree recursively
  const renderFolders = (parentId: string | null = null) => {
    const childFolders = folders.filter(folder => folder.parentId === parentId);

    return childFolders.map(folder => (
      <div key={folder.id} className="ml-2">
        <div className="flex items-center py-1 group">
          <button
            onClick={() => toggleFolder(folder.id)}
            className="mr-1 text-gray-500 hover:text-gray-700"
          >
            {expandedFolders[folder.id] ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
          </button>

          <Folder size={16} className="mr-2 text-gray-500" />

          {editingFolderId === folder.id ? (
            <div className="flex items-center flex-grow">
              <input
                type="text"
                value={editName}
                onChange={e => setEditName(e.target.value)}
                className="flex-grow px-2 py-1 text-sm border rounded"
                autoFocus
                onBlur={() => renameFolder(folder.id)}
                onKeyDown={e => e.key === 'Enter' && renameFolder(folder.id)}
              />
            </div>
          ) : (
            <>
              <span className="flex-grow text-sm">{folder.name}</span>
              <div className="hidden group-hover:flex items-center">
                <button
                  onClick={() => {
                    setEditingFolderId(folder.id);
                    setEditName(folder.name);
                  }}
                  className="p-1 text-gray-500 hover:text-gray-700"
                >
                  <Edit2 size={14} />
                </button>
                <button
                  onClick={() => deleteFolder(folder.id)}
                  className="p-1 text-gray-500 hover:text-red-500"
                >
                  <Trash2 size={14} />
                </button>
              </div>
            </>
          )}
        </div>

        {expandedFolders[folder.id] && (
          <div className="ml-4">
            {/* Render child folders */}
            {renderFolders(folder.id)}

            {/* Render sessions in this folder */}
            {sessions
              .filter(session => session.folderId === folder.id)
              .map(session => renderSession(session))}
          </div>
        )}
      </div>
    ));
  };

  // Render a chat session
  const renderSession = (session: ChatSession) => (
    <div
      key={session.id}
      className={`flex items-center py-1 pl-2 group rounded ${
        currentSessionId === session.id ? 'bg-gray-100' : 'hover:bg-gray-50'
      }`}
    >
      <MessageSquare size={14} className="mr-2 text-gray-500" />

      {editingSessionId === session.id ? (
        <div className="flex items-center flex-grow">
          <input
            type="text"
            value={editName}
            onChange={e => setEditName(e.target.value)}
            className="flex-grow px-2 py-1 text-sm border rounded"
            autoFocus
            onBlur={() => renameSession(session.id)}
            onKeyDown={e => e.key === 'Enter' && renameSession(session.id)}
          />
        </div>
      ) : (
        <>
          <button
            onClick={() => onSelectSession(session.id)}
            className="flex-grow text-left text-sm truncate"
          >
            {session.title}
          </button>
          <div className="hidden group-hover:flex items-center">
            <button
              onClick={() => {
                setEditingSessionId(session.id);
                setEditName(session.title);
              }}
              className="p-1 text-gray-500 hover:text-gray-700"
            >
              <Edit2 size={14} />
            </button>
            <button
              onClick={() => deleteSession(session.id)}
              className="p-1 text-gray-500 hover:text-red-500"
            >
              <Trash2 size={14} />
            </button>
          </div>
        </>
      )}
    </div>
  );

  return (
    <div className="h-full flex flex-col bg-white border-r">
      <div className="p-4 border-b">
        <button
          onClick={onNewSession}
          className="w-full flex items-center justify-center py-2 px-4 bg-primary-500 text-white rounded hover:bg-primary-600"
        >
          <Plus size={16} className="mr-2" />
          New Chat
        </button>
      </div>

      <div className="flex-grow overflow-y-auto p-2">
        {/* Uncategorized sessions */}
        <div className="mb-4">
          <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2">
            Recent Chats
          </h3>
          {sessions
            .filter(session => !session.folderId)
            .map(session => renderSession(session))}
        </div>

        {/* Folders */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider">
              Folders
            </h3>
            <button
              onClick={() => setIsCreatingFolder(true)}
              className="p-1 text-gray-500 hover:text-gray-700"
            >
              <Plus size={14} />
            </button>
          </div>

          {isCreatingFolder && (
            <div className="mb-2 p-2 border rounded">
              <input
                type="text"
                placeholder="Folder name"
                value={newFolderName}
                onChange={e => setNewFolderName(e.target.value)}
                className="w-full px-2 py-1 text-sm border rounded mb-2"
                autoFocus
              />
              <div className="flex justify-end space-x-2">
                <button
                  onClick={() => setIsCreatingFolder(false)}
                  className="px-2 py-1 text-xs text-gray-500 hover:text-gray-700"
                >
                  Cancel
                </button>
                <button
                  onClick={createFolder}
                  className="px-2 py-1 text-xs bg-primary-500 text-white rounded hover:bg-primary-600"
                >
                  Create
                </button>
              </div>
            </div>
          )}

          {renderFolders()}
        </div>
      </div>
    </div>
  );
};

export default ChatSidebar;
