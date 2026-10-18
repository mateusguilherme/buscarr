import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { Artist, ArtistRelease } from './api';
import { ArtistItem } from './ArtistItem';
import { mockFetch } from './test-utils';

function artist(): Artist {
  return {
    id: '5b11f4ce-a62d-471e-81fc-a69a8278c7da',
    name: 'Nirvana',
    disambiguation: null,
    area: null,
    image: null,
    listed: false,
  };
}

function release(overrides: Partial<ArtistRelease> = {}): ArtistRelease {
  return {
    releaseGroupId: 'g1',
    title: 'Nevermind',
    type: 'album',
    firstReleaseDate: '1991-09-24',
    coverUrl: null,
    trackCount: 12,
    requested: false,
    ...overrides,
  };
}

function renderItem() {
  render(
    <ul>
      <ArtistItem artist={artist()} />
    </ul>,
  );
}

describe('ArtistItem', () => {
  it('abre os lançamentos do artista e pede um álbum', async () => {
    const fetchMock = mockFetch(
      { status: 200, body: [release()] },
      { status: 204 },
    );
    renderItem();

    await userEvent.click(screen.getByRole('button', { name: 'Lançamentos' }));
    const dialog = await screen.findByRole('dialog', { name: 'Lançamentos de Nirvana' });

    expect(within(dialog).getByText('Nevermind')).toBeDefined();
    expect(within(dialog).getByText('1991')).toBeDefined();
    expect(within(dialog).getByText('12 faixas')).toBeDefined();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Baixar' }));

    expect(await within(dialog).findByText('Pedido')).toBeDefined();
    const call = fetchMock.mock.calls.find((args) => args[0] === '/api/downloads');
    expect(call?.[1]).toMatchObject({ method: 'POST' });
    expect(JSON.parse(call?.[1]?.body as string)).toEqual({
      artistId: '5b11f4ce-a62d-471e-81fc-a69a8278c7da',
      releaseGroupId: 'g1',
    });
  });

  it('mostra os álbuns já pedidos sem o botão', async () => {
    mockFetch({ status: 200, body: [release({ requested: true })] });
    renderItem();

    await userEvent.click(screen.getByRole('button', { name: 'Lançamentos' }));
    const dialog = await screen.findByRole('dialog');

    expect(await within(dialog).findByText('Pedido')).toBeDefined();
    expect(within(dialog).queryByRole('button', { name: 'Baixar' })).toBeNull();
  });

  it('mostra a discografia vazia', async () => {
    mockFetch({ status: 200, body: [] });
    renderItem();

    await userEvent.click(screen.getByRole('button', { name: 'Lançamentos' }));

    expect(await screen.findByText('Nenhum lançamento encontrado.')).toBeDefined();
  });
});
