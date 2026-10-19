import { describe, expect, it } from 'vitest';
import {
  addToList,
  findArtists,
  getCurrentUser,
  getList,
  getListEntry,
  getServerSettings,
  getServerStatus,
  getSettings,
  login,
  register,
  removeFromList,
  saveServerSettings,
  saveSettings,
  ServerSettingsError,
} from './api';
import { mockFetch } from './test-utils';

describe('getCurrentUser', () => {
  it('devolve null quando não há sessão', async () => {
    mockFetch({ status: 401 });

    expect(await getCurrentUser()).toBeNull();
  });

  it('devolve o usuário logado', async () => {
    mockFetch({ status: 200, body: { email: 'fulano@exemplo.com' } });

    expect(await getCurrentUser()).toEqual({ email: 'fulano@exemplo.com' });
  });
});

describe('login', () => {
  it('envia e-mail e senha como JSON', async () => {
    const fetchMock = mockFetch({ status: 200, body: { email: 'fulano@exemplo.com' } });

    await login('fulano@exemplo.com', 'segredo123');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/auth/login');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({ email: 'fulano@exemplo.com', password: 'segredo123' });
  });

  it('avisa quando o e-mail ou a senha estão errados', async () => {
    mockFetch({ status: 401 });

    await expect(login('fulano@exemplo.com', 'errada123')).rejects.toThrow('E-mail ou senha incorretos.');
  });
});

describe('register', () => {
  it('envia só e-mail e senha como JSON', async () => {
    const fetchMock = mockFetch({ status: 200, body: { email: 'fulano@exemplo.com' } });

    await register('fulano@exemplo.com', 'segredo123');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/auth/register');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({ email: 'fulano@exemplo.com', password: 'segredo123' });
  });

  it('avisa quando o e-mail já existe', async () => {
    mockFetch({ status: 409 });

    await expect(register('fulano@exemplo.com', 'segredo123')).rejects.toThrow(
      'Já existe um usuário com esse e-mail.',
    );
  });

  it('avisa quando faltam dados ou a senha é curta', async () => {
    mockFetch({ status: 400 });

    await expect(register('fulano@exemplo.com', 'curta')).rejects.toThrow(
      'Preencha todos os campos. A senha precisa ter pelo menos 8 caracteres.',
    );
  });
});

describe('findArtists', () => {
  it('codifica a busca na URL', async () => {
    const fetchMock = mockFetch({ status: 200, body: [] });

    await findArtists('sigur rós');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists?q=sigur%20r%C3%B3s');
  });

  it('devolve os candidatos encontrados', async () => {
    const rancore = { id: '1', name: 'Rancore', disambiguation: 'Brazillian Rock Band', area: 'São Paulo' };
    mockFetch({ status: 200, body: [rancore] });

    expect(await findArtists('rancore')).toEqual([rancore]);
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 502 });

    await expect(findArtists('rancore')).rejects.toThrow('Erro 502 na busca.');
  });
});

describe('getList', () => {
  it('pede a lista de artistas do usuário', async () => {
    const fetchMock = mockFetch({ status: 200, body: [] });

    expect(await getList()).toEqual([]);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/list');
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 500 });

    await expect(getList()).rejects.toThrow('Erro 500 ao carregar a lista.');
  });
});

describe('addToList', () => {
  it('põe o artista na lista', async () => {
    const fetchMock = mockFetch({ status: 204 });

    await addToList('abc-123', { releaseTypes: ['album', 'live'] });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/list/abc-123');
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ releaseTypes: ['album', 'live'] }),
    });
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 502 });

    await expect(addToList('abc-123', { releaseTypes: ['album'] })).rejects.toThrow('Erro 502 ao adicionar à lista.');
  });
});

describe('getListEntry', () => {
  it('pede como o artista está na lista', async () => {
    const fetchMock = mockFetch({ status: 200, body: { releaseTypes: ['single'] } });

    expect(await getListEntry('abc-123')).toEqual({ releaseTypes: ['single'] });
    expect(fetchMock.mock.calls[0][0]).toBe('/api/list/abc-123');
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 404 });

    await expect(getListEntry('abc-123')).rejects.toThrow('Erro 404 ao carregar a lista.');
  });
});

describe('removeFromList', () => {
  it('tira o artista da lista', async () => {
    const fetchMock = mockFetch({ status: 204 });

    await removeFromList('abc-123');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/list/abc-123');
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'DELETE' });
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 500 });

    await expect(removeFromList('abc-123')).rejects.toThrow('Erro 500 ao tirar da lista.');
  });
});

describe('getSettings', () => {
  it('pede as configurações do usuário', async () => {
    const fetchMock = mockFetch({ status: 200, body: { edition: 'deluxe', quality: 'flac' } });

    expect(await getSettings()).toEqual({ edition: 'deluxe', quality: 'flac' });
    expect(fetchMock.mock.calls[0][0]).toBe('/api/settings');
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 500 });

    await expect(getSettings()).rejects.toThrow('Erro 500 ao carregar as configurações.');
  });
});

describe('saveSettings', () => {
  it('envia as configurações do usuário', async () => {
    const fetchMock = mockFetch({ status: 204 });

    await saveSettings({ edition: 'remastered', quality: 'flac' });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/settings');
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ edition: 'remastered', quality: 'flac' }),
    });
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 400 });

    await expect(saveSettings({ edition: 'remastered', quality: 'mp3-320' })).rejects.toThrow(
      'Erro 400 ao salvar as configurações.',
    );
  });
});

const serverSettings = {
  slskdUrl: 'http://slskd:5030',
  hasSlskdApiKey: true,
  downloadsPath: '/downloads/complete',
  libraryPath: '/music',
  rejectedPath: '',
  subsonicUrl: '',
  subsonicUser: '',
  hasSubsonicPassword: false,
  hasFanartTvApiKey: false,
  spotifyClientId: '',
  hasSpotifyClientSecret: false,
};

const serverUpdate = {
  slskdUrl: 'http://slskd:5030',
  slskdApiKey: null,
  downloadsPath: '/downloads/complete',
  libraryPath: '/music',
  rejectedPath: '',
  subsonicUrl: '',
  subsonicUser: '',
  subsonicPassword: null,
  fanartTvApiKey: null,
  spotifyClientId: '',
  spotifyClientSecret: null,
};

describe('getServerSettings', () => {
  it('pede as configurações do servidor', async () => {
    const fetchMock = mockFetch({ status: 200, body: serverSettings });

    expect(await getServerSettings()).toEqual(serverSettings);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/server-settings');
  });

  it('devolve null para quem não é admin', async () => {
    mockFetch({ status: 403 });

    expect(await getServerSettings()).toBeNull();
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 500 });

    await expect(getServerSettings()).rejects.toThrow('Erro 500 ao carregar as configurações do servidor.');
  });
});

describe('saveServerSettings', () => {
  it('envia as configurações do servidor', async () => {
    const fetchMock = mockFetch({ status: 204 });

    await saveServerSettings(serverUpdate);

    expect(fetchMock.mock.calls[0][0]).toBe('/api/server-settings');
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: 'PUT', body: JSON.stringify(serverUpdate) });
  });

  it('lança os erros de cada campo quando a API recusa', async () => {
    mockFetch({ status: 400, body: { errors: { libraryPath: 'not-found' } } });

    const error = await saveServerSettings(serverUpdate).catch((err: unknown) => err);

    expect(error).toBeInstanceOf(ServerSettingsError);
    expect((error as ServerSettingsError).errors).toEqual({ libraryPath: 'not-found' });
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 500 });

    await expect(saveServerSettings(serverUpdate)).rejects.toThrow('Erro 500 ao salvar as configurações do servidor.');
  });
});

describe('getServerStatus', () => {
  it('pede o estado das conexões', async () => {
    const status = { slskd: { state: 'ok', message: null }, subsonic: { state: 'not-configured', message: null } };
    const fetchMock = mockFetch({ status: 200, body: status });

    expect(await getServerStatus()).toEqual(status);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/server-settings/status');
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 500 });

    await expect(getServerStatus()).rejects.toThrow('Erro 500 ao testar as conexões.');
  });
});
