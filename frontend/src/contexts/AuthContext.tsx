import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';
import axios from 'axios';

interface AuthState {
  token: string | null;
  user: { id: string; username: string; email: string; role?: string } | null;
  login: (identifier: string, password: string, remember?: boolean) => Promise<boolean>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  // Token and user are kept in memory only — the HTTPOnly cookie handles persistence.
  // localStorage is intentionally NOT used to prevent XSS token theft.
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthState['user']>(null);

  // On mount, try to restore session from the server using the HTTPOnly cookie.
  useEffect(() => {
    fetch('/api/auth/me', { credentials: 'include' })
      .then(r => r.ok ? r.json() : null)
      .then(data => {
        if (data?.id) {
          setUser(data as AuthState['user']);
          // Token is managed by the HTTPOnly cookie; set a placeholder so
          // other parts of the app know we are authenticated.
          setToken('cookie');
          axios.defaults.withCredentials = true;
        }
      })
      .catch(() => {/* not logged in */});
  }, []);

  // Keep axios Authorization header in sync when a Bearer token is available.
  useEffect(() => {
    if (token && token !== 'cookie') {
      axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
    } else if (!token) {
      delete axios.defaults.headers.common['Authorization'];
    }
    axios.defaults.withCredentials = true;
  }, [token]);

  const login = useMemo(() => async (identifier: string, password: string, remember?: boolean) => {
    try {
      const resp = await fetch('/api/auth/login', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: identifier,
          email: identifier.includes('@') ? identifier : undefined,
          password,
          remember: !!remember,
        }),
      });
      if (!resp.ok) return false;
      const data = await resp.json();
      const tok = data.access_token as string;
      const usr = data.user as { id: string; username: string; email: string; role?: string };
      setToken(tok);
      setUser(usr);
      axios.defaults.headers.common['Authorization'] = `Bearer ${tok}`;
      axios.defaults.withCredentials = true;
      return true;
    } catch {
      return false;
    }
  }, []);

  const logout = useMemo(() => async () => {
    try {
      await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' });
    } catch { /* ignore */ }
    setToken(null);
    setUser(null);
    delete axios.defaults.headers.common['Authorization'];
  }, []);

  const value: AuthState = { token, user, login, logout: logout as unknown as () => void };
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
