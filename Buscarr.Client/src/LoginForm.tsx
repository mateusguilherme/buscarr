import { useState, type FormEvent } from 'react';
import { login, register, type User } from './api';
import { Brand } from './Header';
import { t } from './i18n';

export function LoginForm({ onLogin }: { onLogin: (user: User) => void }) {
  const [creating, setCreating] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(undefined);

    try {
      const user = creating ? await register(email, password) : await login(email, password);
      onLogin(user);
    } catch (err) {
      setError((err as Error).message);
    }
  }

  function toggleMode() {
    setCreating(!creating);
    setError(undefined);
  }

  return (
    <main className="auth">
      <div className="card auth-card">
        <h1 className="brand brand-large">
          <Brand />
        </h1>
        <form className="stack" onSubmit={handleSubmit}>
          <input
            type="email"
            placeholder={t.login.email}
            required
            autoFocus
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <input
            type="password"
            placeholder={t.login.password}
            required
            minLength={creating ? 8 : undefined}
            autoComplete={creating ? 'new-password' : 'current-password'}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
          <button className="button-primary">{creating ? t.login.createUser : t.login.signIn}</button>
          <button type="button" className="button-ghost" onClick={toggleMode}>
            {creating ? t.login.haveUser : t.login.createUser}
          </button>
        </form>
      </div>
    </main>
  );
}
