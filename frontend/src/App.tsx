import { Suspense } from 'react';
import { Route, Routes } from 'react-router-dom';
import Layout from './components/layout/Layout';
import LoadingSpinner from './components/ui/LoadingSpinner';
import SplashScreen from './components/SplashScreen';
import DashboardPage from './pages/DashboardPage';
import ChatPage from './pages/ChatPage';
import VoiceRoomPage from './pages/VoiceRoomPage';
import MemoryPage from './pages/MemoryPage';
import ModulesPage from './pages/ModulesPage';
import AgentsTab from './components/AgentsTab'; // Changed this line
import UserProfilePage from './pages/UserProfilePage';
import SettingsPage from './pages/SettingsPage';
import MemoryBrowser from './pages/MemoryBrowser';
import ConversationManagement from './pages/ConversationManagement';
import KnowledgeUploadPage from './pages/KnowledgeUploadPage';
import { InitializationProvider, useInitialization } from './contexts/InitializationContext';

// Initialize i18n
import './i18n';

function AppContent() {
  const { isInitialized, isLoading, currentStep, error, initialize, user } = useInitialization();

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

  // Show main app once initialized
  return (
    <Suspense fallback={<LoadingSpinner />}>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<DashboardPage />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="chat/:sessionCharacter?" element={<ChatPage />} />
          <Route path="voice-room" element={<VoiceRoomPage />} />
          <Route path="memory" element={<MemoryPage />} />
          <Route path="modules" element={<ModulesPage />} />
          <Route path="agents" element={<AgentsTab />} /> {/* Changed this line */}
          <Route path="profile" element={<UserProfilePage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="knowledge-upload" element={<KnowledgeUploadPage />} />
          <Route path="memory-browser" element={<MemoryBrowser userId={user?.id || "demo-user-id"} />} />
          <Route path="conversation-management" element={<ConversationManagement userId={user?.id || "demo-user-id"} onSelectConversation={() => {}} />} />
        </Route>
      </Routes>
    </Suspense>
  );
}

function App() {
  return (
    <InitializationProvider>
      <AppContent />
    </InitializationProvider>
  );
}

export default App;