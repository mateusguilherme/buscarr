import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { ListSettings, ReleaseType } from './api';
import { t } from './i18n';
import { releaseTypeOptions } from './options';

export function ListDialog({
  artistName,
  initial,
  listed,
  saving,
  error,
  onSave,
  onRemove,
  onClose,
}: {
  artistName: string;
  initial: ListSettings;
  listed: boolean;
  saving: boolean;
  error?: string;
  onSave: (settings: ListSettings) => void;
  onRemove: () => void;
  onClose: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [releaseTypes, setReleaseTypes] = useState<ReleaseType[]>(initial.releaseTypes);

  useEffect(() => {
    dialog.current?.showModal();
  }, []);

  function toggle(type: ReleaseType) {
    setReleaseTypes((current) =>
      current.includes(type) ? current.filter((value) => value !== type) : [...current, type],
    );
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    onSave({
      releaseTypes: releaseTypeOptions.map((option) => option.value).filter((type) => releaseTypes.includes(type)),
    });
  }

  return (
    <dialog ref={dialog} className="card dialog" aria-labelledby="list-dialog-title" onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <h2 id="list-dialog-title">
          {listed ? t.listDialog.editTitle(artistName) : t.listDialog.addTitle(artistName)}
        </h2>

        <fieldset>
          <legend>{t.listDialog.releases}</legend>
          <div className="checks">
            {releaseTypeOptions.map((option) => (
              <label key={option.value} className="check">
                <input
                  type="checkbox"
                  checked={releaseTypes.includes(option.value)}
                  onChange={() => toggle(option.value)}
                />
                {option.label}
              </label>
            ))}
          </div>
        </fieldset>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}

        <div className="dialog-actions">
          {listed && (
            <button type="button" className="button-ghost button-danger" disabled={saving} onClick={onRemove}>
              {t.listDialog.remove}
            </button>
          )}
          <button type="button" className="button-ghost" onClick={onClose}>
            {t.listDialog.cancel}
          </button>
          <button type="submit" className="button-primary" disabled={saving || releaseTypes.length === 0}>
            {listed ? t.listDialog.save : t.listDialog.add}
          </button>
        </div>
      </form>
    </dialog>
  );
}
