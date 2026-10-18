import { useState } from 'react';
import { cancelDownload, retryDownload, type Download } from './api';
import { DiscIcon } from './icons';
import { locale, t } from './i18n';

const cancellable = ['waiting', 'searching', 'not-found', 'downloading', 'failed'];

function retryable(download: Download): boolean {
  return (
    download.state === 'not-found' ||
    download.state === 'failed' ||
    (download.state === 'waiting' && new Date(download.nextAttemptAt).getTime() > Date.now())
  );
}

function when(date: string): string {
  return new Date(date).toLocaleString(locale, { dateStyle: 'short', timeStyle: 'short' });
}

function failureLabel(failure: string | null): string {
  switch (failure) {
    case 'refused':
      return t.downloads.refused;
    case 'stalled':
      return t.downloads.stalled;
    case 'transfer-failed':
      return t.downloads.transferFailed;
    case 'missing-files':
      return t.downloads.missingFiles;
    case 'wrong-tracks':
      return t.downloads.wrongTracks;
    case 'bad-audio':
      return t.downloads.badAudio;
    case 'fake-audio':
      return t.downloads.fakeAudio;
    case 'transcode-failed':
      return t.downloads.transcodeFailed;
    case 'import-failed':
      return t.downloads.importFailed;
    case 'rejected':
      return t.downloads.rejected;
    default:
      return failure ?? '';
  }
}

function stateLabel(download: Download): string {
  switch (download.state) {
    case 'waiting':
      return t.downloads.waiting;
    case 'searching':
      return t.downloads.searching;
    case 'not-found':
      return download.failure === 'no-matching-folder' ? t.downloads.noMatchingFolder : t.downloads.notFound;
    case 'downloading':
      return download.placeInQueue != null
        ? t.downloads.inQueue(download.placeInQueue)
        : t.downloads.downloading(download.tracksDone, download.trackCount);
    case 'downloaded':
      return t.downloads.downloaded;
    case 'importing':
      return t.downloads.importing;
    case 'imported':
      return t.downloads.imported;
    case 'failed':
      return t.downloads.failed(failureLabel(download.failure));
    case 'cancelled':
      return t.downloads.cancelled;
    default:
      return download.state;
  }
}

function nextAttemptLabel(download: Download): string | undefined {
  if (download.state === 'not-found' || download.state === 'failed') {
    return t.downloads.nextAttempt(when(download.nextAttemptAt));
  }

  if (download.state === 'waiting' && new Date(download.nextAttemptAt).getTime() > Date.now()) {
    return t.downloads.nextAttempt(when(download.nextAttemptAt));
  }

  return undefined;
}

export function DownloadItem({
  download,
  onCancelled,
  onRetried,
}: {
  download: Download;
  onCancelled: () => void;
  onRetried: () => void;
}) {
  const [cancelling, setCancelling] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [coverFailed, setCoverFailed] = useState(false);
  const [error, setError] = useState<string>();

  async function handleCancel() {
    setError(undefined);
    setCancelling(true);

    try {
      await cancelDownload(download.id);
      onCancelled();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setCancelling(false);
    }
  }

  async function handleRetry() {
    setError(undefined);
    setRetrying(true);

    try {
      await retryDownload(download.id);
      onRetried();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setRetrying(false);
    }
  }

  return (
    <li className="card artist-card">
      <span className="avatar" aria-hidden="true">
        {download.coverUrl && !coverFailed ? (
          <img src={download.coverUrl} alt="" loading="lazy" onError={() => setCoverFailed(true)} />
        ) : (
          <DiscIcon />
        )}
      </span>
      <div className="artist-info">
        <span className="artist-name release-title" title={download.title}>
          {download.title}
        </span>
        <div className="artist-meta">
          <span className="credit" title={download.credit}>
            {download.credit}
          </span>
        </div>
        <div className="artist-meta">
          <span className="chip">{t.downloads.types[download.type] ?? download.type}</span>
          <span>{t.downloads.tracks(download.trackCount)}</span>
        </div>
      </div>
      <div className="download-side">
        {(retryable(download) || cancellable.includes(download.state)) && (
          <div className="download-actions">
            {retryable(download) && (
              <button className="button-ghost button-small" disabled={retrying} onClick={handleRetry}>
                {t.downloads.retry}
              </button>
            )}
            {cancellable.includes(download.state) && (
              <button
                className="button-ghost button-danger button-small"
                disabled={cancelling}
                onClick={handleCancel}
              >
                {t.downloads.cancel}
              </button>
            )}
          </div>
        )}
        <p className="download-state">{stateLabel(download)}</p>
        {nextAttemptLabel(download) && <p className="download-state">{nextAttemptLabel(download)}</p>}
      </div>
      {download.state === 'downloading' && download.bytesTotal > 0 && (
        <div className="progress">
          <div
            className="progress-fill"
            style={{ width: `${Math.round((download.bytesTransferred / download.bytesTotal) * 100)}%` }}
          />
        </div>
      )}
      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}
    </li>
  );
}
