import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';

interface User {
  id: string;
  username: string;
  email: string;
  role: string;
}

export default function AdminUsersPage() {
  const [users, setUsers] = useState<User[]>([]);
  const [form, setForm] = useState({ id: '', username: '', email: '', password: '', role: 'user' });
  const [error, setError] = useState('');
  const { token } = useAuth();

  // Use token from AuthContext (memory-only — never localStorage)
  const authHeader = useMemo(() => {
    return token ? { 'Authorization': `Bearer ${token}` as string } : undefined;
  }, [token]);

  const load = async () => {
    setError('');
    try {
      const r = await fetch('/api/admin/users', { headers: authHeader });
      if (!r.ok) throw new Error(await r.text());
      setUsers(await r.json());
    } catch (e: any) {
      setError(e?.message || 'Failed to load users');
    }
  };

  useEffect(() => { void load(); }, []);

  const create = async () => {
    setError('');
    try {
      const r = await fetch('/api/admin/users', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...(authHeader ?? {}) },
        body: JSON.stringify(form)
      });
      if (!r.ok) throw new Error(await r.text());
      setForm({ id: '', username: '', email: '', password: '', role: 'user' });
      await load();
    } catch (e: any) {
      setError(e?.message || 'Failed to create user');
    }
  };

  return (
    <div className="p-4">
      <h1 className="text-2xl font-bold mb-4">Admin: Users</h1>
      {error && <div className="text-red-600 mb-2">{error}</div>}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="border rounded p-4">
          <h2 className="font-semibold mb-2">Create User</h2>
          <div className="space-y-2">
            <input className="w-full border px-2 py-1" placeholder="ID" value={form.id} onChange={e => setForm({ ...form, id: e.target.value })} />
            <input className="w-full border px-2 py-1" placeholder="Username" value={form.username} onChange={e => setForm({ ...form, username: e.target.value })} />
            <input className="w-full border px-2 py-1" placeholder="Email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
            <input className="w-full border px-2 py-1" placeholder="Password" type="password" value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} />
            <select className="w-full border px-2 py-1" value={form.role} onChange={e => setForm({ ...form, role: e.target.value })}>
              <option value="user">user</option>
              <option value="admin">admin</option>
            </select>
            <button className="bg-blue-600 text-white px-3 py-1 rounded" onClick={create}>Create</button>
          </div>
        </div>
        <div className="border rounded p-4">
          <h2 className="font-semibold mb-2">Existing Users</h2>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left border-b">
                <th className="py-1">ID</th>
                <th className="py-1">Username</th>
                <th className="py-1">Email</th>
                <th className="py-1">Role</th>
              </tr>
            </thead>
            <tbody>
              {users.map(u => (
                <tr key={u.id} className="border-b">
                  <td className="py-1 pr-2">{u.id}</td>
                  <td className="py-1 pr-2">{u.username}</td>
                  <td className="py-1 pr-2">{u.email}</td>
                  <td className="py-1 pr-2">{u.role}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
