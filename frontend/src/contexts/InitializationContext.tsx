import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useRef,
  ReactNode,
} from 'react';
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
      // Step 0: wait for backend readiness to avoid early timeouts during migrations/seed.
      safeSetState({ currentStep: 'Waiting for backend…' });
      await fetchWithRetry(
        '/api/readyz',
        {},
        60,   // up to ~6–7 minutes worst‑case
        500,  // gentle backoff
        8000, // per‑attempt timeout
        (a, t) => safeSetAttempt(`(backend ${a}/${t})`)
      );

      // Step 1: load default user with retries
      safeSetState({ currentStep: 'Loading user profile…' });
      const userResponse = await fetchWithRetry(
        '/api/user/default',
        {},
        20,
        400,
        10000,
        (a, t) => safeSetAttempt(`(attempt ${a}/${t})`)
      );

      if (!userResponse.ok) {
        throw new Error(`Failed to load user profile (${userResponse.status})`);
      }

      // Some backends return 204. Guard parse.
      let userData: any = null;
      const text = await userResponse.text();
      if (text) userData = JSON.parse(text);

      const user: User = {
        id: userData?.id ?? 'unknown',
        username: userData?.username || 'User',
        email: userData?.email || 'user@example.com',
      };
      safeSetState({ user });

      // Step 2: settings + characters in parallel. Each with its own retry surface.
      safeSetState({ currentStep: 'Loading settings and characters…' });

      const [settingsResponse, charactersResponse] = await Promise.all([
        fetchWithRetry(
          `/api/settings/llm?userId=${encodeURIComponent(user.id)}`,
          {},
          20,
          400,
          10000,
          (a, t) => safeSetAttempt(`(settings ${a}/${t})`)
        ).catch(e => e as Response), // normalize
        fetchWithRetry(
          `/api/character/user/${encodeURIComponent(user.id)}`,
          {},
          20,
          400,
          10000,
          (a, t) => safeSetAttempt(`(characters ${a}/${t})`)
        ).catch(e => e as Response),
      ]);

      if (settingsResponse && (settingsResponse as Response).ok) {
        try {
          const settings = await (settingsResponse as Response).json();
          // eslint-disable-next-line no-console
          console.log('Settings loaded:', settings);
        } catch {
          // ignore parse issues
        }
      }

      if (charactersResponse && (charactersResponse as Response).ok) {
        try {
          const characters = await (charactersResponse as Response).json();
          // eslint-disable-next-line no-console
          console.log('Characters loaded:', Array.isArray(characters) ? characters.length : 'n/a');
        } catch {
          // ignore parse issues
        }
      }

      // Step 3: done
      safeSetState({
        currentStep: 'Initialization complete!',
        isInitialized: true,
        isLoading: false,
      });
      safeSetAttempt('');
      // eslint-disable-next-line no-console
      console.log('Application initialization completed successfully');
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message :
        typeof err === 'string' ? err :
        'Initialization failed';
      // eslint-disable-next-line no-console
      console.error('Initialization failed:', err);
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
