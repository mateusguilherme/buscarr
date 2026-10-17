import { useState } from 'react';
import { DiscIcon } from './icons';

export function Cover({ src }: { src: string | null }) {
  const [failed, setFailed] = useState(false);

  return (
    <span className="cover" aria-hidden="true">
      {src && !failed ? <img src={src} alt="" loading="lazy" onError={() => setFailed(true)} /> : <DiscIcon />}
    </span>
  );
}
