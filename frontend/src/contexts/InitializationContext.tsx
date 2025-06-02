import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

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

  const updateState = (updates: Partial<InitializationState>) => {
    setState(prev => ({ ...prev, ...updates }));
  };

  const initialize = async () => {
    if (state.isInitialized || state.isLoading) {
      return;
    }

    updateState({ isLoading: true, error: null });

    try {
      // Step 1: Load default user
      updateState({ currentStep: 'Loading user profile...' });
      const userResponse = await fetch('/api/user/default');
      if (!userResponse.ok) {
        throw new Error('Failed to load user profile');
      }
      const userData = await userResponse.json();
      const user: User = {
        id: userData.id || '00000000-0000-0000-0000-000000000001',
        username: userData.username || 'User',
        email: userData.email || 'user@example.com'
      };
      updateState({ user });

      // Step 2: Load user settings
      updateState({ currentStep: 'Loading settings...' });
      const settingsResponse = await fetch(`/api/settings/llm?userId=${user.id}`);
      if (settingsResponse.ok) {
        const settings = await settingsResponse.json();
        console.log('✅ Settings loaded:', settings);
      }

      // Step 3: Load characters
      updateState({ currentStep: 'Loading characters...' });
      const charactersResponse = await fetch('/api/character/global');
      if (charactersResponse.ok) {
        const characters = await charactersResponse.json();
        console.log('✅ Global characters loaded:', characters.length, 'characters');
      }

      // Step 4: Complete initialization
      updateState({ 
        currentStep: 'Initialization complete!',
        isInitialized: true,
        isLoading: false 
      });

      console.log('🎉 Application initialization completed successfully');

    } catch (error) {
      console.error('❌ Initialization failed:', error);
      updateState({
        error: error instanceof Error ? error.message : 'Initialization failed',
        isLoading: false
      });
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
      {children}
    </InitializationContext.Provider>
  );
};
