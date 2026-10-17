import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Cover } from './Cover';

const cover = 'https://coverartarchive.org/release/r1/front-250';

describe('Cover', () => {
  it('mostra a capa quando há uma', () => {
    const { container } = render(<Cover src={cover} />);

    expect(container.querySelector('img')?.getAttribute('src')).toBe(cover);
    expect(container.querySelector('svg')).toBeNull();
  });

  it('mostra a imagem padrão quando não há capa', () => {
    const { container } = render(<Cover src={null} />);

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('.cover svg')).not.toBeNull();
  });

  it('mostra a imagem padrão quando a capa não carrega', () => {
    const { container } = render(<Cover src={cover} />);

    fireEvent.error(container.querySelector('img')!);

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('.cover svg')).not.toBeNull();
  });
});
