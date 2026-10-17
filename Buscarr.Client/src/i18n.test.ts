import { describe, expect, it, vi } from 'vitest';
import { detectLocale } from './i18n';

describe('detectLocale', () => {
  it('usa português para qualquer variante de pt', () => {
    expect(detectLocale(['pt-BR'])).toBe('pt-BR');
    expect(detectLocale(['pt-PT'])).toBe('pt-BR');
    expect(detectLocale(['pt'])).toBe('pt-BR');
  });

  it('usa inglês para qualquer variante de en', () => {
    expect(detectLocale(['en-US'])).toBe('en');
    expect(detectLocale(['en-GB'])).toBe('en');
  });

  it('respeita a ordem de preferência do navegador', () => {
    expect(detectLocale(['fr-FR', 'pt-BR', 'en-US'])).toBe('pt-BR');
    expect(detectLocale(['de-DE', 'en-US', 'pt-BR'])).toBe('en');
  });

  it('cai no inglês quando nenhum idioma é suportado', () => {
    expect(detectLocale(['fr-FR', 'de-DE'])).toBe('en');
    expect(detectLocale([])).toBe('en');
  });
});

describe('t', () => {
  it('segue o idioma do navegador', async () => {
    vi.stubGlobal('navigator', { languages: ['en-US'] });
    vi.resetModules();

    const { locale, t } = await import('./i18n');

    expect(locale).toBe('en');
    expect(t.search.artistName).toBe('Artist name');
  });
});
