import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { ServerSettings } from './api';
import { ServerSettingsForm } from './ServerSettingsForm';
import { mockFetch } from './test-utils';

const saved: ServerSettings = {
  slskdUrl: 'http://slskd:5030',
  hasSlskdApiKey: true,
  downloadsPath: '/downloads/complete',
  libraryPath: '/music',
  rejectedPath: '',
  subsonicUrl: 'http://navidrome:4533',
  subsonicUser: 'fulano',
  hasSubsonicPassword: false,
  hasFanartTvApiKey: true,
  spotifyClientId: 'id-spotify',
  hasSpotifyClientSecret: true,
};

const statusOk = {
  slskd: { state: 'ok', message: null },
  subsonic: { state: 'ok', message: null },
  fanartTv: { state: 'ok', message: null },
  spotify: { state: 'ok', message: null },
};

function field(label: string) {
  return screen.getByLabelText(label) as HTMLInputElement;
}

function sentBody(fetchMock: ReturnType<typeof mockFetch>, call: number) {
  return JSON.parse(fetchMock.mock.calls[call][1]?.body as string);
}

describe('ServerSettingsForm', () => {
  it('mostra os valores salvos sem revelar as chaves', () => {
    render(<ServerSettingsForm initial={saved} />);

    expect(field('Endereço do slskd').value).toBe('http://slskd:5030');
    expect(field('Pasta de downloads concluídos').value).toBe('/downloads/complete');
    expect(field('Biblioteca de música').value).toBe('/music');
    expect(field('Usuário').value).toBe('fulano');
    expect(field('Chave de API do slskd').value).toBe('');
    expect(field('Chave de API do slskd').placeholder).toBe('Salva. Deixe em branco para manter.');
    expect(field('Senha').placeholder).toBe('');
    expect(field('Client ID do Spotify').value).toBe('id-spotify');
    expect(field('Client secret do Spotify').placeholder).toBe('Salva. Deixe em branco para manter.');
  });

  it('salva mantendo as chaves que não foram digitadas e testa as conexões', async () => {
    const fetchMock = mockFetch({ status: 204 }, { status: 200, body: statusOk });
    render(<ServerSettingsForm initial={saved} />);

    await userEvent.clear(field('Biblioteca de música'));
    await userEvent.type(field('Biblioteca de música'), '/srv/music');
    await userEvent.type(field('Senha'), 'segredo');
    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));

    expect(await screen.findByText('Salvo.')).toBeDefined();
    expect(fetchMock.mock.calls[0][0]).toBe('/api/server-settings');
    expect(sentBody(fetchMock, 0)).toEqual({
      slskdUrl: 'http://slskd:5030',
      slskdApiKey: null,
      downloadsPath: '/downloads/complete',
      libraryPath: '/srv/music',
      rejectedPath: '',
      subsonicUrl: 'http://navidrome:4533',
      subsonicUser: 'fulano',
      subsonicPassword: 'segredo',
      fanartTvApiKey: null,
      spotifyClientId: 'id-spotify',
      spotifyClientSecret: null,
    });
    expect(await screen.findByText('slskd: conectado ao Soulseek.')).toBeDefined();
    expect(field('Senha').value).toBe('');
    expect(field('Senha').placeholder).toBe('Salva. Deixe em branco para manter.');
  });

  it('apaga uma chave salva', async () => {
    const fetchMock = mockFetch({ status: 204 }, { status: 200, body: statusOk });
    render(<ServerSettingsForm initial={saved} />);

    await userEvent.click(screen.getByRole('button', { name: 'Apagar a chave do fanart.tv' }));
    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));

    expect(await screen.findByText('Salvo.')).toBeDefined();
    expect(sentBody(fetchMock, 0).fanartTvApiKey).toBe('');
    expect(screen.queryByRole('button', { name: 'Apagar a chave do fanart.tv' })).toBeNull();
  });

  it('mostra o erro de cada campo recusado', async () => {
    mockFetch({ status: 400, body: { errors: { libraryPath: 'not-found', slskdUrl: 'invalid-url' } } });
    render(<ServerSettingsForm initial={saved} />);

    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));

    expect(await screen.findByText('Pasta não encontrada.')).toBeDefined();
    expect(screen.getByText('Endereço inválido. Use http:// ou https://.')).toBeDefined();
    expect(screen.queryByText('Salvo.')).toBeNull();
  });

  it('avisa quando não consegue salvar', async () => {
    mockFetch({ status: 500 });
    render(<ServerSettingsForm initial={saved} />);

    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));

    expect((await screen.findByRole('alert')).textContent).toBe('Erro 500 ao salvar as configurações do servidor.');
  });

  it('testa as conexões e mostra o estado de cada uma', async () => {
    const fetchMock = mockFetch({
      status: 200,
      body: {
        slskd: { state: 'offline', message: null },
        subsonic: { state: 'error', message: 'Wrong username or password' },
        fanartTv: { state: 'error', message: 'chave inválida' },
        spotify: { state: 'error', message: 'credenciais inválidas' },
      },
    });
    render(<ServerSettingsForm initial={saved} />);

    await userEvent.click(screen.getByRole('button', { name: 'Testar conexões' }));

    expect(fetchMock.mock.calls[0][0]).toBe('/api/server-settings/status');
    expect(await screen.findByText('slskd: responde, mas não está conectado ao Soulseek.')).toBeDefined();
    expect(screen.getByText('Servidor de música: erro (Wrong username or password).')).toBeDefined();
    expect(screen.getByText('fanart.tv: erro (chave inválida).')).toBeDefined();
    expect(screen.getByText('Spotify: erro (credenciais inválidas).')).toBeDefined();
  });

  it('desabilita o testar enquanto há mudanças não salvas', async () => {
    render(<ServerSettingsForm initial={saved} />);

    await userEvent.type(field('Senha'), 'segredo');

    const test = screen.getByRole('button', { name: 'Testar conexões' }) as HTMLButtonElement;
    expect(test.disabled).toBe(true);
    expect(screen.getByText('Salve para testar.')).toBeDefined();
  });

  it('reabilita o testar depois de salvar', async () => {
    mockFetch({ status: 204 }, { status: 200, body: statusOk });
    render(<ServerSettingsForm initial={saved} />);

    await userEvent.type(field('Senha'), 'segredo');
    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));

    expect(await screen.findByText('Salvo.')).toBeDefined();
    const test = screen.getByRole('button', { name: 'Testar conexões' }) as HTMLButtonElement;
    expect(test.disabled).toBe(false);
    expect(screen.queryByText('Salve para testar.')).toBeNull();
  });

  it('diz quando uma conexão não está configurada', async () => {
    mockFetch({
      status: 200,
      body: {
        slskd: { state: 'ok', message: null },
        subsonic: { state: 'not-configured', message: null },
        fanartTv: { state: 'not-configured', message: null },
        spotify: { state: 'not-configured', message: null },
      },
    });
    render(<ServerSettingsForm initial={saved} />);

    await userEvent.click(screen.getByRole('button', { name: 'Testar conexões' }));

    expect(await screen.findByText('Servidor de música: não configurado.')).toBeDefined();
    expect(screen.getByText('fanart.tv: não configurado.')).toBeDefined();
    expect(screen.getByText('Spotify: não configurado.')).toBeDefined();
  });
});
