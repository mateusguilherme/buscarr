import { describe, expect, it } from 'vitest';
import {
  findArtists,
  getArtist,
  getArtistReleases,
  getCurrentUser,
  getSettings,
  login,
  register,
  saveSettings,
} from './api';
import { mockFetch } from './test-utils';

describe('getCurrentUser', () => {
  it('devolve null quando não há sessão', async () => {
    mockFetch({ status: 401 });

    expect(await getCurrentUser()).toBeNull();
  });

  it('devolve o usuário logado', async () => {
    mockFetch({ status: 200, body: { email: 'fulano@exemplo.com', name: 'Fulano' } });

    expect(await getCurrentUser()).toEqual({ email: 'fulano@exemplo.com', name: 'Fulano' });
  });
});

describe('login', () => {
  it('envia e-mail e senha como JSON', async () => {
    const fetchMock = mockFetch({ status: 200, body: { email: 'fulano@exemplo.com', name: 'Fulano' } });

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
  it('avisa quando o e-mail já existe', async () => {
    mockFetch({ status: 409 });

    await expect(register('fulano@exemplo.com', 'Fulano', 'segredo123')).rejects.toThrow(
      'Já existe um usuário com esse e-mail.',
    );
  });

  it('avisa quando faltam dados ou a senha é curta', async () => {
    mockFetch({ status: 400 });

    await expect(register('fulano@exemplo.com', 'Fulano', 'curta')).rejects.toThrow(
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

describe('getSettings', () => {
  it('pede as configurações do usuário', async () => {
    const fetchMock = mockFetch({ status: 200, body: { edition: 'deluxe' } });

    expect(await getSettings()).toEqual({ edition: 'deluxe' });
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

    await saveSettings({ edition: 'remastered' });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/settings');
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ edition: 'remastered' }),
    });
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 400 });

    await expect(saveSettings({ edition: 'remastered' })).rejects.toThrow('Erro 400 ao salvar as configurações.');
  });
});

describe('getArtist', () => {
  it('pede o artista pelo id', async () => {
    const nirvana = { id: 'abc-123', name: 'Nirvana', disambiguation: null, area: null };
    const fetchMock = mockFetch({ status: 200, body: nirvana });

    expect(await getArtist('abc-123')).toEqual(nirvana);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists/abc-123');
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 502 });

    await expect(getArtist('abc-123')).rejects.toThrow('Erro 502 ao carregar o artista.');
  });
});

describe('getArtistReleases', () => {
  it('pede todos os releases do artista', async () => {
    const fetchMock = mockFetch({ status: 200, body: [] });

    await getArtistReleases('abc-123');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists/abc-123/releases');
  });

  it('lança erro quando a API falha', async () => {
    mockFetch({ status: 502 });

    await expect(getArtistReleases('abc-123')).rejects.toThrow('Erro 502 ao carregar os releases.');
  });
});
