export type User = { email: string; name: string };
export type Artist = { id: string; name: string };
export type Release = { title: string; date: string; live: boolean };
export type ArtistReleases = { name: string; releases: Release[] };

export async function getCurrentUser(): Promise<User | null> {
  const res = await fetch('/api/auth/me');

  if (res.status === 401) {
    return null;
  }

  if (!res.ok) {
    throw new Error(`Erro ${res.status} ao carregar o usuário.`);
  }

  return res.json();
}

export async function login(email: string, password: string): Promise<User> {
  const res = await postJson('/api/auth/login', { email, password });

  if (res.status === 401) {
    throw new Error('E-mail ou senha incorretos.');
  }

  if (!res.ok) {
    throw new Error(`Erro ${res.status} ao entrar.`);
  }

  return res.json();
}

export async function register(email: string, name: string, password: string): Promise<User> {
  const res = await postJson('/api/auth/register', { email, name, password });

  if (res.status === 409) {
    throw new Error('Já existe um usuário com esse e-mail.');
  }

  if (res.status === 400) {
    throw new Error('Preencha todos os campos. A senha precisa ter pelo menos 8 caracteres.');
  }

  if (!res.ok) {
    throw new Error(`Erro ${res.status} ao criar o usuário.`);
  }

  return res.json();
}

export async function logout(): Promise<void> {
  const res = await fetch('/api/auth/logout', { method: 'POST' });

  if (!res.ok) {
    throw new Error(`Erro ${res.status} ao sair.`);
  }
}

function postJson(url: string, body: unknown): Promise<Response> {
  return fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export async function findArtist(query: string): Promise<Artist | null> {
  const res = await fetch(`/api/artists?q=${encodeURIComponent(query)}`);

  if (res.status === 404) {
    return null;
  }

  if (!res.ok) {
    throw new Error(`Erro ${res.status} na busca.`);
  }

  return res.json();
}

export async function getArtistReleases(id: string, type: string): Promise<ArtistReleases> {
  const res = await fetch(
    `/api/artists/${encodeURIComponent(id)}/releases?type=${encodeURIComponent(type)}`,
  );

  if (!res.ok) {
    throw new Error(`Erro ${res.status} ao carregar os releases.`);
  }

  return res.json();
}
