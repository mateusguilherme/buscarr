import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { ListPage } from './ListPage';
import { mockFetch } from './test-utils';

function renderListPage() {
  render(
    <MemoryRouter initialEntries={['/list']}>
      <ListPage />
    </MemoryRouter>,
  );
}

const listed = [
  { id: '83048ffe', name: 'Rancore', disambiguation: 'Brazillian Rock Band', area: 'São Paulo', image: null, listed: true },
  { id: '5b11f4ce', name: 'Nirvana', disambiguation: null, area: null, image: null, listed: true },
];

describe('ListPage', () => {
  it('mostra os artistas da lista, cada um marcado como na lista', async () => {
    const fetchMock = mockFetch({ status: 200, body: listed });
    renderListPage();

    const items = await screen.findAllByRole('listitem');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/list');
    expect(items.map((item) => within(item).getByRole('button', { name: 'Na lista' }).textContent)).toEqual([
      'Na lista',
      'Na lista',
    ]);
    expect(within(items[0]).getByText('Rancore')).toBeDefined();
    expect(within(items[0]).getByText('Brazillian Rock Band')).toBeDefined();
    expect(within(items[0]).getByText('São Paulo')).toBeDefined();
    expect(within(items[1]).getByText('Nirvana')).toBeDefined();
    expect(screen.queryByRole('link', { name: /Rancore|Nirvana/ })).toBeNull();
  });

  it('tira da página o artista removido da lista', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: listed },
      { status: 200, body: { releaseTypes: ['album'] } },
      { status: 204 },
    );
    renderListPage();
    const [rancore] = await screen.findAllByRole('listitem');
    await userEvent.click(within(rancore).getByRole('button', { name: 'Na lista' }));

    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Remover da lista' }));

    await waitFor(() => expect(screen.queryByText('Rancore')).toBeNull());
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
    expect(fetchMock.mock.calls[2][0]).toBe('/api/list/83048ffe');
    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: 'DELETE' });
  });

  it('avisa quando a lista está vazia', async () => {
    mockFetch({ status: 200, body: [] });
    renderListPage();

    expect(await screen.findByText('Nenhum artista na lista ainda. Adicione pela busca.')).toBeDefined();
  });

  it('avisa quando não consegue carregar a lista', async () => {
    mockFetch({ status: 500 });
    renderListPage();

    expect((await screen.findByRole('alert')).textContent).toBe('Erro 500 ao carregar a lista.');
  });

  it('volta para a busca', async () => {
    mockFetch({ status: 200, body: [] });
    renderListPage();

    await screen.findByText(/Nenhum artista/);

    expect(screen.getByRole('link', { name: 'Voltar' }).getAttribute('href')).toBe('/');
  });
});
