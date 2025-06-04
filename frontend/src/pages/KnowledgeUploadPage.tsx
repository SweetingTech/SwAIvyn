import React, { useState, useEffect, useCallback } from 'react';
import { motion } from 'framer-motion';
import {
  Upload,
  File,
  CheckCircle,
  XCircle,
  Clock,
  Trash2,
  RefreshCw,
  Search,
  Filter,
  Download,
  AlertCircle,
  FileText,
  Image,
  FileImage
} from 'lucide-react';

interface UploadDocument {
  id: string;
  fileName: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Cancelled';
  uploadedAt: string;
  processedAt?: string;
  category?: string;
  description?: string;
  fileSizeBytes: number;
  chunkCount: number;
  errorMessage?: string;
}

interface UploadProgress {
  fileName: string;
  progress: number;
  status: 'uploading' | 'processing' | 'completed' | 'error';
  error?: string;
}

const KnowledgeUploadPage: React.FC = () => {
  const [documents, setDocuments] = useState<UploadDocument[]>([]);
  const [uploadProgress, setUploadProgress] = useState<UploadProgress[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');

  // Load documents on component mount
  useEffect(() => {
    loadDocuments();
  }, []);

  const loadDocuments = async () => {
    try {
      setIsLoading(true);
      const response = await fetch('/api/upload/documents');
      if (response.ok) {
        const docs = await response.json();
        setDocuments(docs);
      }
    } catch (error) {
      console.error('Failed to load documents:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleFileUpload = async (files: FileList) => {
    const formData = new FormData();
    
    // Add all files to form data
    Array.from(files).forEach(file => {
      formData.append('files', file);
    });

    // Initialize progress tracking
    const initialProgress = Array.from(files).map(file => ({
      fileName: file.name,
      progress: 0,
      status: 'uploading' as const
    }));
    setUploadProgress(initialProgress);

    try {
      const response = await fetch('/api/upload/files', {
        method: 'POST',
        body: formData
      });

      if (response.ok) {
        const result = await response.json();
        
        // Update progress to completed
        setUploadProgress(prev => prev.map(p => ({
          ...p,
          progress: 100,
          status: 'completed'
        })));

        // Reload documents list
        await loadDocuments();

        // Clear progress after a delay
        setTimeout(() => setUploadProgress([]), 3000);
      } else {
        const error = await response.text();
        setUploadProgress(prev => prev.map(p => ({
          ...p,
          status: 'error',
          error: error
        })));
      }
    } catch (error) {
      setUploadProgress(prev => prev.map(p => ({
        ...p,
        status: 'error',
        error: 'Upload failed'
      })));
    }
  };

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
    
    const files = e.dataTransfer.files;
    if (files.length > 0) {
      handleFileUpload(files);
    }
  }, []);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
  }, []);

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      handleFileUpload(files);
    }
    // Reset input
    e.target.value = '';
  };

  const deleteDocument = async (id: string) => {
    try {
      const response = await fetch(`/api/upload/documents/${id}`, {
        method: 'DELETE'
      });
      
      if (response.ok) {
        await loadDocuments();
      }
    } catch (error) {
      console.error('Failed to delete document:', error);
    }
  };

  const reprocessDocument = async (id: string) => {
    try {
      const response = await fetch(`/api/upload/documents/${id}/reprocess`, {
        method: 'POST'
      });
      
      if (response.ok) {
        await loadDocuments();
      }
    } catch (error) {
      console.error('Failed to reprocess document:', error);
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'Completed':
        return <div className="w-3 h-3 bg-green-500 rounded-full" title="Vectorized - Ready for search" />;
      case 'Failed':
        return <div className="w-3 h-3 bg-red-500 rounded-full" title="Failed to process" />;
      case 'Processing':
        return <div className="w-3 h-3 bg-yellow-500 rounded-full animate-pulse" title="Processing..." />;
      default:
        return <div className="w-3 h-3 bg-red-500 rounded-full" title="Not processed" />;
    }
  };

  const getFileIcon = (fileName: string) => {
    const extension = fileName.toLowerCase().split('.').pop();

    switch (extension) {
      case 'pdf':
        return <FileText className="w-5 h-5 text-red-600" />;
      case 'jpg':
      case 'jpeg':
      case 'png':
      case 'gif':
      case 'bmp':
      case 'tiff':
      case 'webp':
        return <FileImage className="w-5 h-5 text-blue-600" />;
      case 'txt':
      case 'md':
        return <FileText className="w-5 h-5 text-gray-600" />;
      case 'json':
      case 'xml':
      case 'html':
        return <FileText className="w-5 h-5 text-green-600" />;
      default:
        return <File className="w-5 h-5 text-gray-500" />;
    }
  };

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const filteredDocuments = documents.filter(doc => {
    const matchesSearch = doc.fileName.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         (doc.description || '').toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCategory = !categoryFilter || doc.category === categoryFilter;
    const matchesStatus = !statusFilter || doc.status === statusFilter;
    
    return matchesSearch && matchesCategory && matchesStatus;
  });

  const categories = [...new Set(documents.map(d => d.category).filter(Boolean))];
  const statuses = [...new Set(documents.map(d => d.status))];

  return (
    <div className="min-h-screen bg-gray-50 p-6">
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">Knowledge Base Upload</h1>
          <p className="text-gray-600">
            Upload documents to expand the AI's general knowledge. Supported formats: TXT, MD, CSV, JSON, XML, HTML, RTF, PDF, Images (JPG, PNG, GIF, etc.)
          </p>
        </div>

        {/* Upload Area */}
        <motion.div
          className={`border-2 border-dashed rounded-lg p-8 mb-8 text-center transition-colors ${
            isDragOver 
              ? 'border-blue-500 bg-blue-50' 
              : 'border-gray-300 bg-white hover:border-gray-400'
          }`}
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          whileHover={{ scale: 1.01 }}
          whileTap={{ scale: 0.99 }}
        >
          <Upload className="w-12 h-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-900 mb-2">
            Drop files here or click to upload
          </h3>
          <p className="text-gray-500 mb-4">
            Upload multiple files at once. Maximum 20 files, 200MB total.
            <br />
            <span className="text-sm">Supported: TXT, MD, CSV, JSON, XML, HTML, RTF, PDF, Images (JPG, PNG, GIF, etc.)</span>
          </p>
          <input
            type="file"
            multiple
            accept=".txt,.md,.csv,.json,.xml,.html,.htm,.rtf,.pdf,.jpg,.jpeg,.png,.gif,.bmp,.tiff,.tif,.webp"
            onChange={handleFileSelect}
            className="hidden"
            id="file-upload"
          />
          <label
            htmlFor="file-upload"
            className="inline-flex items-center px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 cursor-pointer"
          >
            <Upload className="w-4 h-4 mr-2" />
            Select Files
          </label>
        </motion.div>

        {/* Upload Progress */}
        {uploadProgress.length > 0 && (
          <div className="bg-white rounded-lg shadow p-6 mb-8">
            <h3 className="text-lg font-medium mb-4">Upload Progress</h3>
            {uploadProgress.map((progress, index) => (
              <div key={index} className="mb-4 last:mb-0">
                <div className="flex items-center justify-between mb-2">
                  <span className="text-sm font-medium">{progress.fileName}</span>
                  <span className="text-sm text-gray-500">
                    {progress.status === 'completed' ? 'Completed' : 
                     progress.status === 'error' ? 'Error' : 
                     progress.status === 'processing' ? 'Processing...' : 
                     `${progress.progress}%`}
                  </span>
                </div>
                <div className="w-full bg-gray-200 rounded-full h-2">
                  <div
                    className={`h-2 rounded-full transition-all duration-300 ${
                      progress.status === 'completed' ? 'bg-green-500' :
                      progress.status === 'error' ? 'bg-red-500' :
                      'bg-blue-500'
                    }`}
                    style={{ width: `${progress.progress}%` }}
                  />
                </div>
                {progress.error && (
                  <p className="text-red-500 text-sm mt-1">{progress.error}</p>
                )}
              </div>
            ))}
          </div>
        )}

        {/* Filters */}
        <div className="bg-white rounded-lg shadow p-6 mb-8">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Search
              </label>
              <div className="relative">
                <Search className="w-4 h-4 absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                <input
                  type="text"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  placeholder="Search documents..."
                  className="pl-10 w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                />
              </div>
            </div>
            
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Category
              </label>
              <select
                value={categoryFilter}
                onChange={(e) => setCategoryFilter(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              >
                <option value="">All Categories</option>
                {categories.map(category => (
                  <option key={category} value={category}>{category}</option>
                ))}
              </select>
            </div>
            
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Status
              </label>
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              >
                <option value="">All Statuses</option>
                {statuses.map(status => (
                  <option key={status} value={status}>{status}</option>
                ))}
              </select>
            </div>
            
            <div className="flex items-end">
              <button
                onClick={loadDocuments}
                disabled={isLoading}
                className="w-full px-4 py-2 bg-gray-600 text-white rounded-lg hover:bg-gray-700 disabled:opacity-50 flex items-center justify-center"
              >
                <RefreshCw className={`w-4 h-4 mr-2 ${isLoading ? 'animate-spin' : ''}`} />
                Refresh
              </button>
            </div>
          </div>
        </div>

        {/* Documents List */}
        <div className="bg-white rounded-lg shadow">
          <div className="px-6 py-4 border-b border-gray-200">
            <h3 className="text-lg font-medium">Uploaded Documents ({filteredDocuments.length})</h3>
          </div>
          
          {filteredDocuments.length === 0 ? (
            <div className="p-8 text-center text-gray-500">
              {documents.length === 0 ? (
                <>
                  <File className="w-12 h-12 mx-auto mb-4 text-gray-300" />
                  <p>No documents uploaded yet. Start by uploading some files above.</p>
                </>
              ) : (
                <p>No documents match your current filters.</p>
              )}
            </div>
          ) : (
            <div className="divide-y divide-gray-200">
              {filteredDocuments.map((doc) => (
                <div key={doc.id} className="p-6 hover:bg-gray-50">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-4">
                      <div className="flex items-center space-x-2">
                        {getFileIcon(doc.fileName)}
                        {getStatusIcon(doc.status)}
                      </div>
                      <div>
                        <h4 className="text-sm font-medium text-gray-900">{doc.fileName}</h4>
                        <div className="text-sm text-gray-500 space-x-4">
                          <span>{formatFileSize(doc.fileSizeBytes)}</span>
                          <span>•</span>
                          <span>{doc.chunkCount} chunks</span>
                          {doc.category && (
                            <>
                              <span>•</span>
                              <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded-full text-xs">
                                {doc.category}
                              </span>
                            </>
                          )}
                        </div>
                        {doc.description && (
                          <p className="text-sm text-gray-600 mt-1">{doc.description}</p>
                        )}
                        {doc.errorMessage && (
                          <div className="flex items-center mt-2 text-red-600">
                            <AlertCircle className="w-4 h-4 mr-1" />
                            <span className="text-sm">{doc.errorMessage}</span>
                          </div>
                        )}
                      </div>
                    </div>
                    
                    <div className="flex items-center space-x-2">
                      {doc.status === 'Failed' && (
                        <button
                          onClick={() => reprocessDocument(doc.id)}
                          className="p-2 text-blue-600 hover:bg-blue-50 rounded-lg"
                          title="Reprocess document"
                        >
                          <RefreshCw className="w-4 h-4" />
                        </button>
                      )}
                      <button
                        onClick={() => deleteDocument(doc.id)}
                        className="p-2 text-red-600 hover:bg-red-50 rounded-lg"
                        title="Delete document"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                  
                  <div className="mt-3 text-xs text-gray-500">
                    Uploaded: {new Date(doc.uploadedAt).toLocaleString()}
                    {doc.processedAt && (
                      <span className="ml-4">
                        Processed: {new Date(doc.processedAt).toLocaleString()}
                      </span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default KnowledgeUploadPage;
