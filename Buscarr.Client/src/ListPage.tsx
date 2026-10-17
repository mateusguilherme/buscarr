import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { getList, type Artist } from './api';
import { ArtistItem } from './ArtistItem';
import { Header } from './Header';
import { ArrowLeftIcon } from './icons';
import { t } from './i18n';

export function ListPage() {
  const [artists, setArtists] = useState<Artist[] | null>(null);
  const [error, setError] = useState<string>();

  useEffect(() => {
    let ignore = false;

    async function loadList() {
      try {
        const result = await getList();

        if (!ignore) {
          setArtists(result);
        }
      } catch (err) {
        if (!ignore) {
          setError((err as Error).message);
        }
      }
    }

    loadList();

    return () => {
      ignore = true;
    };
  }, []);

  return (
    <>
      <Header />

      <main className="page">
        <Link to="/" className="back">
          <ArrowLeftIcon />
          {t.common.back}
        </Link>

        <h1 className="page-title">{t.list.title}</h1>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        {!artists && !error && (
          <p className="status" role="status">
            {t.list.loading}
          </p>
        )}
        {artists && artists.length === 0 && <p className="status hint">{t.list.empty}</p>}
        {artists && artists.length > 0 && (
          <ul className="artist-list">
            {artists.map((artist) => (
              <ArtistItem
                key={artist.id}
                artist={artist}
                onRemoved={() => setArtists((current) => current && current.filter((other) => other.id !== artist.id))}
              />
            ))}
          </ul>
        )}
      </main>
    </>
  );
}
