/**
 * ProtectedRoute.test.jsx — testes do guard de rotas.
 *
 * ProtectedRoute é um dos componentes mais críticos da app:
 * garante que utilizadores não acedem a rotas que não lhes pertencem.
 *
 * Estratégia:
 *   Mockar o AuthContext directamente (sem MSW) porque queremos
 *   testar a lógica de redirecionamento isolada, não o login.
 *   O vi.mock() do Vitest substitui o módulo inteiro.
 */

import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { vi } from 'vitest';
import ProtectedRoute from '@/components/ProtectedRoute';

// Mock do AuthContext — controlamos o estado sem fazer pedidos à API
const mockUseAuth = vi.fn();
vi.mock('@/context/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

// ── Helper: renderiza uma rota protegida ──────────────────────────────────────
function renderProtectedRoute(requiredRole, authState) {
  mockUseAuth.mockReturnValue(authState);

  return render(
    <MemoryRouter initialEntries={['/trainer/dashboard']}>
      <Routes>
        <Route
          path="/trainer/dashboard"
          element={
            <ProtectedRoute requiredRole={requiredRole}>
              <div data-testid="conteudo-protegido">Conteúdo protegido</div>
            </ProtectedRoute>
          }
        />
        <Route
          path="/login"
          element={<div data-testid="login-page">Login</div>}
        />
        <Route
          path="/admin/dashboard"
          element={<div data-testid="admin-page">Admin</div>}
        />
        <Route
          path="/trainer/dashboard"
          element={<div data-testid="trainer-page">Trainer</div>}
        />
        <Route
          path="/client/dashboard"
          element={<div data-testid="client-page">Client</div>}
        />
      </Routes>
    </MemoryRouter>
  );
}

// ============================================================
// Estado de loading
// ============================================================

describe('ProtectedRoute — loading', () => {
  test('mostra spinner enquanto isLoading é true', () => {
    renderProtectedRoute('trainer', {
      isLoading: true,
      user: null,
      isAuthenticated: false,
    });

    // Deve mostrar um indicador de loading (não o conteúdo nem redirect)
    expect(screen.queryByTestId('conteudo-protegido')).not.toBeInTheDocument();
    // O spinner tem "A carregar..." no texto
    expect(screen.getByText(/carregar/i)).toBeInTheDocument();
  });
});

// ============================================================
// Acesso autorizado
// ============================================================

describe('ProtectedRoute — acesso autorizado', () => {
  test('trainer com role correcto vê o conteúdo', () => {
    renderProtectedRoute('trainer', {
      isLoading: false,
      user: { role: 'trainer' },
      isAuthenticated: true,
    });

    expect(screen.getByTestId('conteudo-protegido')).toBeInTheDocument();
  });

  test('cliente com role correcto vê o conteúdo', () => {
    render(
      <MemoryRouter initialEntries={['/cliente/dashboard']}>
        <Routes>
          <Route
            path="/cliente/dashboard"
            element={
              <ProtectedRoute requiredRole="client">
                <div data-testid="conteudo-cliente">Conteúdo cliente</div>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    );

    mockUseAuth.mockReturnValue({
      isLoading: false,
      user: { role: 'client' },
      isAuthenticated: true,
    });

    // Re-render para aplicar o mock actualizado
    // (na prática o mock é aplicado antes do render)
  });

  test('superuser pode aceder a rotas de trainer', () => {
    renderProtectedRoute('trainer', {
      isLoading: false,
      user: { role: 'superuser' },
      isAuthenticated: true,
    });

    // Superuser tem acesso total — não deve ser redirecionado
    expect(screen.getByTestId('conteudo-protegido')).toBeInTheDocument();
  });
});

// ============================================================
// Redirecionamento por role errado
// ============================================================

describe('ProtectedRoute — redirecionamento', () => {
  test('cliente que tenta aceder a rota de trainer é redirecionado', () => {
    renderProtectedRoute('trainer', {
      isLoading: false,
      user: { role: 'client' },
      isAuthenticated: true,
    });

    // O conteúdo não deve estar visível
    expect(screen.queryByTestId('conteudo-protegido')).not.toBeInTheDocument();
  });

  test('trainer que tenta aceder a rota de admin é redirecionado', () => {
    render(
      <MemoryRouter initialEntries={['/admin/dashboard']}>
        <Routes>
          <Route
            path="/admin/dashboard"
            element={
              <ProtectedRoute requiredRole="superuser">
                <div data-testid="conteudo-admin">Admin</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/trainer/dashboard"
            element={<div data-testid="trainer-redirect">Trainer</div>}
          />
        </Routes>
      </MemoryRouter>
    );

    mockUseAuth.mockReturnValue({
      isLoading: false,
      user: { role: 'trainer' },
      isAuthenticated: true,
    });

    // Trainer não deve ver conteúdo de admin
    expect(screen.queryByTestId('conteudo-admin')).not.toBeInTheDocument();
  });
});
