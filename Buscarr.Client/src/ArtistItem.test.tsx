import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import type { Artist } from './api';
import { ArtistItem } from './ArtistItem';

function artist(overrides: Partial<Artist> = {}): Artist {
  return {
    id: '5b11f4ce-a62d-471e-81fc-a69a8278c7da',
    name: 'Nirvana',
    disambiguation: null,
    area: null,
    image: null,
    listed: false,
    ...overrides,
  };
}

describe('ArtistItem', () => {
  it('leva para a página de lançamentos do artista', () => {
    render(
      <MemoryRouter initialEntries={['/?q=nirvana']}>
        <ul>
          <ArtistItem artist={artist()} />
        </ul>
      </MemoryRouter>,
    );

    const link = screen.getByRole('link', { name: 'Nirvana' });

    expect(link.getAttribute('href')).toBe('/artists/5b11f4ce-a62d-471e-81fc-a69a8278c7da/releases');
    expect(screen.queryByRole('link', { name: 'Lançamentos' })).toBeNull();
  });

  it('mostra o nome e a descrição completos no hover', () => {
    render(
      <MemoryRouter>
        <ul>
          <ArtistItem artist={artist({ disambiguation: '90s grunge band' })} />
        </ul>
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'Nirvana' }).getAttribute('title')).toBe('Nirvana');
    expect(screen.getByText('90s grunge band').getAttribute('title')).toBe('90s grunge band');
  });
});
