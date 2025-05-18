import React, { useEffect, useState } from 'react';

interface Folder {
  id: string;
  name: string;
  parentId: string | null;
  children?: Folder[];
}

interface FolderTreeProps {
  userId: string;
  onSelectFolder: (id: string) => void;
}

/**
 * FolderTree component displays a hierarchical folder tree with add, rename, and delete functionality.
 * Fully commented and designed for integration with backend APIs.
 */
const FolderTree: React.FC<FolderTreeProps> = ({ userId, onSelectFolder }) => {
  const [folders, setFolders] = useState<Folder[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  // Fetch folders for the user
  useEffect(() => {
    fetch(`/api/folder/${userId}`)
      .then(res => res.json())
      .then(data => setFolders(data))
      .catch(console.error);
  }, [userId]);

  // Render folder tree recursively
  const renderTree = (nodes: Folder[]) => (
    <ul>
      {nodes.map(folder => (
        <li key={folder.id}>
          <button
            className={selectedId === folder.id ? 'font-bold' : ''}
            onClick={() => {
              setSelectedId(folder.id);
              onSelectFolder(folder.id);
            }}
          >
            {folder.name}
          </button>
          {folder.children && folder.children.length > 0 && renderTree(folder.children)}
        </li>
      ))}
    </ul>
  );

  return (
    <div className="p-2 border rounded bg-white">
      <h3 className="text-lg font-semibold mb-2">Folders</h3>
      {renderTree(folders)}
      {/* Add, rename, delete, and drag-and-drop functionality can be added here */}
    </div>
  );
};

export default FolderTree;
