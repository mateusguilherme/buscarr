import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { SettingsPage } from './SettingsPage';
import { mockFetch } from './test-utils';

function renderSettingsPage() {
  render(
    <MemoryRouter initialEntries={['/settings']}>
      <SettingsPage />
    </MemoryRouter>,
  );
}

function radio(name: RegExp) {
  return screen.findByRole('radio', { name }) as Promise<HTMLInputElement>;
}

describe('SettingsPage', () => {
  it('marca a edição que o usuário prefere', async () => {
    mockFetch({ status: 200, body: { edition: 'remastered' } });
    renderSettingsPage();

    expect((await radio(/^A remasterizada/)).checked).toBe(true);
    expect((await radio(/^A original/)).checked).toBe(false);
    expect(screen.getByText('Se o álbum não tiver essa edição, fica a original.')).toBeDefined();
  });

  it('salva a edição escolhida', async () => {
    const fetchMock = mockFetch({ status: 200, body: { edition: 'original' } }, { status: 204 });
    renderSettingsPage();

    await userEvent.click(await radio(/^A deluxe/));

    expect(await screen.findByText('Salvo.')).toBeDefined();
    expect((await radio(/^A deluxe/)).checked).toBe(true);
    expect(fetchMock.mock.calls[1][0]).toBe('/api/settings');
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: 'PUT', body: JSON.stringify({ edition: 'deluxe' }) });
  });

  it('avisa quando não consegue salvar', async () => {
    mockFetch({ status: 200, body: { edition: 'original' } }, { status: 500 });
    renderSettingsPage();

    await userEvent.click(await radio(/^A deluxe/));

    expect((await screen.findByRole('alert')).textContent).toBe('Erro 500 ao salvar as configurações.');
  });

  it('volta para a busca', async () => {
    mockFetch({ status: 200, body: { edition: 'original' } });
    renderSettingsPage();

    await radio(/^A original/);

    expect(screen.getByRole('link', { name: 'Voltar' }).getAttribute('href')).toBe('/');
  });
});
