import React, { useState, useEffect } from 'react';
import useEffectiveUser from '../hooks/useEffectiveUser';
import { motion } from 'framer-motion';
import { Database, RefreshCw, AlertCircle, CheckCircle, Wrench, Info } from 'lucide-react';
import { toast } from 'react-toastify';

interface SyncStatus {
  userId: string;
  sqliteCount: number;
  neo4jCount: number;
  inSync: boolean;
  missingInNeo4j: {
    count: number;
    memoryIds: string[];
    details: Array<{
      id: string;
      content: string;
      category: string;
      createdAt: string;
      preview: string;
    }>;
  };
  missingInSqlite: {
    count: number;
    memoryIds: string[];
  };
  timestamp: string;
}

interface RepairResult {
  userId: string;
  totalMissingMemories: number;
  successfulRepairs: number;
  failedRepairs: number;
  repairDetails: Array<{
    memoryId: string;
    status: string;
    error?: string;
    preview: string;
  }>;
  timestamp: string;
}

interface FullSyncResult {
  userId: string;
  initialStatus: SyncStatus;
  finalStatus: SyncStatus;
  repairResult: RepairResult | null;
  summary: {
    wasInSync: boolean;
    isNowInSync: boolean;
    improvementMade: boolean;
    totalInitialIssues: number;
    totalRemainingIssues: number;
    issuesResolved: number;
  };
  timestamp: string;
}

interface MemorySyncStatusProps {
  userId: string;
}

const MemorySyncStatus: React.FC<MemorySyncStatusProps> = ({ userId }) => {
  const eff = useEffectiveUser();
  const [syncStatus, setSyncStatus] = useState<SyncStatus | null>(null);
  const [loading, setLoading] = useState(false);
  const [fullSyncing, setFullSyncing] = useState(false);
  const [repairResult, setRepairResult] = useState<RepairResult | null>(null);
  const [fullSyncResult, setFullSyncResult] = useState<FullSyncResult | null>(null);
  const [showDetails, setShowDetails] = useState(false);

  const loadSyncStatus = async () => {
    try {
      setLoading(true);
      const response = await fetch(`/api/memorysyncstatus/status/${userId}`, { headers: eff.headers });
      if (response.ok) {
        const data = await response.json();
        setSyncStatus(data);
      } else {
        toast.error(`Failed to load sync status: ${response.statusText}`, { toastId: 'memory-sync-status-load' });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Error loading sync status: ${message}`, { toastId: 'memory-sync-status-load' });
    } finally {
      setLoading(false);
    }
  };
  const repairMemories = async () => {
    try {
      setFullSyncing(true);
      setRepairResult(null);
      setFullSyncResult(null);
      const response = await fetch(`/api/memorysyncstatus/repair/${userId}`, { method: 'POST', headers: eff.headers });
      if (response.ok) {
        const data = await response.json();
        setRepairResult(data);
        // Refresh sync status after repair
        await loadSyncStatus();
      } else {
        toast.error(`Failed to repair memories: ${response.statusText}`, { toastId: 'memory-sync-repair' });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Error repairing memories: ${message}`, { toastId: 'memory-sync-repair' });
    } finally {
      setFullSyncing(false);
    }
  };

  const performFullSync = async () => {
    try {
      setFullSyncing(true);
      setRepairResult(null);
      setFullSyncResult(null);
      const response = await fetch(`/api/memorysyncstatus/full/${userId}`, { method: 'POST', headers: eff.headers });
      if (response.ok) {
        const data = await response.json();
        setFullSyncResult(data);
        setSyncStatus(data.finalStatus);
      } else {
        toast.error(`Failed to perform full sync: ${response.statusText}`, { toastId: 'memory-sync-full' });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      toast.error(`Error performing full sync: ${message}`, { toastId: 'memory-sync-full' });
    } finally {
      setFullSyncing(false);
    }
  };

  useEffect(() => {
    loadSyncStatus();
  }, [userId, eff.headers]);

  const getSyncStatusColor = () => {
    if (!syncStatus) return 'text-gray-500';
    if (syncStatus.inSync) return 'text-green-500';
    if (syncStatus.missingInNeo4j.count > 0) return 'text-red-500';
    return 'text-yellow-500';
  };

  const getSyncStatusIcon = () => {
    if (!syncStatus) return <Database className="w-5 h-5" />;
    if (syncStatus.inSync) return <CheckCircle className="w-5 h-5 text-green-500" />;
    return <AlertCircle className="w-5 h-5 text-red-500" />;
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="bg-white rounded-lg shadow-md p-6 mb-6"
    >
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center space-x-3">
          {getSyncStatusIcon()}
          <h3 className="text-lg font-semibold text-gray-800">Memory Sync Status</h3>
          <span className={`text-sm font-medium ${getSyncStatusColor()}`}>
            {syncStatus?.inSync ? 'In Sync' : 'Out of Sync'}
          </span>
        </div>
        <div className="flex items-center space-x-2">
          <button
            onClick={loadSyncStatus}
            disabled={loading}
            className="flex items-center space-x-1 px-3 py-1 text-sm bg-blue-50 text-blue-600 rounded-md hover:bg-blue-100 disabled:opacity-50"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            <span>Refresh</span>
          </button>
          <button
            onClick={() => setShowDetails(!showDetails)}
            className="flex items-center space-x-1 px-3 py-1 text-sm bg-gray-50 text-gray-600 rounded-md hover:bg-gray-100"
          >
            <Info className="w-4 h-4" />
            <span>{showDetails ? 'Hide' : 'Show'} Details</span>
          </button>
        </div>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-8">
          <RefreshCw className="w-6 h-6 animate-spin text-blue-500" />
          <span className="ml-2 text-gray-600">Loading sync status...</span>
        </div>
      ) : syncStatus ? (
        <div className="space-y-4">
          {/* Summary Stats */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="bg-blue-50 p-4 rounded-lg">
              <div className="text-2xl font-bold text-blue-600">{syncStatus.sqliteCount}</div>
              <div className="text-sm text-blue-800">SQLite Memories</div>
            </div>
            <div className="bg-green-50 p-4 rounded-lg">
              <div className="text-2xl font-bold text-green-600">{syncStatus.neo4jCount}</div>
              <div className="text-sm text-green-800">Neo4j Memories</div>
            </div>
            <div className="bg-red-50 p-4 rounded-lg">
              <div className="text-2xl font-bold text-red-600">{syncStatus.missingInNeo4j.count}</div>
              <div className="text-sm text-red-800">Missing in Neo4j</div>
            </div>
          </div>          {/* Sync Issues */}
          {!syncStatus.inSync && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4">
              <div className="flex items-center justify-between mb-3">
                <h4 className="font-medium text-red-800">Synchronization Issues Detected</h4>
                <div className="flex space-x-2">
                  <button
                    onClick={repairMemories}
                    disabled={fullSyncing || syncStatus.missingInNeo4j.count === 0}
                    className="flex items-center space-x-1 px-3 py-1 bg-orange-600 text-white rounded-md hover:bg-orange-700 disabled:opacity-50"
                  >
                    <Wrench className={`w-4 h-4 ${fullSyncing ? 'animate-spin' : ''}`} />
                    <span>{fullSyncing ? 'Repairing...' : 'Quick Repair'}</span>
                  </button>
                  <button
                    onClick={performFullSync}
                    disabled={fullSyncing}
                    className="flex items-center space-x-1 px-4 py-1 bg-red-600 text-white rounded-md hover:bg-red-700 disabled:opacity-50 font-medium"
                  >
                    <RefreshCw className={`w-4 h-4 ${fullSyncing ? 'animate-spin' : ''}`} />
                    <span>{fullSyncing ? 'Syncing...' : 'Fix All'}</span>
                  </button>
                </div>
              </div>
              <p className="text-red-700">
                {syncStatus.missingInNeo4j.count} memories are stored in SQLite but missing from Neo4j.
                This affects graph-based memory retrieval and relationship detection.
              </p>
            </div>
          )}          {/* Repair Result */}
          {repairResult && (
            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
              <h4 className="font-medium text-green-800 mb-2">Quick Repair Completed</h4>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <span className="text-green-700">Successful Repairs: </span>
                  <span className="font-medium">{repairResult.successfulRepairs}</span>
                </div>
                <div>
                  <span className="text-green-700">Failed Repairs: </span>
                  <span className="font-medium">{repairResult.failedRepairs}</span>
                </div>
              </div>
            </div>
          )}

          {/* Full Sync Result */}
          {fullSyncResult && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <h4 className="font-medium text-blue-800 mb-3">Full Sync Completed</h4>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Summary */}
                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-blue-700">Status:</span>
                    <span className={`font-medium ${fullSyncResult.summary.isNowInSync ? 'text-green-600' : 'text-red-600'}`}>
                      {fullSyncResult.summary.isNowInSync ? 'Now In Sync' : 'Issues Remain'}
                    </span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-blue-700">Initial Issues:</span>
                    <span className="font-medium">{fullSyncResult.summary.totalInitialIssues}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-blue-700">Issues Resolved:</span>
                    <span className="font-medium text-green-600">{fullSyncResult.summary.issuesResolved}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-blue-700">Remaining Issues:</span>
                    <span className="font-medium">{fullSyncResult.summary.totalRemainingIssues}</span>
                  </div>
                </div>
                
                {/* Repair Details (if any repairs were performed) */}
                {fullSyncResult.repairResult && (
                  <div className="space-y-2">
                    <h5 className="text-sm font-medium text-blue-800">Repair Details</h5>
                    <div className="flex justify-between text-sm">
                      <span className="text-blue-700">Successful:</span>
                      <span className="font-medium text-green-600">{fullSyncResult.repairResult.successfulRepairs}</span>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span className="text-blue-700">Failed:</span>
                      <span className="font-medium text-red-600">{fullSyncResult.repairResult.failedRepairs}</span>
                    </div>
                  </div>
                )}
              </div>
              
              {/* Success/Warning Message */}
              <div className={`mt-3 p-2 rounded text-sm ${
                fullSyncResult.summary.isNowInSync 
                  ? 'bg-green-100 text-green-700' 
                  : 'bg-yellow-100 text-yellow-700'
              }`}>
                {fullSyncResult.summary.isNowInSync 
                  ? '✅ All memories are now synchronized between SQLite and Neo4j!' 
                  : '⚠️ Some synchronization issues could not be automatically resolved. Manual intervention may be required.'}
              </div>
            </div>
          )}

          {/* Detailed Info */}
          {showDetails && syncStatus.missingInNeo4j.count > 0 && (
            <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
              <h4 className="font-medium text-gray-800 mb-3">Missing Memories (Sample)</h4>
              <div className="space-y-2 max-h-60 overflow-y-auto">
                {syncStatus.missingInNeo4j.details.slice(0, 10).map((memory) => (
                  <div key={memory.id} className="bg-white p-3 rounded border">
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-xs font-mono text-gray-500">{memory.id}</span>
                      <span className="text-xs text-gray-600">{memory.category}</span>
                    </div>
                    <p className="text-sm text-gray-700">{memory.preview}</p>
                    <div className="text-xs text-gray-500 mt-1">
                      Created: {new Date(memory.createdAt).toLocaleString()}
                    </div>
                  </div>
                ))}
                {syncStatus.missingInNeo4j.details.length > 10 && (
                  <div className="text-center text-sm text-gray-500 py-2">
                    ... and {syncStatus.missingInNeo4j.details.length - 10} more
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Last Updated */}
          <div className="text-xs text-gray-500 text-center">
            Last updated: {new Date(syncStatus.timestamp).toLocaleString()}
          </div>
        </div>
      ) : (
        <div className="text-center py-8 text-gray-500">
          Failed to load sync status. Please try refreshing.
        </div>
      )}
    </motion.div>
  );
};

export default MemorySyncStatus;
