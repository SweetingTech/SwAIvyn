import React, { useState } from 'react';

export default function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [pinCode, setPinCode] = useState('');
  const [error, setError] = useState('');
  const [isPinLogin, setIsPinLogin] = useState(false);

  const handleLogin = async () => {
    setError('');
    try {
      const response = await fetch(isPinLogin ? '/api/auth/pin-login' : '/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(
          isPinLogin
            ? { username, pinCode }
            : { username, password }
        ),
      });
      if (!response.ok) {
        const errorText = await response.text();
        setError(errorText || 'Login failed');
        return;
      }
      const data = await response.json();
      alert(`Welcome ${data.username}!`);
      // TODO: Redirect to main app page or set auth state
    } catch (err) {
      setError('Network error');
    }
  };

  return (
    <div className="min-h-screen flex flex-col justify-center items-center bg-gray-100 p-4">
      <div className="bg-white p-6 rounded shadow-md w-full max-w-md">
        <h2 className="text-2xl font-bold mb-4 text-center">Login</h2>
        <div className="mb-4">
          <label className="block mb-1 font-semibold">Username</label>
          <input
            type="text"
            className="w-full border border-gray-300 rounded px-3 py-2"
            value={username}
            onChange={e => setUsername(e.target.value)}
          />
        </div>
        {!isPinLogin && (
          <div className="mb-4">
            <label className="block mb-1 font-semibold">Password</label>
            <input
              type="password"
              className="w-full border border-gray-300 rounded px-3 py-2"
              value={password}
              onChange={e => setPassword(e.target.value)}
            />
          </div>
        )}
        {isPinLogin && (
          <div className="mb-4">
            <label className="block mb-1 font-semibold">PIN Code</label>
            <input
              type="password"
              className="w-full border border-gray-300 rounded px-3 py-2"
              value={pinCode}
              onChange={e => setPinCode(e.target.value)}
            />
          </div>
        )}
        {error && <div className="mb-4 text-red-600">{error}</div>}
        <button
          onClick={handleLogin}
          className="w-full bg-blue-600 text-white py-2 rounded hover:bg-blue-700 transition"
        >
          {isPinLogin ? 'Login with PIN' : 'Login'}
        </button>
        <div className="mt-4 text-center">
          <button
            onClick={() => setIsPinLogin(!isPinLogin)}
            className="text-blue-600 hover:underline"
          >
            {isPinLogin ? 'Use Username/Password' : 'Use PIN Code'}
          </button>
        </div>
      </div>
    </div>
  );
}
