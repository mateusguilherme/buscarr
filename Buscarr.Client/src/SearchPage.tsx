import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { findArtists, logout, type Artist, type User } from './api';
import { countries } from './countries';
import { t } from './i18n';

function details(artist: Artist) {
  return [artist.disambiguation, artist.area].filter(Boolean).join(' · ');
}

export function SearchPage({ user, onLogout }: { user: User; onLogout: () => void }) {
  const [query, setQuery] = useState('');
  const [country, setCountry] = useState('');
  const [type, setType] = useState('');
  const [artists, setArtists] = useState<Artist[]>([]);
  const [message, setMessage] = useState('');

  async function handleSearch(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setArtists([]);
    setMessage(t.search.searching);

    try {
      const found = await findArtists(query, country, type);
      setArtists(found);
      setMessage(found.length > 0 ? '' : t.search.notFound);
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
          <select aria-label={t.search.country} value={country} onChange={(e) => setCountry(e.target.value)}>
            <option value="">{t.search.allCountries}</option>
            {countries.map(({ code, name }) => (
              <option key={code} value={code}>
                {name}
              </option>
            ))}
          </select>{' '}
          <select aria-label={t.search.type} value={type} onChange={(e) => setType(e.target.value)}>
            <option value="">{t.search.anyType}</option>
            <option value="group">{t.search.band}</option>
            <option value="person">{t.search.solo}</option>
          </select>{' '}
          <button>{t.search.search}</button>
        </p>
      </form>

      {message && <p>{message}</p>}
      {artists.length > 0 && (
        <ul>
          {artists.map((artist) => (
            <li key={artist.id}>
              <Link to={`/artist/${artist.id}`}>{artist.name}</Link>
              {details(artist) && ` — ${details(artist)}`}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
