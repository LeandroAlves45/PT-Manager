import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { StrictMode } from 'react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthProvider } from '@/app/providers/AuthProvider';
import { ThemeProvider } from '@/app/providers/ThemeProvider';
import { routes } from '@/app/router';
import { resetConfirmEmailRequests } from '@/features/auth/lib/confirm-email';
import { API, problem } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { createTestQueryClient, renderApp } from '@/test/render';

beforeEach(() => resetConfirmEmailRequests());

function answerWith(status: number, code: string) {
  server.use(
    http.post(`${API}/auth/confirm-email`, () => HttpResponse.json(problem(code), { status }))
  );
}

describe('ConfirmEmailPage', () => {
  it('confirms once under StrictMode (the token is single use) and goes to login with a notice', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post(`${API}/auth/confirm-email`, async ({ request }) => {
        bodies.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      })
    );
    const router = createMemoryRouter(routes, { initialEntries: ['/confirmar-email?token=abc'] });

    render(
      <StrictMode>
        <ThemeProvider>
          <QueryClientProvider client={createTestQueryClient()}>
            <AuthProvider>
              <RouterProvider router={router} />
            </AuthProvider>
          </QueryClientProvider>
        </ThemeProvider>
      </StrictMode>
    );

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
    expect(bodies).toEqual([{ token: 'abc' }]);
    expect(await screen.findByText('Email confirmado. Já podes entrar.')).toBeInTheDocument();
  });

  it('shows the invalid link screen when there is no token', async () => {
    renderApp({ initialEntries: ['/confirmar-email'] });

    expect(await screen.findByRole('heading', { name: 'Link inválido' })).toHaveFocus();
    expect(screen.getByRole('link', { name: 'Pedir um novo email' })).toHaveAttribute(
      'href',
      '/reenviar-confirmacao'
    );
  });

  it.each([
    ['authentication_confirmation_token_invalid', 401, 'O link de confirmação não é válido.'],
    ['authentication_confirmation_token_expired', 401, 'O link de confirmação expirou.'],
  ])('explains %s and offers a new email', async (code, status, message) => {
    answerWith(status, code);
    renderApp({ initialEntries: ['/confirmar-email?token=abc'] });

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(screen.getByRole('link', { name: 'Pedir um novo email' })).toHaveAttribute(
      'href',
      '/reenviar-confirmacao'
    );
  });

  it('tells the user the email was already confirmed and offers login', async () => {
    answerWith(409, 'authentication_confirmation_token_consumed');
    renderApp({ initialEntries: ['/confirmar-email?token=abc'] });

    expect(await screen.findByRole('heading', { name: 'Email já confirmado' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', '/entrar');
  });

  it('explains an inactive account', async () => {
    answerWith(403, 'authentication_account_inactive');
    renderApp({ initialEntries: ['/confirmar-email?token=abc'] });

    expect(await screen.findByRole('alert')).toHaveTextContent('Esta conta está desativada.');
  });
});
