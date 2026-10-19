import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import type { ArtistRelease } from './api';
import { ReleasesPage } from './ReleasesPage';
import { mockFetch } from './test-utils';

const artistId = '5b11f4ce-a62d-471e-81fc-a69a8278c7da';

function release(overrides: Partial<ArtistRelease> = {}): ArtistRelease {
  return {
    releaseGroupId: 'g1',
    title: 'Nevermind',
    type: 'album',
    firstReleaseDate: '1991-09-24',
    coverUrl: null,
    trackCount: 12,
    requested: false,
    inLibrary: false,
    ...overrides,
  };
}

function renderPage(state?: { name?: string; from?: string }) {
  render(
    <MemoryRouter initialEntries={[{ pathname: `/artists/${artistId}/releases`, state }]}>
      <Routes>
        <Route path="/artists/:id/releases" element={<ReleasesPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('ReleasesPage', () => {
  it('mostra os lançamentos do artista e pede um álbum', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: [release()] },
      { status: 204 },
    );
    renderPage({ name: 'Nirvana' });

    expect(screen.getByRole('heading', { name: 'Lançamentos de Nirvana' })).toBeDefined();
    expect(await screen.findByText('Nevermind')).toBeDefined();
    expect(screen.getByText('1991')).toBeDefined();
    expect(screen.getByText('12 faixas')).toBeDefined();
    expect(fetchMock.mock.calls[0][0]).toBe(`/api/artists/${artistId}/releases`);

    await userEvent.click(screen.getByRole('button', { name: 'Baixar' }));

    expect(await screen.findByText('Pedido')).toBeDefined();
    const call = fetchMock.mock.calls.find((args) => args[0] === '/api/downloads');
    expect(call?.[1]).toMatchObject({ method: 'POST' });
    expect(JSON.parse(call?.[1]?.body as string)).toEqual({ artistId, releaseGroupId: 'g1' });
  });

  it('troca a capa quebrada pelo ícone de disco', async () => {
    mockFetch({ status: 200, body: [release({ coverUrl: 'https://capas.local/quebrada.jpg' })] });
    renderPage({ name: 'Nirvana' });
    await screen.findByText('Nevermind');
    const cover = () => document.querySelector('.avatar img') as HTMLImageElement | null;

    expect(cover()?.src).toBe('https://capas.local/quebrada.jpg');

    fireEvent.error(cover()!);

    expect(cover()).toBeNull();
  });

  it('mostra os álbuns já pedidos sem o botão', async () => {
    mockFetch({ status: 200, body: [release({ requested: true })] });
    renderPage({ name: 'Nirvana' });

    expect(await screen.findByText('Pedido')).toBeDefined();
    expect(screen.queryByRole('button', { name: 'Baixar' })).toBeNull();
  });

  it('mostra os álbuns que a biblioteca já tem sem o botão', async () => {
    mockFetch({ status: 200, body: [release({ inLibrary: true })] });
    renderPage({ name: 'Nirvana' });

    expect(await screen.findByText('Na biblioteca')).toBeDefined();
    expect(screen.queryByRole('button', { name: 'Baixar' })).toBeNull();
  });

  it('mostra a discografia vazia', async () => {
    mockFetch({ status: 200, body: [] });
    renderPage({ name: 'Nirvana' });

    expect(await screen.findByText('Nenhum lançamento encontrado.')).toBeDefined();
  });

  it('avisa quando não consegue carregar os lançamentos', async () => {
    mockFetch({ status: 500 });
    renderPage({ name: 'Nirvana' });

    expect((await screen.findByRole('alert')).textContent).toBe('Erro 500 ao carregar os lançamentos.');
  });

  it('usa o título genérico quando abre direto pela URL', async () => {
    mockFetch({ status: 200, body: [] });
    renderPage();

    expect(screen.getByRole('heading', { name: 'Lançamentos' })).toBeDefined();
    expect(screen.getByRole('link', { name: 'Voltar' }).getAttribute('href')).toBe('/');

    await screen.findByText('Nenhum lançamento encontrado.');
  });

  it('volta para a página de origem', async () => {
    mockFetch({ status: 200, body: [] });
    renderPage({ name: 'Nirvana', from: '/?q=nirvana' });

    expect(screen.getByRole('link', { name: 'Voltar' }).getAttribute('href')).toBe('/?q=nirvana');

    await screen.findByText('Nenhum lançamento encontrado.');
  });
});
