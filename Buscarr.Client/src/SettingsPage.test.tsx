import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { SettingsPage } from './SettingsPage';
import { mockFetch } from './test-utils';

function renderSettingsPage() {
  render(
    <MemoryRouter initialEntries={['/settings']}>
      <SettingsPage />
    </MemoryRouter>,
  );
}

function radio(name: RegExp) {
  return screen.findByRole('radio', { name }) as Promise<HTMLInputElement>;
}

describe('SettingsPage', () => {
  it('marca a edição que o usuário prefere', async () => {
    mockFetch({ status: 200, body: { edition: 'remastered', quality: 'mp3-320' } }, { status: 403 });
    renderSettingsPage();

    expect((await radio(/^A remasterizada/)).checked).toBe(true);
    expect((await radio(/^A original/)).checked).toBe(false);
    expect(screen.getByText('Se o álbum não tiver essa edição, fica a original.')).toBeDefined();
  });

  it('salva a edição escolhida, mantendo a qualidade', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: { edition: 'original', quality: 'flac' } },
      { status: 403 },
      { status: 204 },
    );
    renderSettingsPage();

    await userEvent.click(await radio(/^A deluxe/));

    expect(await screen.findByText('Salvo.')).toBeDefined();
    expect((await radio(/^A deluxe/)).checked).toBe(true);
    expect(fetchMock.mock.calls[2][0]).toBe('/api/settings');
    expect(fetchMock.mock.calls[2][1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ edition: 'deluxe', quality: 'flac' }),
    });
  });

  it('marca a qualidade que o usuário prefere, num grupo à parte', async () => {
    mockFetch({ status: 200, body: { edition: 'original', quality: 'flac' } }, { status: 403 });
    renderSettingsPage();

    const group = await screen.findByRole('group', { name: 'Baixar os arquivos em:' });

    expect(
      within(group)
        .getAllByRole('radio')
        .map((option) => [(option as HTMLInputElement).value, (option as HTMLInputElement).checked]),
    ).toEqual([
      ['mp3-320', false],
      ['flac', true],
    ]);
    expect((await radio(/^A original/)).checked).toBe(true);
  });

  it('salva a qualidade escolhida, mantendo a edição', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: { edition: 'deluxe', quality: 'mp3-320' } },
      { status: 403 },
      { status: 204 },
    );
    renderSettingsPage();

    await userEvent.click(await radio(/^FLAC/));

    expect(await screen.findByText('Salvo.')).toBeDefined();
    expect((await radio(/^FLAC/)).checked).toBe(true);
    expect((await radio(/^A deluxe/)).checked).toBe(true);
    expect(fetchMock.mock.calls[2][1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ edition: 'deluxe', quality: 'flac' }),
    });
  });

  it('volta à qualidade anterior quando não consegue salvar', async () => {
    mockFetch({ status: 200, body: { edition: 'original', quality: 'mp3-320' } }, { status: 403 }, { status: 500 });
    renderSettingsPage();

    await userEvent.click(await radio(/^FLAC/));

    expect((await screen.findByRole('alert')).textContent).toBe('Erro 500 ao salvar as configurações.');
    expect((await radio(/^MP3 320/)).checked).toBe(true);
  });

  it('avisa quando não consegue salvar', async () => {
    mockFetch({ status: 200, body: { edition: 'original', quality: 'mp3-320' } }, { status: 403 }, { status: 500 });
    renderSettingsPage();

    await userEvent.click(await radio(/^A deluxe/));

    expect((await screen.findByRole('alert')).textContent).toBe('Erro 500 ao salvar as configurações.');
  });

  it('volta para a busca', async () => {
    mockFetch({ status: 200, body: { edition: 'original', quality: 'mp3-320' } }, { status: 403 });
    renderSettingsPage();

    await radio(/^A original/);

    expect(screen.getByRole('link', { name: 'Voltar' }).getAttribute('href')).toBe('/');
  });

  it('não mostra as configurações do servidor para quem não é admin', async () => {
    mockFetch({ status: 200, body: { edition: 'original', quality: 'mp3-320' } }, { status: 403 });
    renderSettingsPage();

    await radio(/^A original/);

    expect(screen.queryByRole('heading', { name: 'Servidor' })).toBeNull();
  });

  it('mostra as configurações do servidor para o admin', async () => {
    mockFetch(
      { status: 200, body: { edition: 'original', quality: 'mp3-320' } },
      {
        status: 200,
        body: {
          slskdUrl: 'http://slskd:5030',
          hasSlskdApiKey: true,
          downloadsPath: '',
          libraryPath: '',
          rejectedPath: '',
          subsonicUrl: '',
          subsonicUser: '',
          hasSubsonicPassword: false,
          hasFanartTvApiKey: false,
          spotifyClientId: '',
          hasSpotifyClientSecret: false,
        },
      },
    );
    renderSettingsPage();

    expect(await screen.findByRole('heading', { name: 'Servidor' })).toBeDefined();
    expect((screen.getByLabelText('Endereço do slskd') as HTMLInputElement).value).toBe('http://slskd:5030');
  });

  it('avisa quando não consegue carregar as configurações do servidor', async () => {
    mockFetch({ status: 200, body: { edition: 'original', quality: 'mp3-320' } }, { status: 500 });
    renderSettingsPage();

    expect((await screen.findByRole('alert')).textContent).toBe('Erro 500 ao carregar as configurações do servidor.');
    expect((await radio(/^A original/)).checked).toBe(true);
  });
});
