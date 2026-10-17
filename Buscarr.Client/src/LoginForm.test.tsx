import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LoginForm } from './LoginForm';
import { mockFetch } from './test-utils';

describe('LoginForm', () => {
  it('começa no modo de login, sem o campo de nome', () => {
    render(<LoginForm onLogin={vi.fn()} />);

    expect(screen.getByPlaceholderText('E-mail')).toBeDefined();
    expect(screen.getByPlaceholderText('Senha')).toBeDefined();
    expect(screen.queryByPlaceholderText('Nome')).toBeNull();
  });

  it('troca para o cadastro pelo botão Criar usuário', async () => {
    render(<LoginForm onLogin={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Criar usuário' }));

    expect(screen.getByPlaceholderText('Nome')).toBeDefined();
    expect(screen.getByRole('button', { name: 'Já tenho usuário' })).toBeDefined();
  });

  it('entra e avisa quem logou', async () => {
    const user = { email: 'fulano@exemplo.com', name: 'Fulano' };
    mockFetch({ status: 200, body: user });
    const onLogin = vi.fn();
    render(<LoginForm onLogin={onLogin} />);

    await userEvent.type(screen.getByPlaceholderText('E-mail'), user.email);
    await userEvent.type(screen.getByPlaceholderText('Senha'), 'segredo123');
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(onLogin).toHaveBeenCalledWith(user);
  });

  it('mostra o erro quando a senha está errada', async () => {
    mockFetch({ status: 401 });
    const onLogin = vi.fn();
    render(<LoginForm onLogin={onLogin} />);

    await userEvent.type(screen.getByPlaceholderText('E-mail'), 'fulano@exemplo.com');
    await userEvent.type(screen.getByPlaceholderText('Senha'), 'errada123');
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByText('E-mail ou senha incorretos.')).toBeDefined();
    expect(onLogin).not.toHaveBeenCalled();
  });
});
