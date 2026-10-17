import { useEffect, useState } from 'react';
import { Link, useLocation, useParams, useSearchParams } from 'react-router';
import { getArtist, getArtistReleases, type Release } from './api';
import { Header } from './Header';
import { ArrowLeftIcon } from './icons';
import { locale, t } from './i18n';
import type { SearchState } from './SearchPage';

const releaseTypes = [
  { value: 'album', label: t.releases.albums },
  { value: 'ep', label: t.releases.eps },
  { value: 'single', label: t.releases.singles },
  { value: 'all', label: t.releases.all },
];

const dateFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
});

export function ReleasesPage() {
  const location = useLocation();
  const from = location.state as SearchState | null;
  const { id = '' } = useParams();
  const [searchParams] = useSearchParams();
  const type = searchParams.get('type') ?? 'album';
  const [name, setName] = useState(from?.artists.find((artist) => artist.id === id)?.name);
  const [releases, setReleases] = useState<Release[] | null>(null);
  const [error, setError] = useState<string>();
  const shown = releases && (type === 'all' ? releases : releases.filter((release) => release.type === type));

  useEffect(() => {
    let ignore = false;

    async function loadArtist() {
      try {
        const artist = await getArtist(id);

        if (!ignore) {
          setName(artist.name);
        }
      } catch (err) {
        if (!ignore) {
          setError((err as Error).message);
        }
      }
    }

    async function loadReleases() {
      setReleases(null);
      setError(undefined);

      try {
        const result = await getArtistReleases(id);

        if (!ignore) {
          setReleases(result);
        }
      } catch (err) {
        if (!ignore) {
          setError((err as Error).message);
        }
      }
    }

    if (!name) {
      loadArtist();
    }

    loadReleases();

    return () => {
      ignore = true;
    };
  }, [id]);

  return (
    <>
      <Header />

      <main className="page">
        <Link to={`/${from?.search ?? ''}`} state={from} className="back">
          <ArrowLeftIcon />
          {t.releases.back}
        </Link>

        <div className="artist-hero">
          {name && <h1>{name}</h1>}
        </div>

        <nav className="segmented" aria-label={t.releases.type}>
          {releaseTypes.map((option) => (
            <Link
              key={option.value}
              to={`?type=${option.value}`}
              state={from}
              replace
              aria-current={option.value === type ? 'page' : undefined}
            >
              {option.label}
            </Link>
          ))}
        </nav>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        {!releases && !error && (
          <p className="status" role="status">
            {t.releases.loading}
          </p>
        )}
        {shown && shown.length === 0 && <p className="status">{t.releases.empty}</p>}
        {shown && (
          <>
            <ReleaseSection title={t.releases.studio} releases={shown.filter((r) => !r.live)} />
            <ReleaseSection title={t.releases.live} releases={shown.filter((r) => r.live)} />
          </>
        )}
      </main>
    </>
  );
}

function ReleaseSection({ title, releases }: { title: string; releases: Release[] }) {
  if (releases.length === 0) {
    return null;
  }

  return (
    <section className="release-section">
      <h2>{title}</h2>
      <ul className="card release-list">
        {releases.map((release) => (
          <li key={release.title} className="release">
            <span className="release-title">{release.title}</span>
            <time dateTime={release.date}>{dateFormat.format(new Date(release.date))}</time>
          </li>
        ))}
      </ul>
    </section>
  );
}
