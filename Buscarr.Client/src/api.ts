import { t } from './i18n';

export type User = { email: string; name: string };
export type Artist = {
  id: string;
  name: string;
  disambiguation: string | null;
  area: string | null;
  image: string | null;
  listed: boolean;
};
export type Edition = 'original' | 'remastered' | 'deluxe';
export type Settings = { edition: Edition };
export type ReleaseType = 'album' | 'ep' | 'single' | 'live' | 'compilation' | 'soundtrack' | 'remix' | 'demo';
export type ListSettings = { releaseTypes: ReleaseType[] };

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

export async function getList(): Promise<Artist[]> {
  const res = await fetch('/api/list');

  if (!res.ok) {
    throw new Error(t.api.listError(res.status));
  }

  return res.json();
}

export async function getListEntry(id: string): Promise<ListSettings> {
  const res = await fetch(`/api/list/${encodeURIComponent(id)}`);

  if (!res.ok) {
    throw new Error(t.api.listError(res.status));
  }

  return res.json();
}

export async function addToList(id: string, settings: ListSettings): Promise<void> {
  const res = await sendJson('PUT', `/api/list/${encodeURIComponent(id)}`, settings);

  if (!res.ok) {
    throw new Error(t.api.addToListError(res.status));
  }
}

export async function removeFromList(id: string): Promise<void> {
  const res = await fetch(`/api/list/${encodeURIComponent(id)}`, { method: 'DELETE' });

  if (!res.ok) {
    throw new Error(t.api.removeFromListError(res.status));
  }
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
