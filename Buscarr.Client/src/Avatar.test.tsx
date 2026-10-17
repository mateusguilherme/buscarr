import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Avatar } from './Avatar';

const photo = 'https://assets.fanart.tv/preview/music/abc-123/artistthumb/nirvana.jpg';

describe('Avatar', () => {
  it('mostra a foto quando há uma', () => {
    const { container } = render(<Avatar image={photo} />);

    expect(container.querySelector('img')?.getAttribute('src')).toBe(photo);
  });

  it('fica vazio quando não há foto', () => {
    const { container } = render(<Avatar image={null} />);

    expect(container.querySelector('.avatar')).not.toBeNull();
    expect(container.querySelector('img')).toBeNull();
  });

  it('fica vazio quando a foto não carrega', () => {
    const { container } = render(<Avatar image={photo} />);

    fireEvent.error(container.querySelector('img')!);

    expect(container.querySelector('img')).toBeNull();
  });
});
