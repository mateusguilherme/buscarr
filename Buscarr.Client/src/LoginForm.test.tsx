import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LoginForm } from './LoginForm';
import { mockFetch } from './test-utils';

describe('LoginForm', () => {
  it('mostra a marca, sem slogan', () => {
    render(<LoginForm onLogin={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'buscarr' })).toBeDefined();
    expect(screen.queryByText('Encontre o artista certo.')).toBeNull();
  });

  it('começa no modo de login', () => {
    render(<LoginForm onLogin={vi.fn()} />);

    expect(screen.getByPlaceholderText('E-mail')).toBeDefined();
    expect(screen.getByPlaceholderText('Senha')).toBeDefined();
  });

  it('troca para o cadastro pelo botão Criar usuário, pedindo só e-mail e senha', async () => {
    render(<LoginForm onLogin={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Criar usuário' }));

    expect(screen.getByRole('button', { name: 'Já tenho usuário' })).toBeDefined();
    expect(screen.queryByPlaceholderText('Nome')).toBeNull();
  });

  it('entra e avisa quem logou', async () => {
    const user = { email: 'fulano@exemplo.com' };
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
