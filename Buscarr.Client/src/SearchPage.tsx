import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { findArtist, logout, type Artist, type User } from './api';
import { countries } from './countries';
import { t } from './i18n';

export function SearchPage({ user, onLogout }: { user: User; onLogout: () => void }) {
  const [query, setQuery] = useState('');
  const [country, setCountry] = useState('');
  const [artist, setArtist] = useState<Artist | null>(null);
  const [message, setMessage] = useState('');

  async function handleSearch(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setArtist(null);
    setMessage(t.search.searching);

    try {
      const found = await findArtist(query, country);
      setArtist(found);
      setMessage(found ? '' : t.search.notFound);
    } catch {
      setMessage(t.search.error);
    }
  }

  async function handleLogout() {
    try {
      await logout();
      onLogout();
    } catch (err) {
      setMessage((err as Error).message);
    }
  }

  return (
    <>
      <p>
        {t.search.greeting(user.name)} <button onClick={handleLogout}>{t.search.logout}</button>
      </p>

      <form onSubmit={handleSearch}>
        <p>
          <input
            placeholder={t.search.artistName}
            required
            autoFocus
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />{' '}
          <select value={country} onChange={(e) => setCountry(e.target.value)}>
            <option value="">{t.search.allCountries}</option>
            {countries.map(({ code, name }) => (
              <option key={code} value={code}>
                {name}
              </option>
            ))}
          </select>{' '}
          <button>{t.search.search}</button>
        </p>
      </form>

      {message && <p>{message}</p>}
      {artist && (
        <Link to={`/artist/${artist.id}`}>
          <h2>{artist.name}</h2>
        </Link>
      )}
    </>
  );
}
