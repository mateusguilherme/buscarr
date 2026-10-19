import { useEffect, useState, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router';
import { logout } from './api';
import { LogoMark, MenuIcon } from './icons';
import { t } from './i18n';

export function Brand() {
  return (
    <>
      <LogoMark />
      buscarr
    </>
  );
}

export function Header({
  title,
  onLogout,
  children,
}: {
  title?: string;
  onLogout?: () => void;
  children?: ReactNode;
}) {
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!menuOpen) {
      return;
    }

    function close(e: Event) {
      if (!(e instanceof KeyboardEvent) || e.key === 'Escape') {
        setMenuOpen(false);
      }
    }

    document.addEventListener('click', close);
    document.addEventListener('keydown', close);

    return () => {
      document.removeEventListener('click', close);
      document.removeEventListener('keydown', close);
    };
  }, [menuOpen]);

  async function handleLogout() {
    setError(undefined);

    try {
      await logout();
      setMenuOpen(false);

      if (onLogout) {
        onLogout();
      } else {
        navigate('/');
      }
    } catch (err) {
      setError((err as Error).message);
    }
  }

  return (
    <header className="header">
      <div className="header-inner">
        <Link to="/" className="brand" aria-label="buscarr">
          <LogoMark />
        </Link>
        {title && (
          <h1 className="header-title" title={title}>
            {title}
          </h1>
        )}
        {children && <div className="header-actions">{children}</div>}
        <div className="menu">
          <button
            className="button-ghost button-small menu-button"
            aria-label={t.search.menu}
            aria-expanded={menuOpen}
            onClick={(e) => {
              e.stopPropagation();
              setMenuOpen((current) => !current);
            }}
          >
            <MenuIcon />
          </button>
          {menuOpen && (
            <nav className="menu-list">
              <Link to="/downloads" className="menu-item">
                {t.search.downloads}
              </Link>
              <Link to="/settings" className="menu-item">
                {t.search.settings}
              </Link>
              <button
                className="menu-item"
                onClick={(e) => {
                  e.stopPropagation();
                  handleLogout();
                }}
              >
                {t.search.logout}
              </button>
              {error && (
                <p className="alert" role="alert">
                  {error}
                </p>
              )}
            </nav>
          )}
        </div>
      </div>
    </header>
  );
}
