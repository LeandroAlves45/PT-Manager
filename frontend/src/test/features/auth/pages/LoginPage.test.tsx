import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { installGoogleFake } from '@/test/google-fake';
import { API, pendingRestore, problem, sessionResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

function openLogin(state: Record<string, unknown> | null = null) {
  return renderApp({ initialEntries: [{ pathname: '/entrar', state }] });
}

function openLoginFrom(from?: string) {
  return openLogin(from === undefined ? null : { from });
}

async function submit(email = 'ana@ptmanager.test', password = 'correct-horse-1') {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Palavra-passe'), password);
  await user.click(screen.getByRole('button', { name: 'Entrar' }));
}

/** Conta os pedidos de login para provar que não há repetição automática. */
function useLoginAnswer(status: number, body: Record<string, unknown>) {
  const calls = { count: 0 };
  server.use(
    http.post(`${API}/auth/login`, () => {
      calls.count += 1;
      return HttpResponse.json(body, { status });
    })
  );
  return calls;
}

describe('LoginPage', () => {
  it('keeps the form disabled while the session is restoring', async () => {
    const restore = pendingRestore();
    server.use(...restore.handlers);

    openLogin();

    expect(await screen.findByRole('button', { name: 'A entrar…' })).toBeDisabled();

    restore.release();
    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeEnabled();
  });

  it('offers the way to recover the password, resend the confirmation and sign up', async () => {
    const user = userEvent.setup();
    const { router } = openLogin();

    expect(await screen.findByRole('link', { name: 'Criar conta' })).toHaveAttribute(
      'href',
      '/criar-conta'
    );
    expect(screen.getByRole('link', { name: 'Esqueceste a palavra-passe?' })).toHaveAttribute(
      'href',
      '/recuperar-password'
    );

    await user.click(screen.getByRole('link', { name: 'Esqueceste a palavra-passe?' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/recuperar-password'));
  });

  it('carries the typed email to the resend page in state, never in the URL', async () => {
    const user = userEvent.setup();
    const { router } = openLogin();

    await user.type(await screen.findByLabelText('Email'), 'ana@ptmanager.test');
    await user.click(screen.getByRole('link', { name: 'Não recebeste o email de confirmação?' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/reenviar-confirmacao'));
    expect(router.state.location.search).toBe('');
    expect(router.state.location.state).toEqual({ email: 'ana@ptmanager.test' });
    expect(await screen.findByLabelText('Email')).toHaveValue('ana@ptmanager.test');
  });

  it('shows the notice left by a previous screen', async () => {
    openLogin({ notice: 'Palavra-passe alterada. Entra com a nova.' });

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Palavra-passe alterada. Entra com a nova.'
    );
  });

  it('toggles the password visibility without losing what was typed', async () => {
    const user = userEvent.setup();
    openLogin();

    const input = await screen.findByLabelText('Palavra-passe');
    await user.type(input, 'segredo');
    const toggle = screen.getByRole('button', { name: 'Mostrar palavra-passe' });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    expect(input).toHaveAttribute('type', 'password');

    await user.click(toggle);

    expect(toggle).toHaveAttribute('aria-pressed', 'true');
    expect(input).toHaveAttribute('type', 'text');
    expect(input).toHaveValue('segredo');
  });

  it('returns to the saved internal destination, query string included', async () => {
    const { router } = openLoginFrom('/trainer/clients?search=ana&page=2');

    await submit();

    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer/clients'));
    expect(router.state.location.search).toBe('?search=ana&page=2');
  });

  it('goes to the home of the returned role when nothing was saved', async () => {
    server.use(
      http.post(`${API}/auth/login`, () =>
        HttpResponse.json(sessionResponse({ role: 'client', trainer_id: null }))
      )
    );
    const { router } = openLogin();

    await submit();

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal'));
  });

  it.each(['//evil.example', 'https://evil.example/phish', '/\\evil.example', 'trainer'])(
    'ignores the external or malformed destination %s',
    async (from) => {
      const { router } = openLoginFrom(from);

      await submit();

      await waitFor(() => expect(router.state.location.pathname).toBe('/trainer'));
    }
  );

  it('shows server validation errors on the matching field', async () => {
    useLoginAnswer(
      400,
      problem('validation_failed', {
        errors: [{ field: 'email', code: 'email_invalid', message: 'Este email não é aceite.' }],
      })
    );
    openLogin();

    await submit();

    expect(await screen.findByText('Este email não é aceite.')).toBeInTheDocument();
  });

  it('answers a 401 with a message that does not reveal whether the account exists', async () => {
    useLoginAnswer(401, problem('authentication_invalid_credentials'));
    openLogin();

    await submit();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Email ou palavra-passe inválidos.');
    expect(alert).toHaveTextContent('confirma primeiro o email');
    expect(alert).not.toHaveTextContent(/ana@ptmanager\.test|não existe/i);
  });

  it('answers the real 429 body (a natural-language title) without retrying', async () => {
    const calls = useLoginAnswer(429, { title: 'Too many requests', status: 429 });
    openLogin();

    await submit();

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiadas tentativas.');
    expect(calls.count).toBe(1);
  });

  it('does not ask for a session refresh when login itself answers 401', async () => {
    const refreshes = vi.fn();
    server.use(
      http.post(`${API}/auth/refresh`, () => {
        refreshes();
        return HttpResponse.json(problem('authentication_refresh_token_required'), {
          status: 401,
        });
      })
    );
    useLoginAnswer(401, problem('authentication_invalid_credentials'));
    openLogin();
    await screen.findByRole('button', { name: 'Entrar' });
    refreshes.mockClear();

    await submit();
    await screen.findByRole('alert');

    expect(refreshes).not.toHaveBeenCalled();
  });

  describe('Google', () => {
    it('does not show the button, nor ask for a challenge, without a client id', async () => {
      const challenges = vi.fn();
      server.use(
        http.post(`${API}/auth/google/challenge`, () => {
          challenges();
          return HttpResponse.json({ nonce: 'n', expires_at: '2099-01-01T00:00:00Z' });
        })
      );
      openLogin();

      await screen.findByRole('button', { name: 'Entrar' });

      expect(screen.queryByTestId('google-button')).not.toBeInTheDocument();
      expect(challenges).not.toHaveBeenCalled();
    });

    it('explains there is no account for that Google identity and asks for a new challenge', async () => {
      vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
      const google = installGoogleFake();
      const bodies: unknown[] = [];
      let challenges = 0;
      server.use(
        http.post(`${API}/auth/google/challenge`, () => {
          challenges += 1;
          return HttpResponse.json({
            nonce: `n-${challenges}`,
            expires_at: '2099-01-01T00:00:00Z',
          });
        }),
        http.post(`${API}/auth/google/sign-in`, async ({ request }) => {
          bodies.push(await request.json());
          return HttpResponse.json(problem('google_account_not_found'), { status: 401 });
        })
      );
      openLogin();

      await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));
      google.pick('id-token-1');

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Não há conta com este Google. Cria uma conta ou usa o link do convite.'
      );
      expect(bodies).toEqual([
        { id_token: 'id-token-1', invitation_token: null, create_account: false },
      ]);
      await waitFor(() => expect(challenges).toBe(2));
      expect(google.initialize).toHaveBeenLastCalledWith(
        expect.objectContaining({ nonce: 'n-2', client_id: 'client-1' })
      );
    });
  });
});
