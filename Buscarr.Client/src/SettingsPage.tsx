import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { getSettings, saveSettings, type Edition, type Settings } from './api';
import { Header } from './Header';
import { ArrowLeftIcon } from './icons';
import { t } from './i18n';
import { editionOptions } from './options';

export function SettingsPage() {
  const [settings, setSettings] = useState<Settings | null>(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState<string>();

  useEffect(() => {
    let ignore = false;

    async function loadSettings() {
      try {
        const result = await getSettings();

        if (!ignore) {
          setSettings(result);
        }
      } catch (err) {
        if (!ignore) {
          setError((err as Error).message);
        }
      }
    }

    loadSettings();

    return () => {
      ignore = true;
    };
  }, []);

  async function choose(edition: Edition) {
    const previous = settings;
    setSettings({ edition });
    setMessage('');
    setError(undefined);

    try {
      await saveSettings({ edition });
      setMessage(t.settings.saved);
    } catch (err) {
      setSettings(previous);
      setError((err as Error).message);
    }
  }

  return (
    <>
      <Header />

      <main className="page">
        <Link to="/" className="back">
          <ArrowLeftIcon />
          {t.common.back}
        </Link>

        <h1 className="page-title">{t.settings.title}</h1>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        {!settings && !error && (
          <p className="status" role="status">
            {t.settings.loading}
          </p>
        )}
        {settings && (
          <div className="card settings">
            <fieldset>
              <legend>{t.settings.edition}</legend>
              {editionOptions.map((option) => (
                <label key={option.value} className="option">
                  <input
                    type="radio"
                    name="edition"
                    value={option.value}
                    checked={settings.edition === option.value}
                    onChange={() => choose(option.value)}
                  />
                  <span>
                    <span className="option-label">{option.label}</span>
                    <span className="option-hint">{option.hint}</span>
                  </span>
                </label>
              ))}
            </fieldset>
            <p className="settings-note">{t.settings.fallback}</p>
          </div>
        )}
        {message && (
          <p className="status" role="status">
            {message}
          </p>
        )}
      </main>
    </>
  );
}
