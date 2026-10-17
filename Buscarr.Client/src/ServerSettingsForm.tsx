import { useId, useState, type FormEvent, type ReactNode } from 'react';
import {
  getServerStatus,
  saveServerSettings,
  ServerSettingsError,
  type ConnectionStatus,
  type ServerSettings,
  type ServerStatus,
} from './api';
import { t } from './i18n';

type TextField = 'slskdUrl' | 'downloadsPath' | 'libraryPath' | 'rejectedPath' | 'subsonicUrl' | 'subsonicUser';
type SecretField = 'slskdApiKey' | 'subsonicPassword' | 'fanartTvApiKey';
type Secret = { value: string; saved: boolean; cleared: boolean };

const savedFlags: Record<SecretField, 'hasSlskdApiKey' | 'hasSubsonicPassword' | 'hasFanartTvApiKey'> = {
  slskdApiKey: 'hasSlskdApiKey',
  subsonicPassword: 'hasSubsonicPassword',
  fanartTvApiKey: 'hasFanartTvApiKey',
};

function valuesFrom(settings: ServerSettings): Record<TextField, string> {
  return {
    slskdUrl: settings.slskdUrl,
    downloadsPath: settings.downloadsPath,
    libraryPath: settings.libraryPath,
    rejectedPath: settings.rejectedPath,
    subsonicUrl: settings.subsonicUrl,
    subsonicUser: settings.subsonicUser,
  };
}

function secretsFrom(settings: ServerSettings): Record<SecretField, Secret> {
  return {
    slskdApiKey: { value: '', saved: settings.hasSlskdApiKey, cleared: false },
    subsonicPassword: { value: '', saved: settings.hasSubsonicPassword, cleared: false },
    fanartTvApiKey: { value: '', saved: settings.hasFanartTvApiKey, cleared: false },
  };
}

function secretUpdate(secret: Secret): string | null {
  if (secret.value) {
    return secret.value;
  }

  return secret.cleared ? '' : null;
}

function describe(name: 'slskdStatus' | 'subsonicStatus', status: ConnectionStatus) {
  const texts = t.server[name];

  return status.state === 'error' ? texts.error(status.message ?? '') : texts[status.state];
}

export function ServerSettingsForm({ initial }: { initial: ServerSettings }) {
  const [values, setValues] = useState(() => valuesFrom(initial));
  const [savedValues, setSavedValues] = useState(() => valuesFrom(initial));
  const [secrets, setSecrets] = useState(() => secretsFrom(initial));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState<'saving' | 'testing'>();
  const [message, setMessage] = useState('');
  const [alert, setAlert] = useState<string>();
  const [status, setStatus] = useState<ServerStatus>();
  const dirty =
    (Object.keys(values) as TextField[]).some((field) => values[field] !== savedValues[field]) ||
    Object.values(secrets).some((secret) => secret.value !== '' || secret.cleared);

  function change(field: TextField, value: string) {
    setValues((current) => ({ ...current, [field]: value }));
  }

  function changeSecret(field: SecretField, change: Partial<Secret>) {
    setSecrets((current) => ({ ...current, [field]: { ...current[field], ...change } }));
  }

  async function test() {
    setBusy('testing');
    setAlert(undefined);

    try {
      setStatus(await getServerStatus());
    } catch (err) {
      setAlert((err as Error).message);
    } finally {
      setBusy(undefined);
    }
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    setBusy('saving');
    setMessage('');
    setAlert(undefined);
    setErrors({});
    setStatus(undefined);

    try {
      await saveServerSettings({
        ...values,
        slskdApiKey: secretUpdate(secrets.slskdApiKey),
        subsonicPassword: secretUpdate(secrets.subsonicPassword),
        fanartTvApiKey: secretUpdate(secrets.fanartTvApiKey),
      });
    } catch (err) {
      if (err instanceof ServerSettingsError) {
        setErrors(err.errors);
      } else {
        setAlert((err as Error).message);
      }

      setBusy(undefined);

      return;
    }

    setSecrets((current) => {
      const next = { ...current };

      for (const field of Object.keys(savedFlags) as SecretField[]) {
        const secret = current[field];
        next[field] = { value: '', saved: secret.value !== '' || (secret.saved && !secret.cleared), cleared: false };
      }

      return next;
    });
    setSavedValues(values);
    setMessage(t.server.saved);
    await test();
  }

  function text(field: TextField, label: string, hint?: string) {
    return (
      <Field label={label} hint={hint} error={errors[field]}>
        {(id) => <input id={id} value={values[field]} onChange={(e) => change(field, e.target.value)} />}
      </Field>
    );
  }

  function secret(field: SecretField, label: string, name: string) {
    const current = secrets[field];
    const kept = current.saved && !current.cleared;

    return (
      <Field label={label} error={errors[field]}>
        {(id) => (
          <div className="secret">
            <input
              id={id}
              type="password"
              autoComplete="new-password"
              placeholder={kept ? t.server.secretKept : ''}
              value={current.value}
              onChange={(e) => changeSecret(field, { value: e.target.value })}
            />
            {kept && (
              <button
                type="button"
                className="button-ghost button-small"
                aria-label={t.server.clear(name)}
                onClick={() => changeSecret(field, { cleared: true, value: '' })}
              >
                ✕
              </button>
            )}
          </div>
        )}
      </Field>
    );
  }

  return (
    <section className="server">
      <h2 className="section-title">{t.server.title}</h2>
      <p className="settings-note">{t.server.intro}</p>

      <form className="server-form" onSubmit={save}>
        <div className="card settings">
          <fieldset>
            <legend>{t.server.slskd}</legend>
            {text('slskdUrl', t.server.slskdUrl)}
            {secret('slskdApiKey', t.server.slskdApiKey, t.server.slskdApiKeyName)}
          </fieldset>
        </div>

        <div className="card settings">
          <fieldset>
            <legend>{t.server.folders}</legend>
            {text('downloadsPath', t.server.downloadsPath, t.server.downloadsPathHint)}
            {text('libraryPath', t.server.libraryPath, t.server.libraryPathHint)}
            {text('rejectedPath', t.server.rejectedPath, t.server.rejectedPathHint)}
          </fieldset>
        </div>

        <div className="card settings">
          <fieldset>
            <legend>{t.server.subsonic}</legend>
            <p className="settings-note">{t.server.subsonicHint}</p>
            {text('subsonicUrl', t.server.subsonicUrl)}
            {text('subsonicUser', t.server.subsonicUser)}
            {secret('subsonicPassword', t.server.subsonicPassword, t.server.subsonicPasswordName)}
          </fieldset>
        </div>

        <div className="card settings">
          <fieldset>
            <legend>{t.server.fanartTv}</legend>
            {secret('fanartTvApiKey', t.server.fanartTvApiKey, t.server.fanartTvApiKeyName)}
          </fieldset>
        </div>

        {alert && (
          <p className="alert" role="alert">
            {alert}
          </p>
        )}

        <div className="server-actions">
          <button className="button-primary" disabled={busy !== undefined}>
            {busy === 'saving' ? t.server.saving : t.server.save}
          </button>
          <button type="button" className="button-ghost" disabled={busy !== undefined || dirty} onClick={test}>
            {busy === 'testing' ? t.server.testing : t.server.test}
          </button>
          {dirty && <span className="field-hint">{t.server.saveToTest}</span>}
        </div>

        {message && (
          <p className="status" role="status">
            {message}
          </p>
        )}
        {status && (
          <ul className="server-status">
            <li data-state={status.slskd.state}>{describe('slskdStatus', status.slskd)}</li>
            <li data-state={status.subsonic.state}>{describe('subsonicStatus', status.subsonic)}</li>
          </ul>
        )}
      </form>
    </section>
  );
}

function Field({
  label,
  hint,
  error,
  children,
}: {
  label: string;
  hint?: string;
  error?: string;
  children: (id: string) => ReactNode;
}) {
  const id = useId();

  return (
    <div className="field">
      <label htmlFor={id} className="field-label">
        {label}
      </label>
      {children(id)}
      {hint && <span className="field-hint">{hint}</span>}
      {error && <span className="field-error">{t.server.errors[error] ?? error}</span>}
    </div>
  );
}
