import { useEffect, useState } from 'react';
import { useLocation, useParams } from 'react-router';
import { getArtistReleases, requestAlbum, type ArtistRelease } from './api';
import { BackLink } from './BackLink';
import { Header } from './Header';
import { DiscIcon } from './icons';
import { t } from './i18n';

const typeOrder = ['album', 'ep', 'single', 'live', 'compilation', 'soundtrack', 'remix', 'demo'];

function typeRank(type: string) {
  const index = typeOrder.indexOf(type);

  return index === -1 ? typeOrder.length : index;
}

function groupByType(releases: ArtistRelease[]): [string, ArtistRelease[]][] {
  const groups = new Map<string, ArtistRelease[]>();

  for (const release of releases) {
    const group = groups.get(release.type);

    if (group) {
      group.push(release);
    } else {
      groups.set(release.type, [release]);
    }
  }

  return [...groups.entries()].sort(([a], [b]) => typeRank(a) - typeRank(b));
}

export function ReleasesPage() {
  const { id } = useParams();
  const state = useLocation().state as { name?: string } | null;
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
      <Header title={state?.name ?? t.releases.show} />

      <main className="page">
        <BackLink />

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
        {releases && releases.length > 0 && groupByType(releases).map(([type, grouped]) => (
          <section key={type} className="release-section">
            <h2 className="section-title">{t.releases.sections[type] ?? type}</h2>
            <ul className="card-grid">
              {grouped.map((release) => {
                const cover = release.coverUrl;

                return (
                  <li key={release.releaseGroupId} className="card artist-card grid-card">
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
          </section>
        ))}
      </main>
    </>
  );
}
