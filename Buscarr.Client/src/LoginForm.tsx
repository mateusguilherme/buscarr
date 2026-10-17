import { useState, type FormEvent } from 'react';
import { login, register, type User } from './api';

export function LoginForm({ onLogin }: { onLogin: (user: User) => void }) {
  const [creating, setCreating] = useState(false);
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(undefined);

    try {
      const user = creating ? await register(email, name, password) : await login(email, password);
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
    <form onSubmit={handleSubmit}>
      <p>
        <input
          type="email"
          placeholder="E-mail"
          required
          autoFocus
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
      </p>
      {creating && (
        <p>
          <input
            placeholder="Nome"
            required
            autoComplete="name"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </p>
      )}
      <p>
        <input
          type="password"
          placeholder="Senha"
          required
          minLength={creating ? 8 : undefined}
          autoComplete={creating ? 'new-password' : 'current-password'}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
      </p>
      <button>{creating ? 'Criar usuário' : 'Entrar'}</button>{' '}
      <button type="button" onClick={toggleMode}>
        {creating ? 'Já tenho usuário' : 'Criar usuário'}
      </button>
      {error && <p>{error}</p>}
    </form>
  );
}
