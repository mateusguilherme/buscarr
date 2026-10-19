import { useEffect, useState } from 'react';
import { getCurrentUser, type User } from './api';
import { LoginForm } from './LoginForm';
import { SearchPage } from './SearchPage';

export function HomePage() {
  const [user, setUser] = useState<User | null>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    async function loadUser() {
      try {
        setUser(await getCurrentUser());
      } catch (err) {
        setError((err as Error).message);
      }
    }

    loadUser();
  }, []);

  if (error) {
    return (
      <main className="auth">
        <p className="alert" role="alert">
          {error}
        </p>
      </main>
    );
  }

  if (user === undefined) {
    return null;
  }

  if (user === null) {
    return <LoginForm onLogin={setUser} />;
  }

  return <SearchPage onLogout={() => setUser(null)} />;
}
