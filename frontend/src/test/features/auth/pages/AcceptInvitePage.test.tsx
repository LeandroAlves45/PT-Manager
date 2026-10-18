import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { getSession } from '@/shared/api/session';
import {
  API,
  pendingRestore,
  problem,
  restorableSession,
  sessionResponse,
} from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const INVITE = '/aceitar-convite?token=tok-1';
const PASSWORD = 'Str0ng!Password';

const CLIENT_SESSION = sessionResponse({
  role: 'client',
  trainer_id: null,
  user_id: '33333333-3333-3333-3333-333333333333',
});

function asClient() {
  server.use(...restorableSession({ role: 'client', trainer_id: null }));
}

async function register(user = userEvent.setup()) {
  await user.type(await screen.findByLabelText('Palavra-passe'), PASSWORD);
  await user.type(screen.getByLabelText('Confirmar palavra-passe'), PASSWORD);
  await user.click(screen.getByRole('button', { name: 'Criar conta e aceitar' }));
}

describe('AcceptInvitePage', () => {
  it('shows an error with a way out when the link has no token', async () => {
    renderApp({ initialEntries: ['/aceitar-convite'] });

    expect(await screen.findByRole('heading', { name: 'Convite inválido' })).toHaveFocus();
    expect(screen.getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', '/entrar');
  });

  it('shows a loading state, and neither form nor button, while the session restores', async () => {
    const restore = pendingRestore();
    server.use(...restore.handlers);
    renderApp({ initialEntries: [INVITE] });

    expect(await screen.findByRole('status', { name: 'A restaurar sessão…' })).toBeInTheDocument();
    expect(screen.queryByLabelText('Palavra-passe')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Aceitar convite' })).not.toBeInTheDocument();

    restore.release();
    expect(await screen.findByLabelText('Palavra-passe')).toBeInTheDocument();
  });

  describe('anonymous visitor', () => {
    it('registers with the token and the password, then enters the portal', async () => {
      const bodies: unknown[] = [];
      server.use(
        http.post(`${API}/auth/accept-invite/register`, async ({ request }) => {
          bodies.push(await request.json());
          return HttpResponse.json(CLIENT_SESSION);
        })
      );
      const { router } = renderApp({ initialEntries: [INVITE] });

      await register();

      await waitFor(() => expect(router.state.location.pathname).toBe('/portal'));
      expect(bodies).toEqual([{ token: 'tok-1', password: PASSWORD, confirm_password: PASSWORD }]);
      expect(getSession()?.role).toBe('client');
    });

    it('sends someone who already has an account to login and back to the same invitation', async () => {
      server.use(
        http.post(`${API}/auth/accept-invite/register`, () =>
          HttpResponse.json(problem('authentication_invitation_account_exists'), { status: 409 })
        ),
        http.post(`${API}/auth/login`, () => HttpResponse.json(CLIENT_SESSION))
      );
      const user = userEvent.setup();
      const { router } = renderApp({ initialEntries: [INVITE] });

      await register(user);

      expect(await screen.findByRole('heading', { name: 'Já tens conta' })).toHaveFocus();
      await user.click(screen.getByRole('link', { name: 'Entrar para aceitar' }));
      await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
      expect(router.state.location.state).toEqual({ from: '/aceitar-convite?token=tok-1' });

      await user.type(await screen.findByLabelText('Email'), 'ana@ptmanager.test');
      await user.type(screen.getByLabelText('Palavra-passe'), 'correct-horse-1');
      await user.click(screen.getByRole('button', { name: 'Entrar' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/aceitar-convite'));
      expect(router.state.location.search).toBe('?token=tok-1');
      expect(await screen.findByRole('button', { name: 'Aceitar convite' })).toBeInTheDocument();
    });

    it('offers the same way back for "Já tenho conta"', async () => {
      const user = userEvent.setup();
      const { router } = renderApp({ initialEntries: [INVITE] });

      await user.click(await screen.findByRole('link', { name: 'Entrar' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
      expect(router.state.location.state).toEqual({ from: '/aceitar-convite?token=tok-1' });
    });

    it('ends on an unavailable screen when the invitation was already used', async () => {
      server.use(
        http.post(`${API}/auth/accept-invite/register`, () =>
          HttpResponse.json(problem('authentication_invitation_consumed'), { status: 409 })
        )
      );
      renderApp({ initialEntries: [INVITE] });

      await register();

      expect(
        await screen.findByRole('heading', { name: 'Convite indisponível' })
      ).toBeInTheDocument();
      expect(screen.getByRole('alert')).toHaveTextContent('Este convite já foi usado.');
      expect(screen.queryByLabelText('Palavra-passe')).not.toBeInTheDocument();
    });
  });

  describe('authenticated client', () => {
    it('accepts with one click, replaces the session and clears the data cache', async () => {
      asClient();
      const bodies: unknown[] = [];
      server.use(
        http.post(`${API}/auth/accept-invite`, async ({ request }) => {
          bodies.push(await request.json());
          return HttpResponse.json(sessionResponse({ role: 'client', trainer_id: 'trainer-2' }));
        })
      );
      const user = userEvent.setup();
      const { router, queryClient } = renderApp({ initialEntries: [INVITE] });
      queryClient.setQueryData(['canary'], 'dados do treinador anterior');

      await user.click(await screen.findByRole('button', { name: 'Aceitar convite' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/portal'));
      expect(bodies).toEqual([{ token: 'tok-1', transfer_approved: false }]);
      expect(getSession()?.trainerId).toBe('trainer-2');
      expect(queryClient.getQueryData(['canary'])).toBeUndefined();
    });

    it('asks for confirmation before moving to another trainer, then repeats with approval', async () => {
      asClient();
      const bodies: Array<{ transfer_approved: boolean }> = [];
      server.use(
        http.post(`${API}/auth/accept-invite`, async ({ request }) => {
          const body = (await request.json()) as { transfer_approved: boolean };
          bodies.push(body);
          return body.transfer_approved
            ? HttpResponse.json(sessionResponse({ role: 'client', trainer_id: 'trainer-2' }))
            : HttpResponse.json(problem('authentication_transfer_approval_required'), {
                status: 409,
              });
        })
      );
      const user = userEvent.setup();
      const { router } = renderApp({ initialEntries: [INVITE] });

      await user.click(await screen.findByRole('button', { name: 'Aceitar convite' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Confirma a mudança');
      expect(router.state.location.pathname).toBe('/aceitar-convite');

      await user.click(screen.getByRole('button', { name: 'Confirmar mudança e aceitar' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/portal'));
      expect(bodies.map((body) => body.transfer_approved)).toEqual([false, true]);
    });

    it('ends on an unavailable screen when the invitation was meant for another email', async () => {
      asClient();
      server.use(
        http.post(`${API}/auth/accept-invite`, () =>
          HttpResponse.json(problem('authentication_invitation_email_mismatch'), { status: 409 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: [INVITE] });

      await user.click(await screen.findByRole('button', { name: 'Aceitar convite' }));

      expect(
        await screen.findByRole('heading', { name: 'Convite indisponível' })
      ).toBeInTheDocument();
      expect(screen.getByRole('alert')).toHaveTextContent('outro email');
    });
  });

  describe('trainer or superuser', () => {
    it('warns the account is not a client and lets the user sign out to continue', async () => {
      server.use(...restorableSession({ role: 'trainer', trainer_id: 't-1' }));
      const user = userEvent.setup();
      renderApp({ initialEntries: [INVITE] });

      expect(
        await screen.findByRole('heading', { name: 'Esta conta não é de cliente' })
      ).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Aceitar convite' })).not.toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: 'Terminar sessão' }));

      expect(await screen.findByLabelText('Palavra-passe')).toBeInTheDocument();
      expect(getSession()).toBeNull();
    });
  });
});
