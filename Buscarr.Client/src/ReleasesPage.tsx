import { useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router';
import { getArtistReleases, requestAlbum, type ArtistRelease } from './api';
import { Header } from './Header';
import { ArrowLeftIcon, DiscIcon } from './icons';
import { t } from './i18n';

export function ReleasesPage() {
  const { id } = useParams();
  const state = useLocation().state as { name?: string; from?: string } | null;
  const [releases, setReleases] = useState<ArtistRelease[] | null>(null);
  const [requesting, setRequesting] = useState<string>();
  const [failedCovers, setFailedCovers] = useState<string[]>([]);
  const [error, setError] = useState<string>();

  useEffect(() => {
    let ignore = false;

    getArtistReleases(id!)
      .then((result) => {
        if (!ignore) {
          setReleases(result);
        }
      })
      .catch((err) => {
        if (!ignore) {
          setError((err as Error).message);
        }
      });

    return () => {
      ignore = true;
    };
  }, [id]);

  async function handleRequest(release: ArtistRelease) {
    setError(undefined);
    setRequesting(release.releaseGroupId);

    try {
      await requestAlbum(id!, release.releaseGroupId);
      setReleases((current) =>
        current && current.map((other) =>
          other.releaseGroupId === release.releaseGroupId ? { ...other, requested: true } : other));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setRequesting(undefined);
    }
  }

  return (
    <>
      <Header />

      <main className="page">
        <Link to={state?.from ?? '/'} className="back">
          <ArrowLeftIcon />
          {t.common.back}
        </Link>

        <h1 className="page-title">{state?.name ? t.releases.title(state.name) : t.releases.show}</h1>

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
        {releases && releases.length === 0 && <p className="status hint">{t.releases.empty}</p>}
        {releases && releases.length > 0 && (
          <ul className="artist-list">
            {releases.map((release) => {
              const cover = release.coverUrl;

              return (
                <li key={release.releaseGroupId} className="card artist-card">
                  <span className="avatar" aria-hidden="true">
                    {cover && !failedCovers.includes(cover) ? (
                      <img
                        src={cover}
                        alt=""
                        loading="lazy"
                        onError={() => setFailedCovers((current) => [...current, cover])}
                      />
                    ) : (
                      <DiscIcon />
                    )}
                  </span>
                  <div className="artist-info">
                    <span className="artist-name release-title" title={release.title}>
                      {release.title}
                    </span>
                    <div className="artist-meta">
                      {release.firstReleaseDate && <span>{release.firstReleaseDate.slice(0, 4)}</span>}
                      <span className="chip">{t.downloads.types[release.type] ?? release.type}</span>
                      <span>{t.downloads.tracks(release.trackCount)}</span>
                    </div>
                  </div>
                  <div className="artist-actions">
                    {release.inLibrary ? (
                      <span className="chip">{t.releases.inLibrary}</span>
                    ) : release.requested ? (
                      <span className="chip">{t.releases.requested}</span>
                    ) : (
                      <button
                        className="button-ghost button-small"
                        disabled={requesting === release.releaseGroupId}
                        onClick={() => handleRequest(release)}
                      >
                        {t.releases.request}
                      </button>
                    )}
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </main>
    </>
  );
}
