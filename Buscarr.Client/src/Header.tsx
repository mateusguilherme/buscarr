import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { LogoMark } from './icons';

export function Brand() {
  return (
    <>
      <LogoMark />
      buscarr
    </>
  );
}

export function Header({ children }: { children?: ReactNode }) {
  return (
    <header className="header">
      <div className="header-inner">
        <Link to="/" className="brand">
          <Brand />
        </Link>
        {children && <div className="header-actions">{children}</div>}
      </div>
    </header>
  );
}
