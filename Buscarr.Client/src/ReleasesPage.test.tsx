import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { ReleasesPage } from './ReleasesPage';
import { mockFetch } from './test-utils';

function renderReleasesPage(url = '/artist/abc-123') {
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/artist/:id" element={<ReleasesPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

const nirvana = {
  name: 'Nirvana',
  releases: [
    { title: 'Nevermind', date: '1991-09-24', live: false },
    { title: 'MTV Unplugged in New York', date: '1994-11-01', live: true },
  ],
};

describe('ReleasesPage', () => {
  it('lista os releases separados entre estúdio e ao vivo', async () => {
    mockFetch({ status: 200, body: nirvana });
    renderReleasesPage();

    expect(await screen.findByRole('heading', { name: 'Nirvana' })).toBeDefined();

    const [studio, live] = screen.getAllByRole('list');
    expect(screen.getByRole('heading', { name: 'Estúdio' })).toBeDefined();
    expect(screen.getByRole('heading', { name: 'Ao vivo' })).toBeDefined();
    expect(within(studio).getByText('Nevermind')).toBeDefined();
    expect(within(live).getByText('MTV Unplugged in New York')).toBeDefined();
  });

  it('mostra a data no formato do idioma, com a data completa no datetime', async () => {
    mockFetch({ status: 200, body: nirvana });
    renderReleasesPage();

    const date = await screen.findByText('24 de set. de 1991');

    expect(date.tagName).toBe('TIME');
    expect(date.getAttribute('datetime')).toBe('1991-09-24');
  });

  it('pede os álbuns e marca o tipo quando a URL não escolhe um', async () => {
    const fetchMock = mockFetch({ status: 200, body: nirvana });
    renderReleasesPage();

    await screen.findByRole('heading', { name: 'Nirvana' });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists/abc-123/releases?type=album');
    expect(screen.getByRole('link', { name: 'Álbuns', current: 'page' })).toBeDefined();
  });

  it('busca de novo ao trocar o tipo', async () => {
    const fetchMock = mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvana });
    renderReleasesPage();
    await screen.findByRole('heading', { name: 'Nirvana' });

    await userEvent.click(screen.getByRole('link', { name: 'Singles' }));
    await screen.findByRole('heading', { name: 'Nirvana' });

    expect(fetchMock.mock.calls[1][0]).toBe('/api/artists/abc-123/releases?type=single');
    expect(screen.getByRole('link', { name: 'Singles', current: 'page' })).toBeDefined();
  });

  it('volta para a busca pela marca', async () => {
    mockFetch({ status: 200, body: nirvana });
    renderReleasesPage();

    await screen.findByRole('heading', { name: 'Nirvana' });

    expect(screen.getByRole('link', { name: 'buscarr' }).getAttribute('href')).toBe('/');
  });

  it('avisa quando não há releases', async () => {
    mockFetch({ status: 200, body: { name: 'Nirvana', releases: [] } });
    renderReleasesPage();

    expect(await screen.findByText('Nenhum release oficial encontrado.')).toBeDefined();
  });
});
