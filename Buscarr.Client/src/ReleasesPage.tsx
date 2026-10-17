import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import { getArtistReleases, type ArtistReleases, type Release } from './api';

const releaseTypes = [
  { value: 'album', label: 'Álbuns' },
  { value: 'ep', label: 'EPs' },
  { value: 'single', label: 'Singles' },
  { value: 'all', label: 'Todos' },
];

export function ReleasesPage() {
  const { id = '' } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const type = searchParams.get('type') ?? 'album';
  const [data, setData] = useState<ArtistReleases | null>(null);
  const [error, setError] = useState<string>();

  useEffect(() => {
    let ignore = false;

    async function loadReleases() {
      setData(null);
      setError(undefined);

      try {
        const result = await getArtistReleases(id, type);

        if (!ignore) {
          setData(result);
        }
      } catch (err) {
        if (!ignore) {
          setError((err as Error).message);
        }
      }
    }

    loadReleases();

    return () => {
      ignore = true;
    };
  }, [id, type]);

  return (
    <>
      <p>
        <Link to="/">Voltar</Link>
      </p>
      {data && <h1>{data.name}</h1>}
      <select value={type} onChange={(e) => setSearchParams({ type: e.target.value })}>
        {releaseTypes.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>

      {error && <p>{error}</p>}
      {!data && !error && <p>Carregando…</p>}
      {data && data.releases.length === 0 && <p>Nenhum release oficial encontrado.</p>}
      {data && (
        <>
          <ReleaseSection title="Estúdio" releases={data.releases.filter((r) => !r.live)} />
          <ReleaseSection title="Ao vivo" releases={data.releases.filter((r) => r.live)} />
        </>
      )}
    </>
  );
}

function ReleaseSection({ title, releases }: { title: string; releases: Release[] }) {
  if (releases.length === 0) {
    return null;
  }

  return (
    <>
      <h2>{title}</h2>
      <ul>
        {releases.map((release) => (
          <li key={release.title}>
            {release.date} · {release.title}
          </li>
        ))}
      </ul>
    </>
  );
}
