import type { Edition, ReleaseType } from './api';
import { t } from './i18n';

export const editionOptions: { value: Edition; label: string; hint: string }[] = [
  { value: 'original', label: t.settings.original, hint: t.settings.originalHint },
  { value: 'remastered', label: t.settings.remastered, hint: t.settings.remasteredHint },
  { value: 'deluxe', label: t.settings.deluxe, hint: t.settings.deluxeHint },
];

export const releaseTypeOptions: { value: ReleaseType; label: string }[] = [
  { value: 'album', label: t.listDialog.albums },
  { value: 'ep', label: t.listDialog.eps },
  { value: 'single', label: t.listDialog.singles },
  { value: 'live', label: t.listDialog.live },
  { value: 'compilation', label: t.listDialog.compilations },
  { value: 'soundtrack', label: t.listDialog.soundtracks },
  { value: 'remix', label: t.listDialog.remixes },
  { value: 'demo', label: t.listDialog.demos },
];
