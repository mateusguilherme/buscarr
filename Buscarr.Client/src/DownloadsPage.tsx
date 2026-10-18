import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { getDownloads, getQuarantine, type Download, type QuarantinedAlbum } from './api';
import { DownloadItem } from './DownloadItem';
import { Header } from './Header';
import { ArrowLeftIcon } from './icons';
import { QuarantineItem } from './QuarantineItem';
import { t } from './i18n';

const retryWait = 10_000;

export function DownloadsPage() {
  const [downloads, setDownloads] = useState<Download[] | null>(null);
  const [quarantine, setQuarantine] = useState<QuarantinedAlbum[]>([]);
  const [error, setError] = useState<string>();
  const failedCount = downloads?.filter((download) => download.state === 'failed').length ?? 0;

  useEffect(() => {
    let ignore = false;
    let source: EventSource | undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;

    async function refetch() {
      try {
        const result = await getDownloads();

        if (!ignore) {
          setDownloads(result);
          setError(undefined);
        }
      } catch (err) {
        if (!ignore) {
          setError((err as Error).message);
        }
      }
    }

    function connect() {
      source = new EventSource('/api/downloads/events');
      source.onmessage = (event) => {
        if (!ignore) {
          setDownloads(JSON.parse(event.data));
          setError(undefined);
        }
      };
      source.onerror = () => {
        source?.close();

        if (!ignore) {
          timer = setTimeout(connect, retryWait);
          refetch();
        }
      };
    }

    refetch();
    connect();

    return () => {
      ignore = true;
      source?.close();
      clearTimeout(timer);
    };
  }, []);

  useEffect(() => {
    let ignore = false;

    getQuarantine()
      .then((result) => {
        if (!ignore) {
          setQuarantine(result);
        }
      })
      .catch(() => undefined);

    return () => {
      ignore = true;
    };
  }, [failedCount]);

  return (
    <>
      <Header />

      <main className="page">
        <Link to="/" className="back">
          <ArrowLeftIcon />
          {t.common.back}
        </Link>

        <h1 className="page-title">{t.downloads.title}</h1>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        {!downloads && !error && (
          <p className="status" role="status">
            {t.downloads.loading}
          </p>
        )}
        {downloads && downloads.length === 0 && <p className="status hint">{t.downloads.empty}</p>}
        {downloads && downloads.length > 0 && (
          <ul className="artist-list">
            {downloads.map((download) => (
              <DownloadItem
                key={download.id}
                download={download}
                onCancelled={() =>
                  setDownloads((current) =>
                    current && current.map((other) =>
                      other.id === download.id ? { ...other, state: 'cancelled' } : other))}
                onRetried={() =>
                  setDownloads((current) =>
                    current && current.map((other) =>
                      other.id === download.id
                        ? { ...other, state: 'waiting', nextAttemptAt: new Date().toISOString() }
                        : other))}
              />
            ))}
          </ul>
        )}
        {quarantine.length > 0 && (
          <>
            <h2 className="section-title">{t.quarantine.title}</h2>
            <p className="status hint">{t.quarantine.hint}</p>
            <ul className="artist-list">
              {quarantine.map((album) => (
                <QuarantineItem
                  key={album.id}
                  album={album}
                  onGone={() =>
                    setQuarantine((current) => current.filter((other) => other.id !== album.id))}
                />
              ))}
            </ul>
          </>
        )}
      </main>
    </>
  );
}
