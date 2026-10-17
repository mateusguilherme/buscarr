import { useState } from 'react';
import { addToList, getListEntry, removeFromList, type Artist, type ListSettings } from './api';
import { Avatar } from './Avatar';
import { ListDialog } from './ListDialog';
import { PinIcon } from './icons';
import { t } from './i18n';

export function ArtistItem({ artist, onRemoved }: { artist: Artist; onRemoved?: () => void }) {
  const [listed, setListed] = useState(artist.listed);
  const [dialog, setDialog] = useState<ListSettings | null>(null);
  const [error, setError] = useState<string>();
  const [dialogError, setDialogError] = useState<string>();
  const [saving, setSaving] = useState(false);

  async function openDialog() {
    setError(undefined);
    setDialogError(undefined);

    if (!listed) {
      setDialog({ releaseTypes: ['album'] });

      return;
    }

    setSaving(true);

    try {
      setDialog(await getListEntry(artist.id));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  }

  async function saveListing(settings: ListSettings) {
    await changeListing(() => addToList(artist.id, settings), true);
  }

  async function removeListing() {
    await changeListing(() => removeFromList(artist.id), false);
  }

  async function changeListing(change: () => Promise<void>, nowListed: boolean) {
    setDialogError(undefined);
    setSaving(true);

    try {
      await change();
      setListed(nowListed);
      setDialog(null);

      if (!nowListed) {
        onRemoved?.();
      }
    } catch (err) {
      setDialogError((err as Error).message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <li className="card artist-card">
      <Avatar image={artist.image} />
      <div className="artist-info">
        <span className="artist-name">{artist.name}</span>
        {(artist.disambiguation || artist.area) && (
          <div className="artist-meta">
            {artist.disambiguation && <span>{artist.disambiguation}</span>}
            {artist.area && (
              <span className="chip">
                <PinIcon />
                {artist.area}
              </span>
            )}
          </div>
        )}
        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
      </div>
      <button
        className={listed ? 'button-ghost button-small' : 'button-primary button-small'}
        aria-pressed={listed}
        disabled={saving}
        onClick={openDialog}
      >
        {listed ? t.artist.inList : t.artist.addToList}
      </button>

      {dialog && (
        <ListDialog
          artistName={artist.name}
          initial={dialog}
          listed={listed}
          saving={saving}
          error={dialogError}
          onSave={saveListing}
          onRemove={removeListing}
          onClose={() => setDialog(null)}
        />
      )}
    </li>
  );
}
