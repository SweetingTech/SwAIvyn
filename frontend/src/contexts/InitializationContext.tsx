import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import fetchWithRetry from '../utils/fetchWithRetry';

interface User {
  id: string;
  username: string;
  email: string;
}

interface InitializationState {
  isInitialized: boolean;
  isLoading: boolean;
  currentStep: string;
  error: string | null;
  user: User | null;
}

interface InitializationContextType extends InitializationState {
  initialize: () => Promise<void>;
}

const InitializationContext = createContext<InitializationContextType | undefined>(undefined);

export const useInitialization = () => {
  const context = useContext(InitializationContext);
  if (context === undefined) {
    throw new Error('useInitialization must be used within an InitializationProvider');
  }
  return context;
};

interface InitializationProviderProps {
  children: ReactNode;
}

export const InitializationProvider: React.FC<InitializationProviderProps> = ({ children }) => {
  const [state, setState] = useState<InitializationState>({
    isInitialized: false,
    isLoading: false,
    currentStep: '',
    error: null,
    user: null,
  });
  const [attemptText, setAttemptText] = useState<string>('');

  const updateState = (updates: Partial<InitializationState>) => {
    setState(prev => ({ ...prev, ...updates }));
  };

  const initialize = async () => {
    if (state.isInitialized || state.isLoading) {
      return;
    }

    updateState({ isLoading: true, error: null });

    try {
      // Step 1: Backend warmup and default user
      updateState({ currentStep: 'Starting backend…' });
      // Quick poll of /healthz to avoid "first call" timeouts during startup races
      const ping = async (retries = 3, backoff = 2000): Promise<void> => {
        try {
          const r = await fetchWithRetry('/healthz', {}, 1, 0, 8000);
          if (!r.ok) throw new Error('not ready');
        } catch {
          if (retries <= 0) return;
          await new Promise(r => setTimeout(r, backoff));
          return ping(retries - 1, Math.min(backoff * 2, 8000));
        }
      };
      await ping();

      updateState({ currentStep: 'Loading user profile...' });
      const userResponse = await fetchWithRetry('/api/user/default', {}, 20, 400, 10000, (a,t) => setAttemptText(`(attempt ${a}/${t})`));
      if (!userResponse.ok) {
        throw new Error('Failed to load user profile');
      }
      const userData = await userResponse.json();
      const user: User = {
        id: userData.id,
        username: userData.username || 'User',
        email: userData.email || 'user@example.com'
      };
      updateState({ user });

      // Step 2 & 3: Load settings and characters concurrently
      updateState({ currentStep: 'Loading settings and characters...' });
      const [settingsResponse, charactersResponse] = await Promise.all([
        fetchWithRetry(`/api/settings/llm?userId=${user.id}`, {}, 20, 400, 10000, (a,t) => setAttemptText(`(settings ${a}/${t})`)),
        fetchWithRetry(`/api/character/user/${user.id}`, {}, 20, 400, 10000, (a,t) => setAttemptText(`(characters ${a}/${t})`))
      ]);

      if (settingsResponse.ok) {
        const settings = await settingsResponse.json();
        console.log('✅ Settings loaded:', settings);
      }

      if (charactersResponse.ok) {
        const characters = await charactersResponse.json();
        console.log('✅ Characters loaded:', characters.length, 'characters');
      }

      // Step 4: Complete initialization
      updateState({
        currentStep: 'Initialization complete!',
        isInitialized: true,
        isLoading: false
      });
      setAttemptText('');

      console.log('🎉 Application initialization completed successfully');

    } catch (error) {
      console.error('❌ Initialization failed:', error);
      updateState({
        error: error instanceof Error ? error.message : 'Initialization failed',
        isLoading: false
      });
      setAttemptText('');
    }
  };

  useEffect(() => {
    initialize();
  }, []);

  const value: InitializationContextType = {
    ...state,
    initialize,
  };

  return (
    <InitializationContext.Provider value={value}>
      {/* Lightweight status for startup */}
      {!state.isInitialized && state.isLoading && (
        <div style={{position:'fixed',bottom:12,right:12,background:'#1f2937',color:'#fff',padding:'8px 12px',borderRadius:8,fontSize:12,opacity:0.9,zIndex:9999}}>
          {state.currentStep} {attemptText}
        </div>
      )}
      {children}
    </InitializationContext.Provider>
  );
};
