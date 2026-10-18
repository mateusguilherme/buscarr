import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { getDownloads, type Download } from './api';
import { DownloadItem } from './DownloadItem';
import { Header } from './Header';
import { ArrowLeftIcon } from './icons';
import { t } from './i18n';

const retryWait = 10_000;

export function DownloadsPage() {
  const [downloads, setDownloads] = useState<Download[] | null>(null);
  const [error, setError] = useState<string>();

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
      </main>
    </>
  );
}
