import { useState } from 'react';
import { ArtistIcon } from './icons';

export function Avatar({ image, large = false }: { image: string | null; large?: boolean }) {
  const [failed, setFailed] = useState(false);

  return (
    <span className={large ? 'avatar avatar-large' : 'avatar'} aria-hidden="true">
      {image && !failed ? (
        <img src={image} alt="" loading="lazy" onError={() => setFailed(true)} />
      ) : (
        <ArtistIcon />
      )}
    </span>
  );
}
