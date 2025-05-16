import { Suspense } from 'react';
import { Route, Routes } from 'react-router-dom';
import Layout from './components/layout/Layout';
import LoadingSpinner from './components/ui/LoadingSpinner';
import ChatPage from './pages/ChatPage';
import VoiceRoomPage from './pages/VoiceRoomPage';
import MemoryPage from './pages/MemoryPage';
import ModulesPage from './pages/ModulesPage';
import SettingsPage from './pages/SettingsPage';

function App() {
  return (
    <Suspense fallback={<LoadingSpinner />}>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<ChatPage />} />
          <Route path="chat" element={<ChatPage />} />
          <Route path="voice-room" element={<VoiceRoomPage />} />
          <Route path="memory" element={<MemoryPage />} />
          <Route path="modules" element={<ModulesPage />} />
          <Route path="settings" element={<SettingsPage />} />
        </Route>
      </Routes>
    </Suspense>
  );
}

export default App;