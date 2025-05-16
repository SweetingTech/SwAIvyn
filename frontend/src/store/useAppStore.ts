import { create } from 'zustand';
import { Message } from '../types/chat';

interface AppState {
  // Chat state
  messages: Message[];
  addMessage: (message: Message) => void;
  
  // Voice room state
  isListening: boolean;
  setIsListening: (isListening: boolean) => void;
  
  // Settings
  settings: {
    aiName: string;
    wakeWord: string;
    wakeSensitivity: number;
    theme: 'light' | 'dark' | 'system';
    accentColor: string;
  };
  updateSettings: (settings: Partial<AppState['settings']>) => void;
}

const useAppStore = create<AppState>((set) => ({
  // Chat state
  messages: [
    {
      id: '1',
      sender: 'ai',
      text: 'Hello! How can I help you today?',
      timestamp: new Date().toISOString()
    }
  ],
  addMessage: (message) => set((state) => ({ 
    messages: [...state.messages, message] 
  })),
  
  // Voice room state
  isListening: false,
  setIsListening: (isListening) => set({ isListening }),
  
  // Settings
  settings: {
    aiName: 'Jazzy',
    wakeWord: 'Hey Jazzy',
    wakeSensitivity: 5,
    theme: 'light',
    accentColor: '#8B5CF6',
  },
  updateSettings: (newSettings) => set((state) => ({
    settings: { ...state.settings, ...newSettings }
  })),
}));

export default useAppStore;