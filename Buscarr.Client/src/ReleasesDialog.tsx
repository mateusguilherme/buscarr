import { useEffect, useRef, useState } from 'react';
import { getArtistReleases, requestAlbum, type Artist, type ArtistRelease } from './api';
import { DiscIcon } from './icons';
import { t } from './i18n';

export function ReleasesDialog({ artist, onClose }: { artist: Artist; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [releases, setReleases] = useState<ArtistRelease[] | null>(null);
  const [requesting, setRequesting] = useState<string>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    dialog.current?.showModal();
  }, []);

  useEffect(() => {
    let ignore = false;

    getArtistReleases(artist.id)
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
  }, [artist.id]);

  async function handleRequest(release: ArtistRelease) {
    setError(undefined);
    setRequesting(release.releaseGroupId);

    try {
      await requestAlbum(artist.id, release.releaseGroupId);
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
    <dialog ref={dialog} className="card dialog releases-dialog" aria-labelledby="releases-dialog-title" onClose={onClose}>
      <h2 id="releases-dialog-title">{t.releases.title(artist.name)}</h2>

      {!releases && !error && (
        <p className="status" role="status">
          {t.releases.loading}
        </p>
      )}
      {releases && releases.length === 0 && <p className="status hint">{t.releases.empty}</p>}
      {releases && releases.length > 0 && (
        <ul className="release-list">
          {releases.map((release) => (
            <li key={release.releaseGroupId} className="source-row">
              <span className="avatar" aria-hidden="true">
                {release.coverUrl ? <img src={release.coverUrl} alt="" loading="lazy" /> : <DiscIcon />}
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
              {release.requested ? (
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
            </li>
          ))}
        </ul>
      )}

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <div className="dialog-actions">
        <button type="button" className="button-ghost" onClick={onClose}>
          {t.releases.close}
        </button>
      </div>
    </dialog>
  );
}
