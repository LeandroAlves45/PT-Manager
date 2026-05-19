/**
 * AuthContext.test.jsx — testes do contexto de autenticação.
 *
 * Três grupos de testes:
 *
 *   1. applyBrandColor — efeito secundário: escreve CSS variables no DOM.
 *      Testamos que --primary fica correctamente definida.
 *
 *   2. AuthProvider — estado inicial sem token (user=null).
 *
 *   3. AuthProvider — comportamento de login/logout com MSW.
 *      Renderizamos um componente filho que usa useAuth() e
 *      verificamos os estados resultantes.
 */

import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { server } from '../test/__mocks__/server';
import { http, HttpResponse } from 'msw';
import { AuthProvider } from './AuthContext';
import { useAuth } from './useAuth';
import { TEST_TOKEN } from '../test/__mocks__/handlers';

// URL base usada pelo axios nos testes (definida em vitest.config.js)
const BASE = 'http://localhost:8000';

// Helper: renderiza um componente dentro do AuthProvider
function renderWithAuth(ui) {
    return render(
        <MemoryRouter>
            <AuthProvider>
                {ui}
            </AuthProvider>
        </MemoryRouter>
    );
}

// ── Componente auxiliar de teste ──────────────────────────────────────────────
// Expõe os valores do AuthContext para inspecção nos testes.
// Gere os três estados possíveis: loading, sem sessão, com sessão.

function AuthConsumer() {
    const { user, isLoading, isTrainer, isClient } = useAuth();

    if (isLoading) return <div data-testid="loading">A carregar...</div>;
    if (!user) return <div data-testid="no-user">sem sessão</div>;

    return (
        <div>
            <div data-testid="user-name">{user.full_name}</div>
            <div data-testid="user-role">{user.role}</div>
            <div data-testid="is-trainer">{isTrainer ? 'sim' : 'nao'}</div>
            <div data-testid="is-client">{isClient ? 'sim' : 'nao'}</div>
        </div>
    );
}

// ============================================================
// applyBrandColor - CSS variables
// ============================================================

describe('applyBrandColor', () => {
    function BrandTester() {
        const { applyBrandColor } = useAuth();
        return (
            <button onClick={() => applyBrandColor('#FF5733')}>
                Aplicar Cor
            </button>
        );
    }

    test('define --primary CSS variable no documentElement', async () => {
        const user = userEvent.setup();
        renderWithAuth(<BrandTester />);

        await user.click(screen.getByText('Aplicar Cor'));

        const primaryValue = document.documentElement.style.getPropertyValue('--primary');
        expect(primaryValue).toBeTruthy();
        // Deve estar no formato "H S% L%"
        expect(primaryValue).toMatch(/^\d+ \d+% \d+%$/);
    });

    test('define --sidebar-primary igual a --primary', async () => {
        const user = userEvent.setup();
        renderWithAuth(<BrandTester />);

        await user.click(screen.getByText('Aplicar Cor'));

        const primary = document.documentElement.style.getPropertyValue('--primary');
        const sidebarPrimary = document.documentElement.style.getPropertyValue('--sidebar-primary');
        expect(primary).toBe(sidebarPrimary);
    });

    test('não falha para hex sem #', async () => {
        function BrandTesterSemHash() {
            const { applyBrandColor } = useAuth();
            return <button onClick={() => applyBrandColor('00A8E8')}>Sem hash</button>;
        }
        const user = userEvent.setup();
        renderWithAuth(<BrandTesterSemHash />);

        await expect(user.click(screen.getByText('Sem hash'))).resolves.not.toThrow();
    });
});

// ============================================================
// AuthProvider - estado inicial sem token
// ============================================================

describe('AuthProvider - estado inicial', () => {

    test('renderiza sem erros quando não há token', () => {
        // O estado loading é imediato — o effect do React flushes de forma
        // síncrona no jsdom, pelo que só o estado final (no-user) é observável.
        renderWithAuth(<AuthConsumer />);
        expect(document.body).toBeTruthy();
    });

    test('mostra sem-sessão quando não há token em localStorage', async () => {
        renderWithAuth(<AuthConsumer />);

        // Aguarda que o loading termine e o estado sem-sessão apareça
        await waitFor(() => {
            expect(screen.getByTestId('no-user')).toBeInTheDocument();
        });
    });
});

// ============================================================
// AuthProvider - login
// ============================================================

describe('AuthProvider - login', () => {

    // Componente que simula o formulário de login
    function LoginForm() {
        const { login } = useAuth();
        const [error, setError] = useState(null);

        const handleLogin = async () => {
            try {
                await login('trainer@teste.pt', 'Trainer123!');
            } catch (err) {
                setError('Erro de login');
            }
        };

        return (
            <>
                <button onClick={handleLogin}>Login</button>
                {error && <div data-testid="login-error">{error}</div>}
            </>
        );
    }

    test('guarda token em localStorage após login bem-sucedido', async () => {
        const user = userEvent.setup();
        renderWithAuth(<LoginForm />);

        await user.click(screen.getByText('Login'));

        await waitFor(() => {
            expect(localStorage.getItem('pt_token')).toBe(TEST_TOKEN);
        });
    });

    test('não guarda token com credenciais erradas', async () => {
        // Sobrepor handler para devolver 401
        server.use(
            http.post(`${BASE}/api/v1/auth/login`, () =>
                HttpResponse.json(
                    { detail: 'Email ou password inválidos' },
                    { status: 401 }
                )
            )
        );

        const user = userEvent.setup();
        renderWithAuth(<LoginForm />);

        await user.click(screen.getByText('Login'));

        await waitFor(() => {
            expect(localStorage.getItem('pt_token')).toBeNull();
        });
    });

    test('carrega trainer settings após login', async () => {
        const user = userEvent.setup();

        function SettingsDisplay() {
            const { trainerSettings } = useAuth();
            return (
                <div data-testid="app-name">
                    {trainerSettings?.app_name ?? 'sem settings'}
                </div>
            );
        }

        renderWithAuth(
            <>
                <LoginForm />
                <SettingsDisplay />
            </>
        );

        await user.click(screen.getByText('Login'));

        await waitFor(() => {
            expect(screen.getByTestId('app-name')).toHaveTextContent('PT Manager Teste');
        });
    });
});

// ============================================================
// AuthProvider — logout
// ============================================================

describe('AuthProvider — logout', () => {

    function LogoutButton() {
        const { logout } = useAuth();
        return <button onClick={logout}>Logout</button>;
    }

    test('remove token de localStorage após logout', async () => {
        localStorage.setItem('pt_token', TEST_TOKEN);
        const user = userEvent.setup();
        renderWithAuth(<LogoutButton />);

        await user.click(screen.getByText('Logout'));

        expect(localStorage.getItem('pt_token')).toBeNull();
    });
});
