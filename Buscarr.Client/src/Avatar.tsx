import { useState } from 'react';
import { ArtistIcon } from './icons';

export function Avatar({ image }: { image: string | null }) {
  const [failed, setFailed] = useState(false);

  return (
    <span className="avatar" aria-hidden="true">
      {image && !failed ? (
        <img src={image} alt="" loading="lazy" onError={() => setFailed(true)} />
      ) : (
        <ArtistIcon />
      )}
    </span>
  );
}
