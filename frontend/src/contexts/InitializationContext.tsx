import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useRef,
  ReactNode,
} from 'react';
import fetchWithRetry from '../utils/fetchWithRetry';
import { useAuth } from './AuthContext';

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

// eslint-disable-next-line react-refresh/only-export-components
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
  const { logout } = useAuth();
  const [state, setState] = useState<InitializationState>({
    isInitialized: false,
    isLoading: false,
    currentStep: '',
    error: null,
    user: null,
  });
  const [attemptText, setAttemptText] = useState<string>('');

  // Guards
  const mountedRef = useRef(true);
  const bootingRef = useRef(false); // prevent concurrent initialize calls

  useEffect(() => {
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const safeSetState = (updates: Partial<InitializationState>) => {
    if (!mountedRef.current) return;
    setState(prev => ({ ...prev, ...updates }));
  };
  const safeSetAttempt = (text: string) => {
    if (!mountedRef.current) return;
    setAttemptText(text);
  };

  const initialize = async () => {
    if (state.isInitialized || state.isLoading || bootingRef.current) return;
    bootingRef.current = true;

    safeSetState({ isLoading: true, error: null });

    try {
      // Step 0: determine auth first to avoid blocking login on backend readiness
      safeSetState({ currentStep: 'Checking session…' });
      let loadedUser: User | null = null;
      const token = localStorage.getItem('auth_token');
      if (token) {
        try {
          const meResp = await fetch('/api/auth/me', {
            headers: { 'Authorization': `Bearer ${token}` }
          });
          if (meResp.ok) {
            const me = await meResp.json();
            loadedUser = {
              id: me.id || 'unknown',
              username: me.username || 'User',
              email: me.email || 'user@example.com'
            };
          } else {
            // Clear stale/invalid token and auth header so UI returns to login
            try { logout(); } catch { /* ignore */ }
          }
        } catch {
          // Network/ready errors: treat as unauthenticated and show login
          try { logout(); } catch { /* ignore */ }
        }
      }
      // No fallback default user; require login
      safeSetState({ user: loadedUser });

      // If not authenticated, finish initialization and show login page
      if (!loadedUser) {
        safeSetState({ currentStep: 'Awaiting login', isInitialized: true, isLoading: false });
        safeSetAttempt('');
        return;
      }

      // Step 2: settings + characters in parallel. Each with its own retry surface.
      safeSetState({ currentStep: 'Loading settings and characters…' });

      await Promise.all([
        fetchWithRetry(
          `/api/settings/llm?userId=${encodeURIComponent(loadedUser.id)}`,
          {},
          20,
          400,
          10000,
          (a, t) => safeSetAttempt(`(settings ${a}/${t})`)
        ).catch(() => undefined),
        fetchWithRetry(
          `/api/character/user/${encodeURIComponent(loadedUser.id)}`,
          {},
          20,
          400,
          10000,
          (a, t) => safeSetAttempt(`(characters ${a}/${t})`)
        ).catch(() => undefined),
      ]);

      // Step 3: done
      safeSetState({ currentStep: 'Initialization complete!', isInitialized: true, isLoading: false });
      safeSetAttempt('');
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message :
        typeof err === 'string' ? err :
        'Initialization failed';
      safeSetState({
        error: message,
        isLoading: false,
      });
      safeSetAttempt('');
    } finally {
      bootingRef.current = false;
    }
  };

  useEffect(() => {
    // fire on mount
    void initialize();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const value: InitializationContextType = {
    ...state,
    initialize,
  };

  return (
    <InitializationContext.Provider value={value}>
      {/* Lightweight status for startup */}
      {!state.isInitialized && state.isLoading && (
        <div
          style={{
            position: 'fixed',
            bottom: 12,
            right: 12,
            background: '#1f2937',
            color: '#fff',
            padding: '8px 12px',
            borderRadius: 8,
            fontSize: 12,
            opacity: 0.9,
            zIndex: 9999,
          }}
        >
          {state.currentStep} {attemptText}
        </div>
      )}
      {children}
    </InitializationContext.Provider>
  );
};
