import { useState } from 'react';
import { discardQuarantined, importQuarantined, type QuarantinedAlbum } from './api';
import { failureLabel } from './DownloadItem';
import { DiscIcon } from './icons';
import { locale, t } from './i18n';

export function QuarantineItem({ album, onGone }: { album: QuarantinedAlbum; onGone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  async function handle(action: (id: number) => Promise<void>) {
    setError(undefined);
    setBusy(true);

    try {
      await action(album.id);
      onGone();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <li className="card artist-card">
      <span className="avatar" aria-hidden="true">
        <DiscIcon />
      </span>
      <div className="artist-info">
        <span className="artist-name release-title" title={album.title}>
          {album.title}
        </span>
        <div className="artist-meta">
          <span className="credit" title={album.artistName}>
            {album.artistName}
          </span>
        </div>
        <div className="artist-meta">
          <span>{failureLabel(album.reason)}</span>
          {album.username && <span>{t.quarantine.from(album.username)}</span>}
          <span>{new Date(album.at).toLocaleString(locale, { dateStyle: 'short', timeStyle: 'short' })}</span>
        </div>
      </div>
      <div className="download-side">
        <div className="download-actions">
          {album.canImport && (
            <button className="button-ghost button-small" disabled={busy} onClick={() => handle(importQuarantined)}>
              {t.quarantine.importAnyway}
            </button>
          )}
          <button
            className="button-ghost button-danger button-small"
            disabled={busy}
            onClick={() => handle(discardQuarantined)}
          >
            {t.quarantine.discard}
          </button>
        </div>
      </div>
      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}
    </li>
  );
}
