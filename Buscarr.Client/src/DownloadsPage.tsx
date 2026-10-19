import { useEffect, useState } from 'react';
import { getDownloads, getQuarantine, type Download, type QuarantinedAlbum } from './api';
import { BackLink } from './BackLink';
import { awaitingRelease, DownloadItem } from './DownloadItem';
import { Header } from './Header';
import { QuarantineItem } from './QuarantineItem';
import { t } from './i18n';

const retryWait = 10_000;

type Filter = 'active' | 'scheduled' | 'done' | 'quarantine';

const filters: Exclude<Filter, 'quarantine'>[] = ['active', 'scheduled', 'done'];

function filterOf(download: Download): Filter {
  if (['imported', 'downloaded', 'cancelled'].includes(download.state)) {
    return 'done';
  }

  return awaitingRelease(download) ? 'scheduled' : 'active';
}

export function DownloadsPage() {
  const [downloads, setDownloads] = useState<Download[] | null>(null);
  const [quarantine, setQuarantine] = useState<QuarantinedAlbum[]>([]);
  const [filter, setFilter] = useState<Filter | undefined>('active');
  const [error, setError] = useState<string>();
  const failedCount = downloads?.filter((download) => download.state === 'failed').length ?? 0;
  const shown = downloads?.filter((download) => !filter || filterOf(download) === filter);

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
      <Header title={t.downloads.title} />

      <main className="page">
        <BackLink />

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
        {downloads && downloads.length === 0 && quarantine.length === 0 && (
          <p className="status hint">{t.downloads.empty}</p>
        )}
        {((downloads && downloads.length > 0) || quarantine.length > 0) && (
          <div className="filters">
            {downloads && downloads.length > 0 && filters.map((value) => (
              <button
                key={value}
                className={value === filter ? 'button-primary button-small' : 'button-ghost button-small'}
                aria-pressed={value === filter}
                onClick={() => setFilter((current) => (current === value ? undefined : value))}
              >
                {t.downloads.filters[value]}
              </button>
            ))}
            {quarantine.length > 0 && (
              <button
                className={filter === 'quarantine' ? 'button-primary button-small' : 'button-ghost button-small'}
                aria-pressed={filter === 'quarantine'}
                onClick={() => setFilter((current) => (current === 'quarantine' ? undefined : 'quarantine'))}
              >
                {t.quarantine.title}
              </button>
            )}
          </div>
        )}
        {filter !== 'quarantine' && shown && shown.length > 0 && (
          <ul className="artist-list">
            {shown.map((download) => (
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
        {filter === 'quarantine' && quarantine.length > 0 && (
          <>
            <p className="status hint">{t.quarantine.hint}</p>
            <ul className="artist-list">
              {quarantine.map((album) => (
                <QuarantineItem
                  key={album.id}
                  album={album}
                  onGone={() => {
                    setQuarantine((current) => current.filter((other) => other.id !== album.id));

                    if (quarantine.length === 1) {
                      setFilter('active');
                    }
                  }}
                />
              ))}
            </ul>
          </>
        )}
      </main>
    </>
  );
}
