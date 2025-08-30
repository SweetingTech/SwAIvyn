import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';
import axios from 'axios';

interface AuthState {
  token: string | null;
  user: { id: string; username: string; email: string; role?: string } | null;
  login: (identifier: string, password: string) => Promise<boolean>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
};

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('auth_token'));
  const [user, setUser] = useState<AuthState['user']>(() => {
    const raw = localStorage.getItem('auth_user');
    return raw ? JSON.parse(raw) : null;
  });

  // Keep axios Authorization header in sync
  useEffect(() => {
    if (token) {
      axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
    } else {
      delete axios.defaults.headers.common['Authorization'];
    }
  }, [token]);

  const login = useMemo(() => async (identifier: string, password: string) => {
    try {
      const resp = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: identifier, email: identifier.includes('@') ? identifier : undefined, password }),
      });
      if (!resp.ok) return false;
      const data = await resp.json();
      const tok = data.access_token as string;
      const usr = data.user as { id: string; username: string; email: string; role?: string };
      setToken(tok);
      setUser(usr);
      localStorage.setItem('auth_token', tok);
      localStorage.setItem('auth_user', JSON.stringify(usr));
      return true;
    } catch {
      return false;
    }
  }, []);

  const logout = useMemo(() => () => {
    setToken(null);
    setUser(null);
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_user');
  }, []);

  const value: AuthState = { token, user, login, logout };
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

