import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Download, QuarantinedAlbum } from './api';
import { DownloadsPage } from './DownloadsPage';
import { mockFetch } from './test-utils';

class FakeEventSource {
  static instances: FakeEventSource[] = [];

  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: (() => void) | null = null;
  closed = false;

  constructor(public url: string) {
    FakeEventSource.instances.push(this);
  }

  close() {
    this.closed = true;
  }

  emit(downloads: Download[]) {
    this.onmessage?.({ data: JSON.stringify(downloads) } as MessageEvent);
  }
}

function download(overrides: Partial<Download>): Download {
  return {
    id: 1,
    artistName: 'Nirvana',
    credit: 'Nirvana',
    title: 'Nevermind',
    type: 'album',
    coverUrl: null,
    trackCount: 12,
    quality: 'mp3-320',
    state: 'waiting',
    failure: null,
    attempts: 0,
    nextAttemptAt: '1970-01-01T00:00:00Z',
    tracksDone: 0,
    bytesTransferred: 0,
    bytesTotal: 0,
    placeInQueue: null,
    username: null,
    ...overrides,
  };
}

function quarantined(overrides: Partial<QuarantinedAlbum> = {}): QuarantinedAlbum {
  return {
    id: 9,
    artistName: 'Nirvana',
    title: 'Nevermind',
    reason: 'fake-audio',
    username: 'fulano',
    at: '2026-10-18T12:00:00Z',
    canImport: true,
    ...overrides,
  };
}

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/downloads']}>
      <DownloadsPage />
    </MemoryRouter>,
  );
}

describe('DownloadsPage', () => {
  beforeEach(() => {
    FakeEventSource.instances = [];
    vi.stubGlobal('EventSource', FakeEventSource);
  });

  it('mostra cada download com seu estado', async () => {
    mockFetch({ status: 200, body: [] });
    renderPage();
    await screen.findByText('Nenhum download ainda. Adicione artistas à lista.');

    FakeEventSource.instances[0].emit([
      download({ id: 1, state: 'downloading', tracksDone: 7, bytesTransferred: 70, bytesTotal: 120 }),
      download({ id: 2, title: 'In Utero', state: 'downloading', placeInQueue: 3 }),
      download({
        id: 3,
        title: 'Bleach',
        state: 'not-found',
        failure: 'no-matching-folder',
        nextAttemptAt: '2999-01-01T12:00:00Z',
      }),
      download({ id: 4, title: 'Incesticide', state: 'downloaded' }),
      download({ id: 5, title: 'Unplugged', state: 'failed', failure: 'refused' }),
      download({ id: 6, title: 'Hormoaning', state: 'cancelled' }),
      download({ id: 7, title: 'Sliver', state: 'verifying' }),
      download({ id: 8, title: 'Love Buzz', state: 'waiting', nextAttemptAt: '2999-01-01T12:00:00Z' }),
    ]);

    expect(await screen.findByText('Baixando 7/12')).toBeDefined();
    expect(screen.getByText('Na fila do peer (posição 3)')).toBeDefined();
    expect(screen.getByText('Nenhuma pasta serve')).toBeDefined();
    expect(screen.getByText('Baixado')).toBeDefined();
    expect(screen.getByText('Falhou: o peer recusou')).toBeDefined();
    expect(screen.getByText('Cancelado')).toBeDefined();
    expect(screen.getByText('verifying')).toBeDefined();
    expect(screen.getByText('Aguardando busca')).toBeDefined();
    expect(screen.getAllByText(/^Próxima tentativa em/)).toHaveLength(3);
  });

  it('mostra os estados e as razões do import', async () => {
    mockFetch({ status: 200, body: [] });
    renderPage();
    await screen.findByText('Nenhum download ainda. Adicione artistas à lista.');

    FakeEventSource.instances[0].emit([
      download({ id: 1, state: 'importing' }),
      download({ id: 2, title: 'In Utero', state: 'imported' }),
      download({ id: 3, title: 'Bleach', state: 'failed', failure: 'fake-audio' }),
      download({ id: 4, title: 'Incesticide', state: 'failed', failure: 'wrong-tracks' }),
    ]);

    expect(await screen.findByText('Importando')).toBeDefined();
    expect(screen.getByText('Importado')).toBeDefined();
    expect(screen.getByText('Falhou: a qualidade anunciada é falsa')).toBeDefined();
    expect(screen.getByText('Falhou: as faixas não batem com a edição')).toBeDefined();
  });

  it('cancela um download e não o deixa voltar', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: [download({ state: 'waiting' })] },
      { status: 200, body: [] },
      { status: 204 },
    );
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Cancelar' }));

    expect(await screen.findByText('Cancelado')).toBeDefined();
    const call = fetchMock.mock.calls.find((args) => args[0] === '/api/downloads/1/cancel');
    expect(call?.[1]).toMatchObject({ method: 'POST' });
  });

  it('mostra todos os artistas creditados na faixa, completos ao passar o mouse', async () => {
    mockFetch({
      status: 200,
      body: [download({ title: 'Do Corre', credit: 'Maru2D, AJULLIACOSTA, NADAMAL & DJ LN' })],
    });
    renderPage();

    const credit = await screen.findByText('Maru2D, AJULLIACOSTA, NADAMAL & DJ LN');
    expect(credit.getAttribute('title')).toBe('Maru2D, AJULLIACOSTA, NADAMAL & DJ LN');
    expect(screen.getByText('Do Corre').getAttribute('title')).toBe('Do Corre');
    expect(screen.queryByText('Nirvana')).toBeNull();
  });

  it('tenta a capa de novo quando a URL muda', async () => {
    mockFetch({ status: 200, body: [] });
    renderPage();
    await screen.findByText('Nenhum download ainda. Adicione artistas à lista.');
    const cover = () => document.querySelector('.avatar img') as HTMLImageElement | null;

    FakeEventSource.instances[0].emit([download({ coverUrl: 'https://capas.local/quebrada.jpg' })]);
    await screen.findByText('Nevermind');
    fireEvent.error(cover()!);

    expect(cover()).toBeNull();

    FakeEventSource.instances[0].emit([download({ coverUrl: 'https://capas.local/boa.jpg' })]);

    await waitFor(() => expect(cover()?.src).toBe('https://capas.local/boa.jpg'));
  });

  it('tenta de novo um download que falhou', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: [download({ state: 'failed', failure: 'stalled', nextAttemptAt: '2999-01-01T12:00:00Z' })] },
      { status: 200, body: [] },
      { status: 200, body: [] },
      { status: 204 },
    );
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Tentar agora' }));

    expect(await screen.findByText('Aguardando busca')).toBeDefined();
    const call = fetchMock.mock.calls.find((args) => args[0] === '/api/downloads/1/retry');
    expect(call?.[1]).toMatchObject({ method: 'POST' });
  });

  it('não oferece tentar de novo durante o download', async () => {
    mockFetch({ status: 200, body: [download({ state: 'downloading' })] });
    renderPage();

    await screen.findByText('Baixando 0/12');

    expect(screen.queryByRole('button', { name: 'Tentar agora' })).toBeNull();
  });

  it('refaz a busca quando o SSE cai', async () => {
    mockFetch(
      { status: 200, body: [] },
      { status: 200, body: [] },
      { status: 200, body: [download({ state: 'downloaded' })] },
    );
    renderPage();

    expect(await screen.findByText('Nenhum download ainda. Adicione artistas à lista.')).toBeDefined();

    FakeEventSource.instances[0].onerror?.();

    expect(await screen.findByText('Baixado')).toBeDefined();
    expect(FakeEventSource.instances[0].closed).toBe(true);
  });

  it('mostra a lista vazia', async () => {
    mockFetch({ status: 200, body: [] });
    renderPage();

    expect(await screen.findByText('Nenhum download ainda. Adicione artistas à lista.')).toBeDefined();
  });

  it('mostra a quarentena com o motivo e as ações', async () => {
    mockFetch(
      { status: 200, body: [] },
      { status: 200, body: [quarantined()] },
    );
    renderPage();

    expect(await screen.findByText('Quarentena')).toBeDefined();
    expect(screen.getByText('Nevermind')).toBeDefined();
    expect(screen.getByText('a qualidade anunciada é falsa')).toBeDefined();
    expect(screen.getByText('de fulano')).toBeDefined();
    expect(screen.getByRole('button', { name: 'Importar mesmo assim' })).toBeDefined();
    expect(screen.getByRole('button', { name: 'Descartar' })).toBeDefined();
  });

  it('esconde a quarentena quando está vazia', async () => {
    mockFetch({ status: 200, body: [] }, { status: 200, body: [] });
    renderPage();
    await screen.findByText('Nenhum download ainda. Adicione artistas à lista.');

    expect(screen.queryByText('Quarentena')).toBeNull();
  });

  it('importa mesmo assim e tira o álbum da quarentena', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: [] },
      { status: 200, body: [quarantined()] },
      { status: 204 },
    );
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Importar mesmo assim' }));

    await waitFor(() => expect(screen.queryByText('Quarentena')).toBeNull());
    const call = fetchMock.mock.calls.find((args) => args[0] === '/api/quarantine/9/import');
    expect(call?.[1]).toMatchObject({ method: 'POST' });
  });

  it('descarta os arquivos da quarentena', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: [] },
      { status: 200, body: [quarantined()] },
      { status: 204 },
    );
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Descartar' }));

    await waitFor(() => expect(screen.queryByText('Quarentena')).toBeNull());
    const call = fetchMock.mock.calls.find((args) => args[0] === '/api/quarantine/9');
    expect(call?.[1]).toMatchObject({ method: 'DELETE' });
  });

  it('não oferece importar quando o pedido já não existe', async () => {
    mockFetch(
      { status: 200, body: [] },
      { status: 200, body: [quarantined({ canImport: false })] },
    );
    renderPage();

    await screen.findByText('Quarentena');

    expect(screen.queryByRole('button', { name: 'Importar mesmo assim' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Descartar' })).toBeDefined();
  });
});
