import { t } from './i18n';

export type User = { email: string };
export type Artist = {
  id: string;
  name: string;
  disambiguation: string | null;
  area: string | null;
  image: string | null;
  listed: boolean;
};
export type Edition = 'original' | 'remastered' | 'deluxe';
export type Quality = 'mp3-320' | 'flac';
export type Settings = { edition: Edition; quality: Quality };
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

export async function register(email: string, password: string): Promise<User> {
  const res = await postJson('/api/auth/register', { email, password });

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

export type ServerSettings = {
  slskdUrl: string;
  hasSlskdApiKey: boolean;
  downloadsPath: string;
  libraryPath: string;
  rejectedPath: string;
  subsonicUrl: string;
  subsonicUser: string;
  hasSubsonicPassword: boolean;
  hasFanartTvApiKey: boolean;
  spotifyClientId: string;
  hasSpotifyClientSecret: boolean;
};

export type ServerSettingsUpdate = {
  slskdUrl: string;
  slskdApiKey: string | null;
  downloadsPath: string;
  libraryPath: string;
  rejectedPath: string;
  subsonicUrl: string;
  subsonicUser: string;
  subsonicPassword: string | null;
  fanartTvApiKey: string | null;
  spotifyClientId: string;
  spotifyClientSecret: string | null;
};

export type ConnectionState = 'ok' | 'not-configured' | 'offline' | 'error';
export type ConnectionStatus = { state: ConnectionState; message: string | null };
export type ServerStatus = {
  slskd: ConnectionStatus;
  subsonic: ConnectionStatus;
  fanartTv: ConnectionStatus;
  spotify: ConnectionStatus;
};

export class ServerSettingsError extends Error {
  readonly errors: Record<string, string>;

  constructor(errors: Record<string, string>) {
    super(t.api.saveServerSettingsError(400));
    this.errors = errors;
  }
}

export async function getServerSettings(): Promise<ServerSettings | null> {
  const res = await fetch('/api/server-settings');

  if (res.status === 403) {
    return null;
  }

  if (!res.ok) {
    throw new Error(t.api.serverSettingsError(res.status));
  }

  return res.json();
}

export async function saveServerSettings(update: ServerSettingsUpdate): Promise<void> {
  const res = await sendJson('PUT', '/api/server-settings', update);

  if (res.status === 400) {
    const body = await res.json();
    throw new ServerSettingsError(body.errors ?? {});
  }

  if (!res.ok) {
    throw new Error(t.api.saveServerSettingsError(res.status));
  }
}

export async function getServerStatus(): Promise<ServerStatus> {
  const res = await fetch('/api/server-settings/status');

  if (!res.ok) {
    throw new Error(t.api.serverStatusError(res.status));
  }

  return res.json();
}

export type Download = {
  id: number;
  artistName: string;
  credit: string;
  title: string;
  type: string;
  coverUrl: string | null;
  trackCount: number;
  quality: string;
  state: string;
  failure: string | null;
  attempts: number;
  nextAttemptAt: string;
  tracksDone: number;
  bytesTransferred: number;
  bytesTotal: number;
  placeInQueue: number | null;
  username: string | null;
  firstReleaseDate: string | null;
  acceptedQuality: string | null;
};

export type SourceCandidate = {
  id: number;
  username: string;
  folder: string;
  fileCount: number;
  format: string;
  bitRate: number | null;
  hasFreeUploadSlot: boolean;
  queueLength: number;
  rejection: string | null;
};

export type ArtistRelease = {
  releaseGroupId: string;
  title: string;
  type: string;
  firstReleaseDate: string | null;
  coverUrl: string | null;
  trackCount: number;
  requested: boolean;
  inLibrary: boolean;
};

export async function getCandidates(id: number): Promise<SourceCandidate[]> {
  const res = await fetch(`/api/downloads/${id}/candidates`);

  if (!res.ok) {
    throw new Error(t.api.candidatesError(res.status));
  }

  return res.json();
}

export async function pickCandidate(id: number, candidateId: number): Promise<void> {
  const res = await sendJson('POST', `/api/downloads/${id}/pick`, { candidateId });

  if (!res.ok) {
    throw new Error(t.api.pickCandidateError(res.status));
  }
}

export async function getArtistReleases(id: string): Promise<ArtistRelease[]> {
  const res = await fetch(`/api/artists/${encodeURIComponent(id)}/releases`);

  if (!res.ok) {
    throw new Error(t.api.releasesError(res.status));
  }

  return res.json();
}

export async function requestAlbum(artistId: string, releaseGroupId: string): Promise<void> {
  const res = await sendJson('POST', '/api/downloads', { artistId, releaseGroupId });

  if (!res.ok) {
    throw new Error(t.api.requestAlbumError(res.status));
  }
}

export async function getDownloads(): Promise<Download[]> {
  const res = await fetch('/api/downloads');

  if (!res.ok) {
    throw new Error(t.api.downloadsError(res.status));
  }

  return res.json();
}

export async function cancelDownload(id: number): Promise<void> {
  const res = await fetch(`/api/downloads/${id}/cancel`, { method: 'POST' });

  if (!res.ok) {
    throw new Error(t.api.cancelDownloadError(res.status));
  }
}

export async function retryDownload(id: number): Promise<void> {
  const res = await fetch(`/api/downloads/${id}/retry`, { method: 'POST' });

  if (!res.ok) {
    throw new Error(t.api.retryDownloadError(res.status));
  }
}

export type QuarantinedAlbum = {
  id: number;
  artistName: string;
  title: string;
  reason: string;
  username: string | null;
  at: string;
  canImport: boolean;
};

export async function getQuarantine(): Promise<QuarantinedAlbum[]> {
  const res = await fetch('/api/quarantine');

  if (!res.ok) {
    throw new Error(t.api.quarantineError(res.status));
  }

  return res.json();
}

export async function importQuarantined(id: number): Promise<void> {
  const res = await fetch(`/api/quarantine/${id}/import`, { method: 'POST' });

  if (!res.ok) {
    throw new Error(t.api.importQuarantinedError(res.status));
  }
}

export async function discardQuarantined(id: number): Promise<void> {
  const res = await fetch(`/api/quarantine/${id}`, { method: 'DELETE' });

  if (!res.ok) {
    throw new Error(t.api.discardQuarantinedError(res.status));
  }
}

export async function findArtists(query: string): Promise<Artist[]> {
  const res = await fetch(`/api/artists?q=${encodeURIComponent(query)}`);

  if (!res.ok) {
    throw new Error(t.api.searchError(res.status));
  }

  return res.json();
}
