import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { getSettings, saveSettings, type Settings } from './api';
import { Header } from './Header';
import { ArrowLeftIcon } from './icons';
import { t } from './i18n';
import { editionOptions, qualityOptions } from './options';

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

  async function choose(chosen: Settings) {
    const previous = settings;
    setSettings(chosen);
    setMessage('');
    setError(undefined);

    try {
      await saveSettings(chosen);
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
          <>
            <div className="card settings">
              <Choices
                legend={t.settings.edition}
                name="edition"
                options={editionOptions}
                value={settings.edition}
                onChoose={(edition) => choose({ ...settings, edition })}
              />
              <p className="settings-note">{t.settings.fallback}</p>
            </div>
            <div className="card settings">
              <Choices
                legend={t.settings.quality}
                name="quality"
                options={qualityOptions}
                value={settings.quality}
                onChoose={(quality) => choose({ ...settings, quality })}
              />
            </div>
          </>
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

function Choices<T extends string>({
  legend,
  name,
  options,
  value,
  onChoose,
}: {
  legend: string;
  name: string;
  options: { value: T; label: string; hint: string }[];
  value: T;
  onChoose: (value: T) => void;
}) {
  return (
    <fieldset>
      <legend>{legend}</legend>
      {options.map((option) => (
        <label key={option.value} className="option">
          <input
            type="radio"
            name={name}
            value={option.value}
            checked={value === option.value}
            onChange={() => onChoose(option.value)}
          />
          <span>
            <span className="option-label">{option.label}</span>
            <span className="option-hint">{option.hint}</span>
          </span>
        </label>
      ))}
    </fieldset>
  );
}
