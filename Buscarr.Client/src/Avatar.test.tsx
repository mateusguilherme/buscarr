import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Avatar } from './Avatar';

const photo = 'https://assets.fanart.tv/preview/music/abc-123/artistthumb/nirvana.jpg';

describe('Avatar', () => {
  it('mostra a foto quando há uma', () => {
    const { container } = render(<Avatar image={photo} />);

    expect(container.querySelector('img')?.getAttribute('src')).toBe(photo);
    expect(container.querySelector('svg')).toBeNull();
  });

  it('mostra a imagem padrão quando não há foto', () => {
    const { container } = render(<Avatar image={null} />);

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('.avatar svg')).not.toBeNull();
  });

  it('mostra a imagem padrão quando a foto não carrega', () => {
    const { container } = render(<Avatar image={photo} />);

    fireEvent.error(container.querySelector('img')!);

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('.avatar svg')).not.toBeNull();
  });
});
