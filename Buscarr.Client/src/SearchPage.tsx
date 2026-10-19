import { useEffect, useState, type FormEvent } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router';
import { findArtists, getList, logout, type Artist, type User } from './api';
import { ArtistItem } from './ArtistItem';
import { Header } from './Header';
import { SearchIcon } from './icons';
import { t } from './i18n';

function normalize(text: string) {
  return text
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '');
}

export function SearchPage({ user, onLogout }: { user: User; onLogout: () => void }) {
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const [artists, setArtists] = useState<Artist[]>([]);
  const [message, setMessage] = useState('');
  const [filter, setFilter] = useState('');
  const listing = !searchParams.get('q');
  const visible =
    listing && filter ? artists.filter((artist) => normalize(artist.name).includes(normalize(filter))) : artists;

  useEffect(() => {
    const query = searchParams.get('q') ?? '';
    let ignore = false;

    setFilter('');

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

    async function loadList() {
      setArtists([]);
      setMessage(t.list.loading);

      try {
        const listedArtists = await getList();

        if (!ignore) {
          setArtists(listedArtists);
          setMessage(listedArtists.length > 0 ? '' : t.list.empty);
        }
      } catch (err) {
        if (!ignore) {
          setMessage((err as Error).message);
        }
      }
    }

    if (query) {
      search();
    } else {
      loadList();
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
        <Link to="/downloads" className="header-link">
          {t.search.downloads}
        </Link>
        <Link to="/settings" className="header-link">
          {t.search.settings}
        </Link>
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
              onChange={(e) => setFilter(e.target.value)}
            />
            <button className="button-primary">{t.search.search}</button>
          </div>
        </form>

        {message && (
          <p className="status" role="status">
            {message}
          </p>
        )}
        {visible.length > 0 && (
          <ul className="artist-list">
            {visible.map((artist) => (
              <ArtistItem
                key={artist.id}
                artist={artist}
                onRemoved={
                  listing
                    ? () => setArtists((current) => current.filter((other) => other.id !== artist.id))
                    : undefined
                }
              />
            ))}
          </ul>
        )}
      </main>
    </>
  );
}
