import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, pendingRestore, problem, sessionResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

function openLogin(from?: string) {
  return renderApp({
    initialEntries: [{ pathname: '/auth/login', state: from === undefined ? null : { from } }],
  });
}

async function submit(email = 'ana@ptmanager.test', password = 'correct-horse-1') {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Password'), password);
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

  it('returns to the saved internal destination, query string included', async () => {
    const { router } = openLogin('/trainer/clients?search=ana&page=2');

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

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal/today'));
  });

  it.each(['//evil.example', 'https://evil.example/phish', '/\\evil.example', 'trainer'])(
    'ignores the external or malformed destination %s',
    async (from) => {
      const { router } = openLogin(from);

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

    expect(await screen.findByRole('alert')).toHaveTextContent('Email ou password inválidos.');
    expect(screen.getByRole('alert')).not.toHaveTextContent(/ana@ptmanager\.test|não existe/i);
  });

  it('shows the PT-PT rate limit message on 429 and does not retry', async () => {
    const calls = useLoginAnswer(429, problem('rate_limit_exceeded'));
    openLogin();

    await submit();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Demasiadas tentativas. Tenta dentro de momentos.'
    );
    expect(calls.count).toBe(1);
  });
});
