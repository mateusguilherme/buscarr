import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Download } from './api';
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
      { status: 204 },
    );
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Cancelar' }));

    expect(await screen.findByText('Cancelado')).toBeDefined();
    expect(fetchMock.mock.calls[1][0]).toBe('/api/downloads/1/cancel');
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: 'POST' });
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

  it('tenta de novo um download que falhou', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: [download({ state: 'failed', failure: 'stalled', nextAttemptAt: '2999-01-01T12:00:00Z' })] },
      { status: 204 },
    );
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Tentar agora' }));

    expect(await screen.findByText('Aguardando busca')).toBeDefined();
    expect(fetchMock.mock.calls[1][0]).toBe('/api/downloads/1/retry');
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: 'POST' });
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
});
