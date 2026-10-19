import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import type { Artist } from './api';
import { ArtistItem } from './ArtistItem';

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

describe('ArtistItem', () => {
  it('leva para a página de lançamentos do artista', () => {
    render(
      <MemoryRouter initialEntries={['/?q=nirvana']}>
        <ul>
          <ArtistItem artist={artist()} />
        </ul>
      </MemoryRouter>,
    );

    const link = screen.getByRole('link', { name: 'Lançamentos' });

    expect(link.getAttribute('href')).toBe('/artists/5b11f4ce-a62d-471e-81fc-a69a8278c7da/releases');
  });
});
