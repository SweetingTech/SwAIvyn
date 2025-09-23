import { Suspense, useEffect } from 'react';
import { Route, Routes, Navigate, useNavigate } from 'react-router-dom';
import Layout from './components/layout/Layout';
import LoadingSpinner from './components/ui/LoadingSpinner';
import SplashScreen from './components/SplashScreen';
import ErrorBoundary from './components/ErrorBoundary';
import { runtimeConfig } from './config';
import DashboardPage from './pages/DashboardPage';
import ChatPage from './pages/ChatPage';
import VoiceRoomPage from './pages/VoiceRoomPage';
import MemoryPage from './pages/MemoryPage';
import ModulesPage from './pages/ModulesPage';
import ModuleStorePage from './pages/ModuleStorePage';
import AgentsTab from './components/AgentsTab'; // Changed this line
import UserProfilePage from './pages/UserProfilePage';
import SettingsPage from './pages/SettingsPage';
import MemoryBrowser from './pages/MemoryBrowser';
import ConversationManagement from './pages/ConversationManagement';
import KnowledgeUploadPage from './pages/KnowledgeUploadPage';
import CharacterEditor from './pages/CharacterEditor';
import { InitializationProvider, useInitialization } from './contexts/InitializationContext';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import LoginPage from './pages/LoginPage';
// Optional Stagewise toolbar (disabled by default). Enable with VITE_STAGEWISE_ENABLED=true
const enableStagewise = runtimeConfig.stagewiseEnabled;
type StagewiseToolbarModule = typeof import('@stagewise/toolbar-react');
type StagewisePluginModule = typeof import('@stagewise-plugins/react');

let StagewiseToolbar: StagewiseToolbarModule['StagewiseToolbar'] | null = null;
let ReactPlugin: StagewisePluginModule['ReactPlugin'] | null = null;
if (enableStagewise) {
  const stagewise = require('@stagewise/toolbar-react') as StagewiseToolbarModule;
  const stagewisePlugin = require('@stagewise-plugins/react') as StagewisePluginModule;
  StagewiseToolbar = stagewise.StagewiseToolbar;
  ReactPlugin = stagewisePlugin.ReactPlugin;
}
import AdminUsersPage from './pages/AdminUsersPage';

// Initialize i18n
import './i18n';

const AppErrorFallback = ({ error, onRetry }: { error: Error | null; onRetry: () => void }) => (
  <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50 p-6 text-center text-gray-800">
    <h1 className="text-2xl font-semibold">Something went wrong</h1>
    {error?.message && <p className="max-w-md text-sm text-gray-600">{error.message}</p>}
    <div className="flex flex-wrap items-center justify-center gap-3">
      <button
        type="button"
        onClick={onRetry}
        className="rounded bg-primary-500 px-4 py-2 text-white shadow hover:bg-primary-600"
      >
        Try again
      </button>
      <button
        type="button"
        onClick={() => window.location.reload()}
        className="rounded border border-primary-500 px-4 py-2 text-primary-600 hover:bg-primary-50"
      >
        Reload app
      </button>
    </div>
  </div>
);

function AppContent() {
  const { isInitialized, isLoading, currentStep, error, initialize, user } = useInitialization();
  const { token } = useAuth();
  const navigate = useNavigate();

  // Force navigate to /login when initialized and unauthenticated
  useEffect(() => {
    if (isInitialized && !token) {
      navigate('/login', { replace: true });
    }
  }, [isInitialized, token, navigate]);

  let content;

  if (!isInitialized) {
    content = (
      <SplashScreen
        isLoading={isLoading}
        currentStep={currentStep}
        error={error}
        onRetry={initialize}
      />
    );
  } else if (!token) {
    content = (
      <Suspense fallback={<LoadingSpinner />}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </Suspense>
    );
  } else {
    content = (
      <Suspense fallback={<LoadingSpinner />}>
        <Routes>
          <Route path="/" element={<Layout />}>
            <Route index element={<DashboardPage />} />
            <Route path="dashboard" element={<DashboardPage />} />
            <Route path="chat/:sessionCharacter?" element={<ChatPage />} />
            <Route path="voice-room" element={<VoiceRoomPage />} />
            <Route path="memory" element={<MemoryPage />} />
            <Route path="modules" element={<ModulesPage />} />
            <Route path="module-store" element={<ModuleStorePage />} />
            <Route path="agents" element={<AgentsTab />} /> {/* Changed this line */}
            <Route path="profile" element={<UserProfilePage />} />
            <Route path="settings" element={<SettingsPage />} />
            <Route path="knowledge-upload" element={<KnowledgeUploadPage />} />
            <Route path="character-editor" element={<CharacterEditor userId={user?.id || "demo-user-id"} onSave={() => navigate('/dashboard')} onCancel={() => navigate('/dashboard')} />} />
            <Route path="admin/users" element={<AdminUsersPage />} />
            <Route path="memory-browser" element={<MemoryBrowser userId={user?.id || "demo-user-id"} />} />
            <Route path="conversation-management" element={<ConversationManagement userId={user?.id || "demo-user-id"} onSelectConversation={() => {}} />} />
          </Route>
        </Routes>
      </Suspense>
    );
  }

  return (
    <ErrorBoundary
      onReset={initialize}
      fallback={({ error: boundaryError, resetErrorBoundary }) => (
        <AppErrorFallback
          error={boundaryError}
          onRetry={() => {
            resetErrorBoundary();
          }}
        />
      )}
    >
      {content}
    </ErrorBoundary>
  );
}

function App() {
  return (
    <AuthProvider>
      <InitializationProvider>
        <AppContent />
        {enableStagewise && StagewiseToolbar && ReactPlugin && (
          <StagewiseToolbar config={{ plugins: [ReactPlugin] }} />
        )}
      </InitializationProvider>
    </AuthProvider>
  );
}

export default App;
