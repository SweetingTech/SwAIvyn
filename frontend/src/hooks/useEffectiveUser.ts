import { useEffect, useMemo, useRef, useState } from 'react';
import { useInitialization } from '../contexts/InitializationContext';
import { useAuth } from '../contexts/AuthContext';

interface EffectiveUser {
  userId: string | null;
  token: string | null;
  headers: Record<string, string>;
  resolved: boolean;
}

export default function useEffectiveUser(): EffectiveUser {
  const { user: initUser } = useInitialization();
  const { user: authUser, token: authToken } = useAuth();
  const [userId, setUserId] = useState<string | null>(authUser?.id || initUser?.id || null);
  const [token, setToken] = useState<string | null>(authToken || ((): string | null => { try { return localStorage.getItem('auth_token'); } catch { return null; } })());
  const [resolved, setResolved] = useState<boolean>(Boolean(authUser?.id || initUser?.id));
  const fetchedRef = useRef<boolean>(false);

  useEffect(() => {
    const uid = authUser?.id || initUser?.id || null;
    setUserId(uid);
    const t = authToken || ((): string | null => { try { return localStorage.getItem('auth_token'); } catch { return null; } })();
    setToken(t);
    if (uid) {
      setResolved(true);
      return;
    }
    if (!fetchedRef.current && t) {
      fetchedRef.current = true;
      (async () => {
        try {
          const resp = await fetch('/api/auth/me', { headers: { 'Authorization': `Bearer ${t}` } });
          if (resp.ok) {
            const me = await resp.json();
            if (me?.id) {
              setUserId(me.id);
            }
          }
        } catch {}
        setResolved(true);
      })();
    } else {
      setResolved(Boolean(uid));
    }
  }, [authUser?.id, initUser?.id, authToken]);

  const headers = useMemo(() => {
    const h: Record<string, string> = {};
    if (token) h['Authorization'] = `Bearer ${token}`;
    return h;
  }, [token]);

  return { userId, token, headers, resolved };
}

