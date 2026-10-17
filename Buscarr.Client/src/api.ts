import { t } from './i18n';

export type User = { email: string; name: string };
export type Artist = {
  id: string;
  name: string;
  disambiguation: string | null;
  area: string | null;
  image: string | null;
};
export type Release = {
  title: string;
  date: string;
  live: boolean;
  type: string | null;
  cover: string | null;
  tracks: number;
};
export type Edition = 'original' | 'remastered' | 'deluxe';
export type Settings = { edition: Edition };

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
  return sendJson('POST', url, body);
}

function sendJson(method: string, url: string, body: unknown): Promise<Response> {
  return fetch(url, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export async function getSettings(): Promise<Settings> {
  const res = await fetch('/api/settings');

  if (!res.ok) {
    throw new Error(t.api.settingsError(res.status));
  }

  return res.json();
}

export async function saveSettings(settings: Settings): Promise<void> {
  const res = await sendJson('PUT', '/api/settings', settings);

  if (!res.ok) {
    throw new Error(t.api.saveSettingsError(res.status));
  }
}

export async function findArtists(query: string): Promise<Artist[]> {
  const res = await fetch(`/api/artists?q=${encodeURIComponent(query)}`);

  if (!res.ok) {
    throw new Error(t.api.searchError(res.status));
  }

  return res.json();
}

export async function getArtist(id: string): Promise<Artist> {
  const res = await fetch(`/api/artists/${encodeURIComponent(id)}`);

  if (!res.ok) {
    throw new Error(t.api.artistError(res.status));
  }

  return res.json();
}

export async function getArtistReleases(id: string): Promise<Release[]> {
  const res = await fetch(`/api/artists/${encodeURIComponent(id)}/releases`);

  if (!res.ok) {
    throw new Error(t.api.releasesError(res.status));
  }

  return res.json();
}
