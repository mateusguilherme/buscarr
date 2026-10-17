import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { SearchPage } from './SearchPage';
import { mockFetch } from './test-utils';

function renderSearchPage(onLogout = vi.fn()) {
  render(
    <MemoryRouter>
      <SearchPage user={{ email: 'fulano@exemplo.com', name: 'Fulano' }} onLogout={onLogout} />
    </MemoryRouter>,
  );
}

async function search(text: string) {
  await userEvent.type(screen.getByPlaceholderText('Nome do artista'), text);
  await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
}

describe('SearchPage', () => {
  it('cumprimenta o usuário', () => {
    renderSearchPage();

    expect(screen.getByText(/Olá, Fulano/)).toBeDefined();
  });

  it('lista os países pelo nome em português, começando por todos', () => {
    renderSearchPage();

    const select = screen.getByRole('combobox', { name: 'País' });
    expect((select as HTMLSelectElement).value).toBe('');
    expect(screen.getByRole('option', { name: 'Todos os países' })).toBeDefined();
    expect(screen.getByRole('option', { name: 'Reino Unido' })).toBeDefined();
  });

  it('oferece o tipo de artista, começando por qualquer um', () => {
    renderSearchPage();

    const select = screen.getByRole('combobox', { name: 'Tipo' });
    expect((select as HTMLSelectElement).value).toBe('');
    expect(screen.getByRole('option', { name: 'Qualquer tipo' })).toBeDefined();
    expect(screen.getByRole('option', { name: 'Banda' })).toBeDefined();
    expect(screen.getByRole('option', { name: 'Solo' })).toBeDefined();
  });

  it('envia o país escolhido na busca', async () => {
    const fetchMock = mockFetch({ status: 200, body: [] });
    renderSearchPage();

    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'País' }), 'GB');
    await search('nirvana');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists?q=nirvana&country=GB');
  });

  it('envia o tipo escolhido na busca', async () => {
    const fetchMock = mockFetch({ status: 200, body: [] });
    renderSearchPage();

    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Tipo' }), 'Banda');
    await search('rancore');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists?q=rancore&type=group');
  });

  it('mostra cada candidato com a descrição e o link para os releases', async () => {
    mockFetch({
      status: 200,
      body: [
        { id: 'e33ee038', name: 'Rancore', disambiguation: 'Italian rapper Tarek Iurcich', area: 'Italy' },
        { id: '83048ffe', name: 'Rancore', disambiguation: 'Brazillian Rock Band', area: 'São Paulo' },
        { id: 'b533daaf', name: 'Rancore', disambiguation: null, area: null },
      ],
    });
    renderSearchPage();

    await search('rancore');

    const links = await screen.findAllByRole('link', { name: 'Rancore' });
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/artist/e33ee038',
      '/artist/83048ffe',
      '/artist/b533daaf',
    ]);
    expect(screen.getByText(/Italian rapper Tarek Iurcich · Italy/)).toBeDefined();
    expect(screen.getByText(/Brazillian Rock Band · São Paulo/)).toBeDefined();
  });

  it('avisa quando nenhum artista é encontrado', async () => {
    mockFetch({ status: 200, body: [] });
    renderSearchPage();

    await search('zzqxjvkwplm');

    expect(await screen.findByText('Nenhum artista encontrado.')).toBeDefined();
  });

  it('sai da conta pelo botão Sair', async () => {
    const fetchMock = mockFetch({ status: 200 });
    const onLogout = vi.fn();
    renderSearchPage(onLogout);

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }));

    expect(fetchMock).toHaveBeenCalledWith('/api/auth/logout', { method: 'POST' });
    expect(onLogout).toHaveBeenCalled();
  });
});
