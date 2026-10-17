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

    const select = screen.getByRole('combobox');
    expect((select as HTMLSelectElement).value).toBe('');
    expect(screen.getByRole('option', { name: 'Todos os países' })).toBeDefined();
    expect(screen.getByRole('option', { name: 'Reino Unido' })).toBeDefined();
  });

  it('envia o país escolhido na busca', async () => {
    const fetchMock = mockFetch({ status: 200, body: { id: 'abc-123', name: 'Nirvana' } });
    renderSearchPage();

    await userEvent.selectOptions(screen.getByRole('combobox'), 'GB');
    await search('nirvana');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists?q=nirvana&country=GB');
  });

  it('mostra o artista encontrado com link para os releases', async () => {
    mockFetch({ status: 200, body: { id: 'abc-123', name: 'Nirvana' } });
    renderSearchPage();

    await search('nirvana');

    const link = await screen.findByRole('link', { name: 'Nirvana' });
    expect(link.getAttribute('href')).toBe('/artist/abc-123');
  });

  it('avisa quando nenhum artista é encontrado', async () => {
    mockFetch({ status: 404 });
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
