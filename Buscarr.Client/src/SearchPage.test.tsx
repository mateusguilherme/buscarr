import { render, screen, waitFor, within } from '@testing-library/react';
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

const rancores = [
  { id: 'e33ee038', name: 'Rancore', disambiguation: 'Italian rapper Tarek Iurcich', area: 'Italy', image: null, listed: false },
  { id: '83048ffe', name: 'Rancore', disambiguation: 'Brazillian Rock Band', area: 'São Paulo', image: null, listed: true },
];

const listed = [
  { id: '83048ffe', name: 'Rancore', disambiguation: 'Brazillian Rock Band', area: 'São Paulo', image: null, listed: true },
  { id: '5b11f4ce', name: 'Nirvana', disambiguation: null, area: null, image: null, listed: true },
];

async function searchRancores(...responses: { status: number; body?: unknown }[]) {
  const fetchMock = mockFetch({ status: 200, body: [] }, { status: 200, body: rancores }, ...responses);
  renderSearchPage();

  await search('rancore');

  return { fetchMock, items: await screen.findAllByRole('listitem') };
}

describe('SearchPage', () => {
  it('cumprimenta o usuário', () => {
    mockFetch({ status: 200, body: [] });
    renderSearchPage();

    expect(screen.getByText(/Olá, Fulano/)).toBeDefined();
  });

  it('leva às configurações pelo cabeçalho', () => {
    mockFetch({ status: 200, body: [] });
    renderSearchPage();

    expect(screen.getByRole('link', { name: 'Configurações' }).getAttribute('href')).toBe('/settings');
  });

  it('mostra a lista de artistas na home, sem página de lista separada', async () => {
    const fetchMock = mockFetch({ status: 200, body: listed });
    renderSearchPage();

    const items = await screen.findAllByRole('listitem');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/list');
    expect(items.map((item) => within(item).getByRole('button', { name: 'Na lista' }).textContent)).toEqual([
      'Na lista',
      'Na lista',
    ]);
    expect(within(items[0]).getByText('Rancore')).toBeDefined();
    expect(within(items[1]).getByText('Nirvana')).toBeDefined();
    expect(screen.queryByRole('link', { name: 'Minha lista' })).toBeNull();
  });

  it('tira da home o artista removido da lista', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: listed },
      { status: 200, body: { releaseTypes: ['album'] } },
      { status: 204 },
    );
    renderSearchPage();
    const [rancore] = await screen.findAllByRole('listitem');
    await userEvent.click(within(rancore).getByRole('button', { name: 'Na lista' }));

    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Remover da lista' }));

    await waitFor(() => expect(screen.queryByText('Rancore')).toBeNull());
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
    expect(fetchMock.mock.calls[2][0]).toBe('/api/list/83048ffe');
    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: 'DELETE' });
  });

  it('convida a buscar quando a lista está vazia', async () => {
    mockFetch(
      { status: 200, body: [] },
      { status: 200, body: [{ id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null }] },
    );
    renderSearchPage();

    expect(await screen.findByText('Nenhum artista na lista ainda. Busque pelo nome para adicionar.')).toBeDefined();

    await search('rancore');
    await screen.findByText('Rancore');

    expect(screen.queryByText(/Nenhum artista na lista/)).toBeNull();
  });

  it('avisa quando não consegue carregar a lista', async () => {
    mockFetch({ status: 500 });
    renderSearchPage();

    expect(await screen.findByText('Erro 500 ao carregar a lista.')).toBeDefined();
  });

  it('mostra a lista de novo ao voltar da busca', async () => {
    mockFetch(
      { status: 200, body: listed },
      { status: 200, body: [{ id: 'e33ee038', name: 'Caetano Veloso', disambiguation: null, area: null }] },
      { status: 200, body: listed },
    );
    renderSearchPage();
    await screen.findByText('Nirvana');

    await search('caetano');
    await screen.findByText('Caetano Veloso');
    await userEvent.click(screen.getByRole('link', { name: /buscarr/ }));

    expect(await screen.findByText('Nirvana')).toBeDefined();
    expect(screen.queryByText('Caetano Veloso')).toBeNull();
  });

  it('filtra a lista em tempo real enquanto digita, sem buscar no servidor', async () => {
    const fetchMock = mockFetch({ status: 200, body: listed });
    renderSearchPage();
    await screen.findByText('Nirvana');

    await userEvent.type(screen.getByPlaceholderText('Nome do artista'), 'nir');

    expect(screen.getByText('Nirvana')).toBeDefined();
    expect(screen.queryByText('Rancore')).toBeNull();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('ignora acentos e maiúsculas ao filtrar a lista', async () => {
    mockFetch({
      status: 200,
      body: [
        ...listed,
        { id: '0383dadf', name: 'Mônica Salmaso', disambiguation: null, area: null, image: null, listed: true },
      ],
    });
    renderSearchPage();
    await screen.findByText('Nirvana');

    await userEvent.type(screen.getByPlaceholderText('Nome do artista'), 'MONICA');

    expect(screen.getByText('Mônica Salmaso')).toBeDefined();
    expect(screen.queryByText('Nirvana')).toBeNull();
    expect(screen.queryByText('Rancore')).toBeNull();
  });

  it('mostra a lista toda de novo ao apagar o filtro', async () => {
    mockFetch({ status: 200, body: listed });
    renderSearchPage();
    await screen.findByText('Nirvana');
    const input = screen.getByPlaceholderText('Nome do artista');
    await userEvent.type(input, 'nir');

    await userEvent.clear(input);

    expect(screen.getByText('Nirvana')).toBeDefined();
    expect(screen.getByText('Rancore')).toBeDefined();
  });

  it('não filtra os resultados da busca pelo texto digitado', async () => {
    mockFetch(
      { status: 200, body: [] },
      { status: 200, body: [{ id: '5b11f4ce', name: 'Nirvana', disambiguation: null, area: null }] },
    );
    renderSearchPage();

    await search('banda de grunge');

    expect(await screen.findByText('Nirvana')).toBeDefined();
  });

  it('limpa o texto da pesquisa com Esc, mantendo o foco', async () => {
    mockFetch({ status: 200, body: listed });
    renderSearchPage();
    await screen.findByText('Nirvana');
    const input = screen.getByPlaceholderText('Nome do artista');
    await userEvent.type(input, 'nir');
    expect(screen.queryByText('Rancore')).toBeNull();

    await userEvent.keyboard('{Escape}');

    expect((input as HTMLInputElement).value).toBe('');
    expect(screen.getByText('Rancore')).toBeDefined();
    expect(screen.getByText('Nirvana')).toBeDefined();
    expect(document.activeElement).toBe(input);
  });

  it('mostra os artistas numa grade', async () => {
    mockFetch({ status: 200, body: listed });
    renderSearchPage();
    await screen.findByText('Nirvana');

    const list = screen.getByRole('list');

    expect(list.className).toContain('card-grid');
    expect(within(list).getAllByRole('listitem').every((item) => item.className.includes('grid-card'))).toBe(true);
  });

  it('não foca o campo de busca ao abrir a home', async () => {
    mockFetch({ status: 200, body: listed });
    renderSearchPage();
    await screen.findByText('Nirvana');

    expect(document.activeElement).not.toBe(screen.getByPlaceholderText('Nome do artista'));
  });

  it('tira o foco do campo com Esc quando não há texto', async () => {
    mockFetch({ status: 200, body: listed });
    renderSearchPage();
    await screen.findByText('Nirvana');
    const input = screen.getByPlaceholderText('Nome do artista');
    input.focus();

    await userEvent.keyboard('{Escape}');

    expect(document.activeElement).not.toBe(input);
  });

  it('limpa a busca e volta à lista pelo link Voltar', async () => {
    mockFetch(
      { status: 200, body: listed },
      { status: 200, body: [{ id: 'e33ee038', name: 'Caetano Veloso', disambiguation: null, area: null }] },
      { status: 200, body: listed },
    );
    renderSearchPage();
    await screen.findByText('Nirvana');

    expect(screen.queryByRole('link', { name: 'Voltar' })).toBeNull();

    await search('caetano');
    await screen.findByText('Caetano Veloso');
    await userEvent.click(screen.getByRole('link', { name: 'Voltar' }));

    expect(await screen.findByText('Nirvana')).toBeDefined();
    expect(screen.queryByText('Caetano Veloso')).toBeNull();
    expect(screen.queryByRole('link', { name: 'Voltar' })).toBeNull();
    expect((screen.getByPlaceholderText('Nome do artista') as HTMLInputElement).value).toBe('');
  });

  it('busca só pelo nome, sem filtros', async () => {
    const fetchMock = mockFetch({ status: 200, body: [] }, { status: 200, body: [] });
    renderSearchPage();

    expect(screen.queryByRole('combobox')).toBeNull();

    await search('nirvana');

    expect(fetchMock.mock.calls[1][0]).toBe('/api/artists?q=nirvana');
  });

  it('mostra cada candidato com a descrição e a área, com link para os lançamentos', async () => {
    mockFetch(
      { status: 200, body: [] },
      {
        status: 200,
        body: [
          { id: 'e33ee038', name: 'Rancore', disambiguation: 'Italian rapper Tarek Iurcich', area: 'Italy' },
          { id: '83048ffe', name: 'Rancore', disambiguation: 'Brazillian Rock Band', area: 'São Paulo' },
          { id: 'b533daaf', name: 'Rancore', disambiguation: null, area: null },
        ],
      },
    );
    renderSearchPage();

    await search('rancore');

    const links = await screen.findAllByRole('link', { name: 'Rancore' });

    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/artists/e33ee038/releases',
      '/artists/83048ffe/releases',
      '/artists/b533daaf/releases',
    ]);
    expect(screen.getByText('Italian rapper Tarek Iurcich')).toBeDefined();
    expect(screen.getByText('Italy')).toBeDefined();
    expect(screen.getByText('Brazillian Rock Band')).toBeDefined();
    expect(screen.getByText('São Paulo')).toBeDefined();
    expect(screen.queryByText('3 artistas encontrados')).toBeNull();
  });

  it('mostra a foto de cada candidato que tem uma', async () => {
    const photo = 'https://assets.fanart.tv/preview/music/e33ee038/artistthumb/rancore.jpg';
    mockFetch(
      { status: 200, body: [] },
      {
        status: 200,
        body: [
          { id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null, image: photo },
          { id: '83048ffe', name: 'Rancore', disambiguation: null, area: null, image: null },
        ],
      },
    );
    renderSearchPage();

    await search('rancore');

    const [withPhoto, withoutPhoto] = await screen.findAllByRole('listitem');
    expect(withPhoto.querySelector('img')?.getAttribute('src')).toBe(photo);
    expect(withoutPhoto.querySelector('img')).toBeNull();
    expect(withoutPhoto.querySelector('.avatar svg')).not.toBeNull();
  });

  it('mostra ao lado de cada candidato o botão de pôr na lista, marcando quem já está nela', async () => {
    const { items } = await searchRancores();

    expect(within(items[0]).getByRole('button', { name: 'Adicionar à lista' }).getAttribute('aria-pressed')).toBe('false');
    expect(within(items[1]).getByRole('button', { name: 'Na lista' }).getAttribute('aria-pressed')).toBe('true');
  });

  it('abre um modal para adicionar o artista, só com os álbuns marcados e sem escolher edição', async () => {
    const { fetchMock, items } = await searchRancores();

    await userEvent.click(within(items[0]).getByRole('button', { name: 'Adicionar à lista' }));

    const dialog = await screen.findByRole('dialog', { name: 'Adicionar Rancore à lista' });
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(within(dialog).queryByRole('radio')).toBeNull();
    expect(
      within(dialog)
        .getAllByRole('checkbox')
        .map((box) => [box.closest('label')?.textContent, (box as HTMLInputElement).checked]),
    ).toEqual([
      ['Álbuns', true],
      ['EPs', false],
      ['Singles', false],
      ['Ao vivo', false],
      ['Compilações', false],
      ['Trilhas sonoras', false],
      ['Remixes', false],
      ['Demos', false],
    ]);
  });

  it('adiciona o artista com os releases escolhidos', async () => {
    const { fetchMock, items } = await searchRancores({ status: 204 });
    await userEvent.click(within(items[0]).getByRole('button', { name: 'Adicionar à lista' }));
    const dialog = await screen.findByRole('dialog');

    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Ao vivo' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Adicionar' }));

    expect(await within(items[0]).findByRole('button', { name: 'Na lista' })).toBeDefined();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(fetchMock.mock.calls[2][0]).toBe('/api/list/e33ee038');
    expect(fetchMock.mock.calls[2][1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ releaseTypes: ['album', 'live'] }),
    });
  });

  it('não deixa adicionar sem nenhum release escolhido', async () => {
    const { items } = await searchRancores();
    await userEvent.click(within(items[0]).getByRole('button', { name: 'Adicionar à lista' }));
    const dialog = await screen.findByRole('dialog');

    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Álbuns' }));

    expect((within(dialog).getByRole('button', { name: 'Adicionar' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('fecha o modal sem adicionar ao cancelar', async () => {
    const { fetchMock, items } = await searchRancores();
    await userEvent.click(within(items[0]).getByRole('button', { name: 'Adicionar à lista' }));

    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancelar' }));

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(within(items[0]).getByRole('button', { name: 'Adicionar à lista' })).toBeDefined();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('avisa no modal quando não consegue adicionar', async () => {
    const { items } = await searchRancores({ status: 502 });
    await userEvent.click(within(items[0]).getByRole('button', { name: 'Adicionar à lista' }));
    const dialog = await screen.findByRole('dialog');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Adicionar' }));

    expect((await within(dialog).findByRole('alert')).textContent).toBe('Erro 502 ao adicionar à lista.');
    expect(screen.getByRole('dialog')).toBeDefined();
  });

  it('avisa no candidato quando não consegue abrir o modal', async () => {
    const { items } = await searchRancores({ status: 500 });

    await userEvent.click(within(items[1]).getByRole('button', { name: 'Na lista' }));

    expect((await within(items[1]).findByRole('alert')).textContent).toBe('Erro 500 ao carregar a lista.');
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('edita no modal o artista que já está na lista', async () => {
    const { fetchMock, items } = await searchRancores(
      { status: 200, body: { releaseTypes: ['single'] } },
      { status: 204 },
    );

    await userEvent.click(within(items[1]).getByRole('button', { name: 'Na lista' }));
    const dialog = await screen.findByRole('dialog', { name: 'Rancore na lista' });

    expect(fetchMock.mock.calls[2][0]).toBe('/api/list/83048ffe');
    expect((within(dialog).getByRole('checkbox', { name: 'Singles' }) as HTMLInputElement).checked).toBe(true);

    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'EPs' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Salvar' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(fetchMock.mock.calls[3][1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ releaseTypes: ['ep', 'single'] }),
    });
  });

  it('tira o artista da lista pelo modal', async () => {
    const { fetchMock, items } = await searchRancores(
      { status: 200, body: { releaseTypes: ['album'] } },
      { status: 204 },
    );
    await userEvent.click(within(items[1]).getByRole('button', { name: 'Na lista' }));

    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Remover da lista' }));

    expect(await within(items[1]).findByRole('button', { name: 'Adicionar à lista' })).toBeDefined();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(fetchMock.mock.calls[3][0]).toBe('/api/list/83048ffe');
    expect(fetchMock.mock.calls[3][1]).toMatchObject({ method: 'DELETE' });
  });

  it('refaz a busca que está na URL', async () => {
    const fetchMock = mockFetch({ status: 200, body: [{ id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null }] });
    renderSearchPage({ url: '/?q=rancore' });

    expect(await screen.findByText('Rancore')).toBeDefined();
    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists?q=rancore');
    expect((screen.getByPlaceholderText('Nome do artista') as HTMLInputElement).value).toBe('rancore');
  });

  it('busca de novo ao repetir a mesma busca', async () => {
    mockFetch(
      { status: 200, body: [] },
      { status: 500 },
      { status: 200, body: [{ id: 'e33ee038', name: 'Rancore', disambiguation: null, area: null }] },
    );
    renderSearchPage();

    await search('rancore');
    await screen.findByText('Erro na busca.');
    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(await screen.findByText('Rancore')).toBeDefined();
  });

  it('avisa quando nenhum artista é encontrado', async () => {
    mockFetch({ status: 200, body: [] }, { status: 200, body: [] });
    renderSearchPage();

    await search('zzqxjvkwplm');

    expect(await screen.findByText('Nenhum artista encontrado.')).toBeDefined();
  });

  it('sai da conta pelo botão Sair', async () => {
    const fetchMock = mockFetch({ status: 200, body: [] }, { status: 200 });
    const onLogout = vi.fn();
    renderSearchPage({ onLogout });

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }));

    expect(fetchMock).toHaveBeenCalledWith('/api/auth/logout', { method: 'POST' });
    expect(onLogout).toHaveBeenCalled();
  });
});
