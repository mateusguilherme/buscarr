import { useState } from 'react';
import { cancelDownload, getCandidates, pickCandidate, retryDownload, type Download, type SourceCandidate } from './api';
import { DiscIcon } from './icons';
import { locale, t } from './i18n';

const cancellable = ['waiting', 'searching', 'not-found', 'downloading', 'failed'];
const searched = ['not-found', 'failed'];

export function awaitingRelease(download: Download): boolean {
  return (
    download.state === 'waiting' &&
    download.firstReleaseDate !== null &&
    new Date(download.firstReleaseDate).getTime() > Date.now()
  );
}

function qualityLabel(quality: string): string {
  return t.downloads.qualities[quality] ?? quality;
}

function candidateStatus(candidate: SourceCandidate): string {
  if (candidate.rejection) {
    return t.sources.rejections[candidate.rejection] ?? candidate.rejection;
  }

  if (candidate.hasFreeUploadSlot) {
    return t.sources.available;
  }

  return candidate.queueLength > 0 ? t.sources.inQueue(candidate.queueLength) : t.sources.busy;
}

function candidateFiles(candidate: SourceCandidate): string {
  const format = candidate.bitRate ? `${candidate.format} ${candidate.bitRate} kbps` : candidate.format;

  return t.sources.files(candidate.fileCount, format);
}

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

export function failureLabel(failure: string | null): string {
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
      return awaitingRelease(download) ? t.downloads.waitingRelease(download.firstReleaseDate!) : t.downloads.waiting;
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
      return download.acceptedQuality
        ? t.downloads.importedBelowCutoff(qualityLabel(download.acceptedQuality), qualityLabel(download.quality))
        : t.downloads.imported;
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
  const [failedCover, setFailedCover] = useState<string>();
  const [error, setError] = useState<string>();
  const [candidates, setCandidates] = useState<SourceCandidate[] | null>(null);
  const [sourcesOpen, setSourcesOpen] = useState(false);
  const [picking, setPicking] = useState(false);

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

  async function toggleSources() {
    setError(undefined);

    if (sourcesOpen) {
      setSourcesOpen(false);

      return;
    }

    setSourcesOpen(true);

    try {
      setCandidates(await getCandidates(download.id));
    } catch (err) {
      setSourcesOpen(false);
      setError((err as Error).message);
    }
  }

  async function handlePick(candidate: SourceCandidate) {
    setError(undefined);
    setPicking(true);

    try {
      await pickCandidate(download.id, candidate.id);
      setSourcesOpen(false);
      onRetried();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setPicking(false);
    }
  }

  return (
    <li className="card artist-card">
      <span className="avatar" aria-hidden="true">
        {download.coverUrl && download.coverUrl !== failedCover ? (
          <img
            src={download.coverUrl}
            alt=""
            loading="lazy"
            onError={() => setFailedCover(download.coverUrl ?? undefined)}
          />
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
            {searched.includes(download.state) && (
              <button className="button-ghost button-small" onClick={toggleSources}>
                {t.sources.show}
              </button>
            )}
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
        {download.state === 'downloading' && download.bytesTotal > 0 && (
          <div className="progress">
            <div
              className="progress-fill"
              style={{ width: `${Math.round((download.bytesTransferred / download.bytesTotal) * 100)}%` }}
            />
          </div>
        )}
      </div>
      {sourcesOpen && (
        <div className="sources">
          {!candidates && <p className="status">{t.sources.loading}</p>}
          {candidates && candidates.length === 0 && <p className="status hint">{t.sources.empty}</p>}
          {candidates && candidates.length > 0 && (
            <ul className="source-list">
              {candidates.map((candidate) => (
                <li key={candidate.id} className="source-row">
                  <div className="artist-info">
                    <span className="artist-name" title={candidate.folder}>
                      {candidate.username}
                    </span>
                    <div className="artist-meta">
                      <span>{candidateFiles(candidate)}</span>
                      <span>{candidateStatus(candidate)}</span>
                    </div>
                  </div>
                  {candidate.rejection !== 'track-count' && (
                    <button
                      className="button-ghost button-small"
                      disabled={picking}
                      onClick={() => handlePick(candidate)}
                    >
                      {t.sources.pick}
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
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
