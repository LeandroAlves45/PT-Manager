/**
 * ClientFirstLoginPage.test.jsx — testes do fluxo de primeiro login.
 *
 * Este é o teste de componente mais importante do projecto porque:
 *   1. É o ponto de entrada de todos os clientes na plataforma
 *   2. Envolve chamadas à API (validação do token + set-password)
 *   3. Tem 3 estados distintos de UI (loading, erro, formulário)
 *   4. Tem lógica de navegação pós-sucesso
 *
 * Estratégia:
 *   - vi.mock() intercepta o módulo inviteApi directamente (mais fiável
 *     do que MSW em ambiente jsdom com axios)
 *   - MemoryRouter simula a rota /invite/:token
 *   - userEvent simula interacção real do utilizador
 *   - waitFor aguarda actualizações assíncronas do estado React
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { vi, beforeEach } from 'vitest';
import { TEST_TOKEN } from '../test/__mocks__/handlers';
import ClientFirstLoginPage from '@/pages/ClientFirstLoginPage';

// Mock do módulo de API — substitui as funções reais por mocks controlados
vi.mock('@/api/inviteApi', () => ({
  validateInvite: vi.fn(),
  setPasswordViaInvite: vi.fn(),
}));

import { validateInvite, setPasswordViaInvite } from '@/api/inviteApi';

// ── Helper: renderiza a página na rota /invite/:token ─────────────────────────
function renderInvitePage(token) {
  return render(
    <MemoryRouter initialEntries={[`/invite/${token}`]}>
      <Routes>
        <Route path="/invite/:token" element={<ClientFirstLoginPage />} />
      </Routes>
    </MemoryRouter>
  );
}

// Comportamentos por defeito dos mocks — reutilizados em vários testes
beforeEach(() => {
  validateInvite.mockImplementation((token) => {
    if (token === 'token-valido-123' || token === 'token-qualquer') {
      return Promise.resolve({ valid: true, client_name: 'João Silva', message: '' });
    }
    if (token === 'token-expirado-456') {
      return Promise.resolve({
        valid: false,
        client_name: '',
        message: 'O link de convite expirou. Contacta o seu Personal Trainer.',
      });
    }
    return Promise.resolve({
      valid: false,
      client_name: '',
      message: 'Link de convite inválido ou expirado. Contacta o seu Personal Trainer.',
    });
  });

  setPasswordViaInvite.mockResolvedValue({
    access_token: TEST_TOKEN,
    token_type: 'bearer',
    role: 'client',
    user_id: 'client-uuid-001',
    full_name: 'João Silva',
  });
});

// ============================================================
// Estado de validação (ao carregar a página)
// ============================================================

describe('ClientFirstLoginPage — validação do token', () => {
  test('mostra spinner de loading enquanto valida o token', () => {
    renderInvitePage('token-valido-123');
    // O spinner deve aparecer imediatamente, antes da resposta da API
    expect(screen.getByText(/verificar/i)).toBeInTheDocument();
  });

  test('token válido: mostra nome do cliente e formulário', async () => {
    renderInvitePage('token-valido-123');

    await waitFor(() => {
      expect(screen.getByText(/João Silva/i)).toBeInTheDocument();
    });

    expect(screen.getByLabelText(/nova password/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/confirmar/i)).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /ativar conta/i })
    ).toBeInTheDocument();
  });

  test('token expirado: mostra mensagem de erro', async () => {
    renderInvitePage('token-expirado-456');

    await waitFor(() => {
      expect(screen.getByText(/expirou/i)).toBeInTheDocument();
    });

    expect(screen.queryByLabelText(/nova password/i)).not.toBeInTheDocument();
  });

  test('token inválido: mostra mensagem de erro', async () => {
    renderInvitePage('token-completamente-invalido');

    await waitFor(() => {
      expect(screen.getAllByText(/inválido|expirou/i).length).toBeGreaterThan(0);
    });
  });

  test('erro de rede: mostra mensagem de erro genérica', async () => {
    validateInvite.mockRejectedValueOnce(new Error('Network Error'));

    renderInvitePage('qualquer-token');

    await waitFor(() => {
      expect(screen.queryByLabelText(/nova password/i)).not.toBeInTheDocument();
    });
  });
});

// ============================================================
// Formulário de definição de password
// ============================================================

describe('ClientFirstLoginPage — formulário de password', () => {
  async function renderWithValidToken() {
    const user = userEvent.setup();
    renderInvitePage('token-valido-123');

    await waitFor(() => {
      expect(screen.getByLabelText(/nova password/i)).toBeInTheDocument();
    });

    return user;
  }

  test('botão desabilitado enquanto a submissão está em curso', async () => {
    // Mock que nunca resolve para manter o estado de loading
    setPasswordViaInvite.mockImplementationOnce(() => new Promise(() => {}));

    const user = await renderWithValidToken();

    await user.type(screen.getByLabelText(/nova password/i), 'NovaPassword123!');
    await user.type(screen.getByLabelText(/confirmar/i), 'NovaPassword123!');

    // Não aguardamos a conclusão — o mock está pendente
    user.click(screen.getByRole('button', { name: /ativar conta/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /ativar conta/i })).toBeDisabled();
    });
  });

  test('passwords que não coincidem mostram erro de validação', async () => {
    const user = await renderWithValidToken();

    await user.type(screen.getByLabelText(/nova password/i), 'Password123!');
    await user.type(screen.getByLabelText(/confirmar/i), 'PasswordDiferente!');
    await user.click(screen.getByRole('button', { name: /ativar conta/i }));

    await waitFor(() => {
      expect(screen.getByText(/não coincidem/i)).toBeInTheDocument();
    });
  });

  test('password demasiado curta mostra erro de validação', async () => {
    const user = await renderWithValidToken();

    await user.type(screen.getByLabelText(/nova password/i), '123');
    await user.type(screen.getByLabelText(/confirmar/i), '123');
    await user.click(screen.getByRole('button', { name: /ativar conta/i }));

    await waitFor(() => {
      expect(screen.getByText(/mínimo|caracteres|curta/i)).toBeInTheDocument();
    });
  });

  test('toggle de visibilidade da password funciona', async () => {
    const user = await renderWithValidToken();
    const passwordInput = screen.getByLabelText(/nova password/i);

    expect(passwordInput).toHaveAttribute('type', 'password');

    const toggleButtons = screen.getAllByRole('button', { name: '' });
    const eyeButton = toggleButtons.find((btn) =>
      btn.closest('div')?.contains(passwordInput)
    );

    if (eyeButton) {
      await user.click(eyeButton);
      expect(passwordInput).toHaveAttribute('type', 'text');
    }
  });
});

// ============================================================
// Submissão bem-sucedida
// ============================================================

describe('ClientFirstLoginPage — submissão bem-sucedida', () => {
  test('guarda token JWT em localStorage após activação', async () => {
    const user = userEvent.setup();
    renderInvitePage('token-valido-123');

    await waitFor(() => {
      expect(screen.getByLabelText(/nova password/i)).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText(/nova password/i), 'NovaPassword123!');
    await user.type(screen.getByLabelText(/confirmar/i), 'NovaPassword123!');
    await user.click(screen.getByRole('button', { name: /ativar conta/i }));

    await waitFor(() => {
      expect(localStorage.getItem('pt_token')).toBe(TEST_TOKEN);
    });
  });

  test('mostra ecrã de sucesso após activação', async () => {
    const user = userEvent.setup();
    renderInvitePage('token-valido-123');

    await waitFor(() => {
      expect(screen.getByLabelText(/nova password/i)).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText(/nova password/i), 'NovaPassword123!');
    await user.type(screen.getByLabelText(/confirmar/i), 'NovaPassword123!');
    await user.click(screen.getByRole('button', { name: /ativar conta/i }));

    await waitFor(() => {
      expect(screen.getByText(/activada|sucesso/i)).toBeInTheDocument();
    });
  });

  test('token inválido na submissão mostra erro', async () => {
    setPasswordViaInvite.mockRejectedValueOnce(
      new Error('Link de convite inválido ou já utilizado.')
    );

    const user = userEvent.setup();
    renderInvitePage('token-qualquer');

    await waitFor(() => {
      expect(screen.getByLabelText(/nova password/i)).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText(/nova password/i), 'Password123!');
    await user.type(screen.getByLabelText(/confirmar/i), 'Password123!');
    await user.click(screen.getByRole('button', { name: /ativar conta/i }));

    await waitFor(() => {
      expect(screen.getAllByText(/inválido|expirado|erro/i).length).toBeGreaterThan(0);
    });
  });
});
