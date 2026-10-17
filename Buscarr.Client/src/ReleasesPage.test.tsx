import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useNavigate } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { ReleasesPage } from './ReleasesPage';
import { SearchPage } from './SearchPage';
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

function BrowserBack() {
  const navigate = useNavigate();

  return <button onClick={() => navigate(-1)}>Voltar do navegador</button>;
}

function renderSearchAndReleases() {
  render(
    <MemoryRouter>
      <BrowserBack />
      <Routes>
        <Route path="/" element={<SearchPage user={{ email: 'fulano@exemplo.com', name: 'Fulano' }} onLogout={vi.fn()} />} />
        <Route path="/artist/:id" element={<ReleasesPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

async function openArtistFromSearch() {
  renderSearchAndReleases();
  await userEvent.type(screen.getByPlaceholderText('Nome do artista'), 'rancore');
  await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
  await userEvent.click(await screen.findByRole('link', { name: 'Rancore' }));
  await screen.findByRole('heading', { name: 'Rancore' });
}

const rancore = [{ id: '83048ffe', name: 'Rancore', disambiguation: 'Brazillian Rock Band', area: 'São Paulo' }];
const rancoreReleases: unknown[] = [];

const nirvana = { id: 'abc-123', name: 'Nirvana', disambiguation: null, area: null };

const nirvanaReleases = [
  { title: 'Smells Like Teen Spirit', date: '1991-09-10', live: false, type: 'single' },
  { title: 'Nevermind', date: '1991-09-24', live: false, type: 'album' },
  { title: 'MTV Unplugged in New York', date: '1994-11-01', live: true, type: 'album' },
];

describe('ReleasesPage', () => {
  it('lista os releases separados entre estúdio e ao vivo', async () => {
    mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();

    expect(await screen.findByRole('heading', { name: 'Nirvana' })).toBeDefined();

    const [studio, live] = screen.getAllByRole('list');
    expect(screen.getByRole('heading', { name: 'Estúdio' })).toBeDefined();
    expect(screen.getByRole('heading', { name: 'Ao vivo' })).toBeDefined();
    expect(within(studio).getByText('Nevermind')).toBeDefined();
    expect(within(live).getByText('MTV Unplugged in New York')).toBeDefined();
  });

  it('mostra o nome do artista antes de os releases carregarem', async () => {
    const fetchMock = mockFetch({ status: 200, body: nirvana });
    fetchMock.mockReturnValueOnce(new Promise(() => {}));
    renderReleasesPage();

    expect(await screen.findByRole('heading', { name: 'Nirvana' })).toBeDefined();
    expect(screen.getByRole('status').textContent).toBe('Carregando…');
  });

  it('mostra a foto do artista pedido pelo id', async () => {
    const photo = 'https://assets.fanart.tv/preview/music/abc-123/artistthumb/nirvana.jpg';
    mockFetch({ status: 200, body: { ...nirvana, image: photo } }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();

    const heading = await screen.findByRole('heading', { name: 'Nirvana' });

    expect(heading.parentElement?.querySelector('img')?.getAttribute('src')).toBe(photo);
  });

  it('mostra a imagem padrão quando o artista não tem foto', async () => {
    mockFetch({ status: 200, body: { ...nirvana, image: null } }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();

    const heading = await screen.findByRole('heading', { name: 'Nirvana' });

    expect(heading.parentElement?.querySelector('.avatar svg')).not.toBeNull();
  });

  it('mostra a foto do artista que veio da busca', async () => {
    const photo = 'https://assets.fanart.tv/preview/music/83048ffe/artistthumb/rancore.jpg';
    mockFetch({ status: 200, body: [{ ...rancore[0], image: photo }] }, { status: 200, body: rancoreReleases });

    await openArtistFromSearch();

    const heading = screen.getByRole('heading', { name: 'Rancore' });
    expect(heading.parentElement?.querySelector('img')?.getAttribute('src')).toBe(photo);
  });

  it('usa o nome que veio da busca, sem pedir o artista', async () => {
    const fetchMock = mockFetch({ status: 200, body: rancore });
    fetchMock.mockReturnValueOnce(new Promise(() => {}));

    await openArtistFromSearch();

    expect(screen.getByRole('status').textContent).toBe('Carregando…');
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1][0]).toBe('/api/artists/83048ffe/releases');
  });

  it('mostra a data no formato do idioma, com a data completa no datetime', async () => {
    mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();

    const date = await screen.findByText('24 de set. de 1991');

    expect(date.tagName).toBe('TIME');
    expect(date.getAttribute('datetime')).toBe('1991-09-24');
  });

  it('pede todos os releases e mostra os álbuns quando a URL não escolhe um tipo', async () => {
    const fetchMock = mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();

    await screen.findByRole('heading', { name: 'Nirvana' });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/artists/abc-123');
    expect(fetchMock.mock.calls[1][0]).toBe('/api/artists/abc-123/releases');
    expect(screen.getByRole('link', { name: 'Álbuns', current: 'page' })).toBeDefined();
    expect(screen.getByText('Nevermind')).toBeDefined();
    expect(screen.queryByText('Smells Like Teen Spirit')).toBeNull();
  });

  it('troca o tipo filtrando na página, sem buscar de novo', async () => {
    const fetchMock = mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();
    await screen.findByRole('heading', { name: 'Nirvana' });

    await userEvent.click(screen.getByRole('link', { name: 'Singles' }));

    expect(screen.getByRole('link', { name: 'Singles', current: 'page' })).toBeDefined();
    expect(screen.getByText('Smells Like Teen Spirit')).toBeDefined();
    expect(screen.queryByText('Nevermind')).toBeNull();

    await userEvent.click(screen.getByRole('link', { name: 'Todos' }));

    expect(screen.getByText('Smells Like Teen Spirit')).toBeDefined();
    expect(screen.getByText('Nevermind')).toBeDefined();
    expect(screen.getByText('MTV Unplugged in New York')).toBeDefined();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('avisa quando não há releases do tipo escolhido', async () => {
    mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();
    await screen.findByRole('heading', { name: 'Nirvana' });

    await userEvent.click(screen.getByRole('link', { name: 'EPs' }));

    expect(screen.getByText('Nenhum release oficial encontrado.')).toBeDefined();
  });

  it('volta para a busca pela marca', async () => {
    mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();

    await screen.findByRole('heading', { name: 'Nirvana' });

    expect(screen.getByRole('link', { name: 'buscarr' }).getAttribute('href')).toBe('/');
  });

  it('volta para a busca de onde veio sem buscar de novo, mesmo depois de trocar o tipo', async () => {
    const fetchMock = mockFetch({ status: 200, body: rancore }, { status: 200, body: rancoreReleases });
    await openArtistFromSearch();

    await userEvent.click(screen.getByRole('link', { name: 'Singles' }));
    await screen.findByRole('heading', { name: 'Rancore' });
    await userEvent.click(screen.getByRole('link', { name: 'Voltar' }));

    expect(await screen.findByRole('link', { name: 'Rancore' })).toBeDefined();
    expect(screen.getByText('Brazillian Rock Band')).toBeDefined();
    expect((screen.getByPlaceholderText('Nome do artista') as HTMLInputElement).value).toBe('rancore');
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('volta pelo navegador sem buscar de novo', async () => {
    const fetchMock = mockFetch({ status: 200, body: rancore }, { status: 200, body: rancoreReleases });
    await openArtistFromSearch();

    await userEvent.click(screen.getByRole('button', { name: 'Voltar do navegador' }));

    expect(await screen.findByRole('link', { name: 'Rancore' })).toBeDefined();
    expect(screen.getByText('Brazillian Rock Band')).toBeDefined();
    expect((screen.getByPlaceholderText('Nome do artista') as HTMLInputElement).value).toBe('rancore');
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('troca o tipo sem criar entrada no histórico do navegador', async () => {
    const fetchMock = mockFetch({ status: 200, body: rancore }, { status: 200, body: rancoreReleases });
    await openArtistFromSearch();

    await userEvent.click(screen.getByRole('link', { name: 'Singles' }));
    await screen.findByRole('heading', { name: 'Rancore' });
    await userEvent.click(screen.getByRole('button', { name: 'Voltar do navegador' }));

    expect(await screen.findByRole('link', { name: 'Rancore' })).toBeDefined();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('busca de novo ao repetir a busca depois de voltar', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: rancore },
      { status: 200, body: rancoreReleases },
      { status: 200, body: rancore },
    );
    await openArtistFromSearch();
    await userEvent.click(screen.getByRole('link', { name: 'Voltar' }));
    await screen.findByRole('link', { name: 'Rancore' });

    await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
    await screen.findByRole('link', { name: 'Rancore' });

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[2][0]).toBe('/api/artists?q=rancore');
  });

  it('volta para a busca vazia quando não veio de uma busca', async () => {
    mockFetch({ status: 200, body: nirvana }, { status: 200, body: nirvanaReleases });
    renderReleasesPage();

    await screen.findByRole('heading', { name: 'Nirvana' });

    expect(screen.getByRole('link', { name: 'Voltar' }).getAttribute('href')).toBe('/');
  });

  it('avisa quando não há releases', async () => {
    mockFetch({ status: 200, body: nirvana }, { status: 200, body: [] });
    renderReleasesPage();

    expect(await screen.findByText('Nenhum release oficial encontrado.')).toBeDefined();
  });
});
