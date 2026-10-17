import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { SearchPage } from './SearchPage';
import { mockFetch } from './test-utils';

function renderSearchPage({ url = '/', onLogout = vi.fn() } = {}) {
  render(
    <MemoryRouter initialEntries={[url]}>
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

  it('busca só pelo nome, sem filtros', async () => {
    const fetchMock = mockFetch({ status: 200, body: [] });
    renderSearchPage();

    expect(screen.queryByRole('combobox')).toBeNull();

    await search('nirvana');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists?q=nirvana');
  });

  it('explica a busca antes da primeira pesquisa', async () => {
    mockFetch({ status: 200, body: [{ id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null }] });
    renderSearchPage();

    expect(screen.getByText('Busque pelo nome do artista.')).toBeDefined();

    await search('rancore');
    await screen.findByRole('link', { name: 'Rancore' });

    expect(screen.queryByText('Busque pelo nome do artista.')).toBeNull();
  });

  it('mostra cada candidato com a descrição, a área e o link para os releases', async () => {
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
    expect(screen.getByText('Italian rapper Tarek Iurcich')).toBeDefined();
    expect(screen.getByText('Italy')).toBeDefined();
    expect(screen.getByText('Brazillian Rock Band')).toBeDefined();
    expect(screen.getByText('São Paulo')).toBeDefined();
    expect(screen.getByText('3 artistas encontrados')).toBeDefined();
  });

  it('mostra a foto de cada candidato que tem uma', async () => {
    const photo = 'https://assets.fanart.tv/preview/music/e33ee038/artistthumb/rancore.jpg';
    mockFetch({
      status: 200,
      body: [
        { id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null, image: photo },
        { id: '83048ffe', name: 'Rancore', disambiguation: null, area: null, image: null },
      ],
    });
    renderSearchPage();

    await search('rancore');

    const [withPhoto, withoutPhoto] = await screen.findAllByRole('listitem');
    expect(withPhoto.querySelector('img')?.getAttribute('src')).toBe(photo);
    expect(withoutPhoto.querySelector('img')).toBeNull();
  });

  it('conta um único candidato no singular', async () => {
    mockFetch({ status: 200, body: [{ id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null }] });
    renderSearchPage();

    await search('rancore');

    expect(await screen.findByText('1 artista encontrado')).toBeDefined();
  });

  it('refaz a busca que está na URL', async () => {
    const fetchMock = mockFetch({ status: 200, body: [{ id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null }] });
    renderSearchPage({ url: '/?q=rancore' });

    expect(await screen.findByRole('link', { name: 'Rancore' })).toBeDefined();
    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists?q=rancore');
    expect((screen.getByPlaceholderText('Nome do artista') as HTMLInputElement).value).toBe('rancore');
  });

  it('busca de novo ao repetir a mesma busca', async () => {
    mockFetch({ status: 500 }, { status: 200, body: [{ id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null }] });
    renderSearchPage();

    await search('rancore');
    await screen.findByText('Erro na busca.');
    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(await screen.findByRole('link', { name: 'Rancore' })).toBeDefined();
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
    renderSearchPage({ onLogout });

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }));

    expect(fetchMock).toHaveBeenCalledWith('/api/auth/logout', { method: 'POST' });
    expect(onLogout).toHaveBeenCalled();
  });
});
