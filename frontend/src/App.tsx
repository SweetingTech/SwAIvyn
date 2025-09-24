import { Suspense, useEffect } from 'react';
import { Route, Routes, Navigate, useNavigate } from 'react-router-dom';
import Layout from './components/layout/Layout';
import ErrorBoundary from './components/ErrorBoundary';
import LoadingSpinner from './components/ui/LoadingSpinner';
import SplashScreen from './components/SplashScreen';
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
import { env } from './config/env';
// Optional Stagewise toolbar (disabled by default). Enable with VITE_STAGEWISE_ENABLED=true
const enableStagewise = env.stagewiseEnabled;
let StagewiseToolbar: any = null as any;
let ReactPlugin: any = null as any;
if (enableStagewise) {
  // Lazy require to avoid loading when disabled
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  StagewiseToolbar = require('@stagewise/toolbar-react').StagewiseToolbar;
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  ReactPlugin = require('@stagewise-plugins/react').ReactPlugin;
}
import AdminUsersPage from './pages/AdminUsersPage';

// Initialize i18n
import './i18n';

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

  // Show splash screen during initialization
  if (!isInitialized) {
    return (
      <SplashScreen
        isLoading={isLoading}
        currentStep={currentStep}
        error={error}
        onRetry={initialize}
      />
    );
  }

  // Show main app once initialized and authenticated
  if (!token) {
    return (
      <ErrorBoundary>
        <Suspense fallback={<LoadingSpinner />}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="*" element={<Navigate to="/login" replace />} />
          </Routes>
        </Suspense>
      </ErrorBoundary>
    );
  }

  return (
    <ErrorBoundary>
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
          {/* Redirect any stray /login navigations for authenticated users */}
          <Route path="/login" element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </Suspense>
    </ErrorBoundary>
  );
}

function App() {
  return (
    <AuthProvider>
      <InitializationProvider>
        <ErrorBoundary>
          <>
            <AppContent />
            {enableStagewise && StagewiseToolbar && ReactPlugin && (
              <StagewiseToolbar config={{ plugins: [ReactPlugin] }} />
            )}
          </>
        </ErrorBoundary>
      </InitializationProvider>
    </AuthProvider>
  );
}

export default App;
