import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { findArtists, logout, type Artist, type User } from './api';
import { Avatar } from './Avatar';
import { countries } from './countries';
import { Header } from './Header';
import { PinIcon, SearchIcon } from './icons';
import { t } from './i18n';

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
      <Header>
        <span className="greeting">{t.search.greeting(user.name)}</span>
        <button className="button-ghost button-small" onClick={handleLogout}>
          {t.search.logout}
        </button>
      </Header>

      <main className="page">
        <form className="search" role="search" onSubmit={handleSearch}>
          <div className="search-bar">
            <SearchIcon />
            <input
              placeholder={t.search.artistName}
              required
              autoFocus
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
            <button className="button-primary">{t.search.search}</button>
          </div>
          <div className="filters">
            <select aria-label={t.search.country} value={country} onChange={(e) => setCountry(e.target.value)}>
              <option value="">{t.search.allCountries}</option>
              {countries.map(({ code, name }) => (
                <option key={code} value={code}>
                  {name}
                </option>
              ))}
            </select>
            <select aria-label={t.search.type} value={type} onChange={(e) => setType(e.target.value)}>
              <option value="">{t.search.anyType}</option>
              <option value="group">{t.search.band}</option>
              <option value="person">{t.search.solo}</option>
            </select>
          </div>
        </form>

        {message && (
          <p className="status" role="status">
            {message}
          </p>
        )}
        {!message && artists.length === 0 && <p className="status hint">{t.search.hint}</p>}
        {artists.length > 0 && (
          <section>
            <p className="result-count">{t.search.found(artists.length)}</p>
            <ul className="artist-list">
              {artists.map((artist) => (
                <li key={artist.id} className="card artist-card">
                  <Avatar name={artist.name} />
                  <div className="artist-info">
                    <Link className="artist-name" to={`/artist/${artist.id}`}>
                      {artist.name}
                    </Link>
                    {(artist.disambiguation || artist.area) && (
                      <div className="artist-meta">
                        {artist.disambiguation && <span>{artist.disambiguation}</span>}
                        {artist.area && (
                          <span className="chip">
                            <PinIcon />
                            {artist.area}
                          </span>
                        )}
                      </div>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          </section>
        )}
      </main>
    </>
  );
}
