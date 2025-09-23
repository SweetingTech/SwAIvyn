import React, { useState, useEffect } from 'react';
import { File, Search, Database, Trash2, Loader2 } from 'lucide-react';
import apiService from '../../services/apiService';
import useEffectiveUser from '../../hooks/useEffectiveUser';

interface UploadedFile {
  id: string;
  filename: string;
  size: number;
  uploadedAt: string;
  conversationId: string;
  hasEmbedding?: boolean;
  embeddingStatus?: 'pending' | 'completed' | 'failed';
}

interface FileManagerProps {
  conversationId: string;
  onFileDeleted?: (fileId: string) => void;
}

const FileManager: React.FC<FileManagerProps> = ({ 
  conversationId, 
  onFileDeleted 
}) => {
  const [files, setFiles] = useState<UploadedFile[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [searching, setSearching] = useState(false);
  const eff = useEffectiveUser();

  // Load files for current conversation
  useEffect(() => {
    loadFiles();
  }, [conversationId]);

  const loadFiles = async () => {
    if (!conversationId) return;
    
    try {
      setLoading(true);
      const response = await apiService.get(`/api/files/conversation/${conversationId}`, {
        headers: eff.headers
      });
      setFiles(response.files || []);
    } catch (error) {
      console.error('Error loading files:', error);
      setFiles([]);
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteFile = async (fileId: string) => {
    if (!confirm('Are you sure you want to delete this file?')) return;
    
    try {
      await apiService.delete(`/api/files/${fileId}`, {
        headers: eff.headers
      });
      
      setFiles(prev => prev.filter(f => f.id !== fileId));
      onFileDeleted?.(fileId);
    } catch (error) {
      console.error('Error deleting file:', error);
      alert('Failed to delete file');
    }
  };

  const handleSearchEmbeddings = async () => {
    if (!searchQuery.trim() || !conversationId) return;
    
    try {
      setSearching(true);
      const response = await apiService.post('/api/embeddings/search', {
        query: searchQuery,
        conversationId: conversationId,
        limit: 10
      }, {
        headers: eff.headers
      });
      
      setSearchResults(response.results || []);
    } catch (error) {
      console.error('Error searching embeddings:', error);
      setSearchResults([]);
    } finally {
      setSearching(false);
    }
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const formatDate = (dateString: string): string => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
  };

  if (loading) {
    return (
      <div className="p-4 text-center">
        <Loader2 className="w-6 h-6 animate-spin mx-auto mb-2" />
        <p className="text-sm text-gray-500">Loading files...</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* File List */}
      <div>
        <h4 className="text-sm font-medium text-gray-700 mb-2">
          Uploaded Files ({files.length})
        </h4>
        
        {files.length === 0 ? (
          <p className="text-sm text-gray-500 italic">
            No files uploaded yet
          </p>
        ) : (
          <div className="space-y-2 max-h-40 overflow-y-auto">
            {files.map((file) => (
              <div
                key={file.id}
                className="flex items-center justify-between p-2 bg-gray-50 rounded-lg text-sm"
              >
                <div className="flex items-center flex-1 min-w-0">
                  <File size={14} className="text-gray-400 mr-2 flex-shrink-0" />
                  <div className="flex-1 min-w-0">
                    <p className="font-medium truncate" title={file.filename}>
                      {file.filename}
                    </p>
                    <p className="text-xs text-gray-500">
                      {formatFileSize(file.size)} • {formatDate(file.uploadedAt)}
                    </p>
                  </div>
                  {file.hasEmbedding && (
                    <div title="Embedded in vector database">
                      <Database 
                        size={12} 
                        className="text-green-500 mr-2 flex-shrink-0" 
                      />
                    </div>
                  )}
                </div>
                
                <button
                  onClick={() => handleDeleteFile(file.id)}
                  className="p-1 text-red-500 hover:text-red-700 hover:bg-red-50 rounded transition-colors"
                  title="Delete file"
                >
                  <Trash2 size={12} />
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Embedding Search */}
      {files.some(f => f.hasEmbedding) && (
        <div>
          <h4 className="text-sm font-medium text-gray-700 mb-2">
            Search Files
          </h4>
          
          <div className="space-y-2">
            <div className="flex gap-2">
              <input
                type="text"
                placeholder="Search file contents..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                onKeyPress={(e) => e.key === 'Enter' && handleSearchEmbeddings()}
                className="flex-1 px-3 py-2 text-sm border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              />
              <button
                onClick={handleSearchEmbeddings}
                disabled={searching || !searchQuery.trim()}
                className="px-3 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 disabled:opacity-50 disabled:cursor-not-allowed flex items-center"
              >
                {searching ? (
                  <Loader2 size={14} className="animate-spin" />
                ) : (
                  <Search size={14} />
                )}
              </button>
            </div>
            
            {searchResults.length > 0 && (
              <div className="max-h-32 overflow-y-auto space-y-1">
                {searchResults.map((result, index) => (
                  <div
                    key={index}
                    className="p-2 bg-blue-50 rounded text-xs"
                  >
                    <p className="font-medium text-blue-800">{result.filename}</p>
                    <p className="text-blue-600 mt-1">{result.excerpt}</p>
                    <p className="text-blue-500 mt-1">
                      Similarity: {(result.similarity * 100).toFixed(1)}%
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default FileManager;