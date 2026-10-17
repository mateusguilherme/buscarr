import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { findArtist, logout, type Artist, type User } from './api';
import { countries } from './countries';

export function SearchPage({ user, onLogout }: { user: User; onLogout: () => void }) {
  const [query, setQuery] = useState('');
  const [country, setCountry] = useState('');
  const [artist, setArtist] = useState<Artist | null>(null);
  const [message, setMessage] = useState('');

  async function handleSearch(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setArtist(null);
    setMessage('Buscando…');

    try {
      const found = await findArtist(query, country);
      setArtist(found);
      setMessage(found ? '' : 'Nenhum artista encontrado.');
    } catch {
      setMessage('Erro na busca.');
    }
  }

  async function handleLogout() {
    try {
      await logout();
      onLogout();
    } catch (err) {
      setMessage((err as Error).message);
    }
  }

  return (
    <>
      <p>
        Olá, {user.name} <button onClick={handleLogout}>Sair</button>
      </p>

      <form onSubmit={handleSearch}>
        <p>
          <input
            placeholder="Nome do artista"
            required
            autoFocus
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />{' '}
          <select value={country} onChange={(e) => setCountry(e.target.value)}>
            <option value="">Todos os países</option>
            {countries.map(({ code, name }) => (
              <option key={code} value={code}>
                {name}
              </option>
            ))}
          </select>{' '}
          <button>Buscar</button>
        </p>
      </form>

      {message && <p>{message}</p>}
      {artist && (
        <Link to={`/artist/${artist.id}`}>
          <h2>{artist.name}</h2>
        </Link>
      )}
    </>
  );
}
