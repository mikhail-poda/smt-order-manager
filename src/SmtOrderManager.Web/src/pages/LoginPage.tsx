import { useState } from 'react';
import { api } from '../api';
import { ProblemMessage } from '../shared/ProblemMessage';
import type { Problem } from '../types';

export function LoginPage({ onLoggedIn }: { onLoggedIn: (username: string) => void }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [problem, setProblem] = useState<Problem | null>(null);

  const login = async () => {
    const result = await api.login(username, password);
    if (result.ok) {
      onLoggedIn(result.value.username);
    } else {
      setProblem(result.problem);
    }
  };

  return (
    <main className="login">
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void login();
        }}
      >
        <h1>SMT Order Manager</h1>
        <label>
          Username
          <input autoComplete="username" value={username} onChange={(event) => setUsername(event.target.value)} />
        </label>
        <label>
          Password
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <button type="submit">Log in</button>
        <ProblemMessage problem={problem} />
      </form>
    </main>
  );
}
