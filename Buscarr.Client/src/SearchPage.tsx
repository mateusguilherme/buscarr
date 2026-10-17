import { useEffect, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router';
import { findArtists, logout, type Artist, type User } from './api';
import { Header } from './Header';
import { PinIcon, SearchIcon } from './icons';
import { t } from './i18n';

export type SearchState = { search: string; artists: Artist[] };

export function SearchPage({ user, onLogout }: { user: User; onLogout: () => void }) {
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [artists, setArtists] = useState<Artist[]>([]);
  const [message, setMessage] = useState('');
  const linkState: SearchState = { search: location.search, artists };

  useEffect(() => {
    const cached = (location.state as SearchState | null)?.artists;
    const query = searchParams.get('q') ?? '';
    let ignore = false;

    async function search() {
      setArtists([]);
      setMessage(t.search.searching);

      try {
        const found = await findArtists(query);

        if (!ignore) {
          setArtists(found);
          setMessage(found.length > 0 ? '' : t.search.notFound);
        }
      } catch {
        if (!ignore) {
          setMessage(t.search.error);
        }
      }
    }

    if (query && !cached) {
      search();
    } else {
      setArtists(cached ?? []);
      setMessage('');
    }

    return () => {
      ignore = true;
    };
  }, [location.key]);

  function handleSearch(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSearchParams({ q: String(new FormData(e.currentTarget).get('q')) });
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
        <form key={location.search} className="search" role="search" onSubmit={handleSearch}>
          <div className="search-bar">
            <SearchIcon />
            <input
              name="q"
              placeholder={t.search.artistName}
              required
              autoFocus
              defaultValue={searchParams.get('q') ?? ''}
            />
            <button className="button-primary">{t.search.search}</button>
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
                  <Link
                    className="artist-name"
                    to={`/artist/${artist.id}`}
                    state={linkState}
                    onClick={() => navigate(location, { replace: true, state: linkState })}
                  >
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
                </li>
              ))}
            </ul>
          </section>
        )}
      </main>
    </>
  );
}
