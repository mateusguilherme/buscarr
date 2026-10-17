import { t } from './i18n';

export type User = { email: string; name: string };
export type Artist = { id: string; name: string; disambiguation: string | null; area: string | null };
export type Release = { title: string; date: string; live: boolean };
export type ArtistReleases = { name: string; releases: Release[] };

export async function getCurrentUser(): Promise<User | null> {
  const res = await fetch('/api/auth/me');

  if (res.status === 401) {
    return null;
  }

  if (!res.ok) {
    throw new Error(t.api.loadUserError(res.status));
  }

  return res.json();
}

export async function login(email: string, password: string): Promise<User> {
  const res = await postJson('/api/auth/login', { email, password });

  if (res.status === 401) {
    throw new Error(t.api.wrongCredentials);
  }

  if (!res.ok) {
    throw new Error(t.api.loginError(res.status));
  }

  return res.json();
}

export async function register(email: string, name: string, password: string): Promise<User> {
  const res = await postJson('/api/auth/register', { email, name, password });

  if (res.status === 409) {
    throw new Error(t.api.emailTaken);
  }

  if (res.status === 400) {
    throw new Error(t.api.invalidRegistration);
  }

  if (!res.ok) {
    throw new Error(t.api.registerError(res.status));
  }

  return res.json();
}

export async function logout(): Promise<void> {
  const res = await fetch('/api/auth/logout', { method: 'POST' });

  if (!res.ok) {
    throw new Error(t.api.logoutError(res.status));
  }
}

function postJson(url: string, body: unknown): Promise<Response> {
  return fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export async function findArtists(query: string, country?: string, type?: string): Promise<Artist[]> {
  const countryParam = country ? `&country=${encodeURIComponent(country)}` : '';
  const typeParam = type ? `&type=${encodeURIComponent(type)}` : '';
  const res = await fetch(`/api/artists?q=${encodeURIComponent(query)}${countryParam}${typeParam}`);

  if (!res.ok) {
    throw new Error(t.api.searchError(res.status));
  }

  return res.json();
}

export async function getArtistReleases(id: string, type: string): Promise<ArtistReleases> {
  const res = await fetch(
    `/api/artists/${encodeURIComponent(id)}/releases?type=${encodeURIComponent(type)}`,
  );

  if (!res.ok) {
    throw new Error(t.api.releasesError(res.status));
  }

  return res.json();
}
