import { render, screen } from '@testing-library/react';
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
  it('lista os releases com data e nome, separados entre estúdio e ao vivo', async () => {
    mockFetch({ status: 200, body: nirvana });
    renderReleasesPage();

    expect(await screen.findByRole('heading', { name: 'Nirvana' })).toBeDefined();

    const [studio, live] = screen.getAllByRole('list');
    expect(screen.getByRole('heading', { name: 'Estúdio' })).toBeDefined();
    expect(screen.getByRole('heading', { name: 'Ao vivo' })).toBeDefined();
    expect(studio.textContent).toBe('1991-09-24 · Nevermind');
    expect(live.textContent).toBe('1994-11-01 · MTV Unplugged in New York');
  });

  it('pede os álbuns quando a URL não escolhe um tipo', async () => {
    const fetchMock = mockFetch({ status: 200, body: nirvana });
    renderReleasesPage();

    await screen.findByRole('heading', { name: 'Nirvana' });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists/abc-123/releases?type=album');
  });

  it('busca de novo ao trocar o tipo', async () => {
    const fetchMock = mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvana });
    renderReleasesPage();
    await screen.findByRole('heading', { name: 'Nirvana' });

    await userEvent.selectOptions(screen.getByRole('combobox'), 'single');
    await screen.findByRole('heading', { name: 'Nirvana' });

    expect(fetchMock.mock.calls[1][0]).toBe('/api/artists/abc-123/releases?type=single');
  });

  it('avisa quando não há releases', async () => {
    mockFetch({ status: 200, body: { name: 'Nirvana', releases: [] } });
    renderReleasesPage();

    expect(await screen.findByText('Nenhum release oficial encontrado.')).toBeDefined();
  });
});
