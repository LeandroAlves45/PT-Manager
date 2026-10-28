import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { peekPendingAuthNotice, resetPendingAuthNotice } from '@/features/auth';
import { getSession } from '@/shared/api/session';
import { installGoogleFake } from '@/test/google-fake';
import { accountResponse } from '@/test/msw/auth-fixtures';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const CURRENT = 'Atual!Password1';
const NEXT = 'N0va!Password';

function openAccount(role: 'superuser' | 'trainer' | 'client' = 'trainer') {
  const base = role === 'superuser' ? '/admin' : role === 'trainer' ? '/trainer' : '/portal';
  server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));
  return renderApp({ initialEntries: [`${base}/account`] });
}

function answerAccount(overrides: Parameters<typeof accountResponse>[0]) {
  server.use(http.get(`${API}/auth/account`, () => HttpResponse.json(accountResponse(overrides))));
}

async function changePassword(current = CURRENT, next = NEXT, confirmation = next) {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Palavra-passe atual'), current);
  await user.type(screen.getByLabelText('Nova palavra-passe'), next);
  await user.type(screen.getByLabelText('Confirmar nova palavra-passe'), confirmation);
  await user.click(screen.getByRole('button', { name: 'Mudar palavra-passe' }));
}

afterEach(() => resetPendingAuthNotice());

describe('AccountSecurityPage', () => {
  it.each(['superuser', 'trainer', 'client'] as const)(
    'is reachable by the %s inside their own area',
    async (role) => {
      openAccount(role);

      expect(
        await screen.findByRole('heading', { name: 'Conta e segurança', level: 1 })
      ).toBeInTheDocument();
      expect(screen.getByText('ana@ptmanager.test')).toBeInTheDocument();
    }
  );

  it('offers the password form to an account with a password, and no Google card without a client id', async () => {
    openAccount();

    expect(
      await screen.findByRole('heading', { name: 'Mudar palavra-passe', level: 2 })
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Ligar conta Google' })).not.toBeInTheDocument();
  });

  it('offers the Google card when Google is configured and not yet linked', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    installGoogleFake();
    openAccount();

    expect(await screen.findByRole('heading', { name: 'Ligar conta Google' })).toBeInTheDocument();
  });

  it('says the Google account is linked and hides the card', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    installGoogleFake();
    answerAccount({ google_linked: true });
    openAccount();

    expect(await screen.findByText('Conta Google ligada.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Ligar conta Google' })).not.toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Mudar palavra-passe', level: 2 })
    ).toBeInTheDocument();
  });

  it('explains a Google-only account has no password to change', async () => {
    answerAccount({ has_password: false, google_linked: true });
    openAccount();

    expect(
      await screen.findByText(/Entras com a conta Google \(ana@ptmanager\.test\)/)
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Mudar palavra-passe' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Nova palavra-passe')).not.toBeInTheDocument();
  });

  it('shows an error state with retry when the account cannot be read', async () => {
    server.use(
      http.get(`${API}/auth/account`, () =>
        HttpResponse.json(problem('internal_error'), { status: 500 })
      )
    );
    openAccount();

    expect(await screen.findByRole('button', { name: 'Tentar novamente' })).toBeInTheDocument();
  });

  describe('change password', () => {
    it('sends the three fields, forgets the local session and goes to login with a notice', async () => {
      const bodies: unknown[] = [];
      server.use(
        http.post(`${API}/auth/change-password`, async ({ request }) => {
          bodies.push(await request.json());
          return new HttpResponse(null, { status: 204 });
        })
      );
      const { router } = openAccount();
      const visited: string[] = [];
      router.subscribe((state) => visited.push(state.location.pathname));

      await changePassword();

      await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
      expect(bodies).toEqual([
        { current_password: CURRENT, new_password: NEXT, confirm_new_password: NEXT },
      ]);
      expect(getSession()).toBeNull();
      expect(await screen.findByRole('status')).toHaveTextContent(
        'Palavra-passe alterada. Entra com a nova.'
      );
      // Quem navega é só o guard (com state.from); o aviso vai pelo flash, sem navegação dupla.
      expect(router.state.location.state).toEqual({ from: '/trainer/account' });
      expect(visited.filter((path) => path === '/entrar')).toHaveLength(1);
      expect(peekPendingAuthNotice()).toBeNull();
    });

    it('does not show the notice again on a later visit to login', async () => {
      const { router } = openAccount();
      await changePassword();
      expect(await screen.findByRole('status')).toBeInTheDocument();

      await router.navigate('/criar-conta');
      expect(await screen.findByRole('heading', { name: 'Criar conta' })).toBeInTheDocument();
      await router.navigate('/entrar');
      expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();

      expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument();
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });

    it('keeps a wrong current password on its field, keeps the session and does not refresh', async () => {
      const refreshes = vi.fn();
      server.use(
        http.post(`${API}/auth/change-password`, () =>
          HttpResponse.json(problem('authentication_current_password_invalid'), { status: 401 })
        )
      );
      const { router } = openAccount();
      await screen.findByRole('heading', { name: 'Mudar palavra-passe' });
      server.events.on('request:start', ({ request }) => {
        if (request.url.endsWith('/auth/refresh') || request.url.endsWith('/auth/csrf'))
          refreshes();
      });

      await changePassword();

      expect(
        await screen.findByText('A palavra-passe atual não está correta.')
      ).toBeInTheDocument();
      expect(screen.getByLabelText('Palavra-passe atual')).toHaveAttribute('aria-invalid', 'true');
      expect(router.state.location.pathname).toBe('/trainer/account');
      expect(getSession()).not.toBeNull();
      expect(refreshes).not.toHaveBeenCalled();
      server.events.removeAllListeners();
    });

    it('keeps a password refused by the policy on the new password field', async () => {
      server.use(
        http.post(`${API}/auth/change-password`, () =>
          HttpResponse.json(problem('authentication_new_password_rejected'), { status: 400 })
        )
      );
      openAccount();

      await changePassword();

      expect(
        await screen.findByText('A nova palavra-passe não é aceite. Escolhe outra.')
      ).toBeInTheDocument();
    });

    it('does not call the server when the confirmation differs', async () => {
      const calls = vi.fn();
      server.use(
        http.post(`${API}/auth/change-password`, () => {
          calls();
          return new HttpResponse(null, { status: 204 });
        })
      );
      openAccount();

      await changePassword(CURRENT, NEXT, 'Outra!Password1');

      expect(await screen.findByText('As palavras-passe não coincidem.')).toBeInTheDocument();
      expect(calls).not.toHaveBeenCalled();
    });
  });

  describe('Google link card', () => {
    async function openWithGoogle() {
      vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
      const google = installGoogleFake();
      const view = openAccount();
      await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));
      const card = within(
        screen.getByRole('heading', { name: 'Ligar conta Google' }).closest('section')!
      );
      return { google, card, ...view };
    }

    it('links with the id token and the current password, then shows the account as linked', async () => {
      const bodies: unknown[] = [];
      let linked = false;
      server.use(
        http.post(`${API}/auth/google/link`, async ({ request }) => {
          bodies.push(await request.json());
          linked = true;
          return new HttpResponse(null, { status: 204 });
        }),
        http.get(`${API}/auth/account`, () =>
          HttpResponse.json(accountResponse({ google_linked: linked }))
        )
      );
      const user = userEvent.setup();
      const { google, card } = await openWithGoogle();

      await user.type(card.getByLabelText('Palavra-passe para confirmar'), CURRENT);
      google.pick('id-token-1');

      expect(await screen.findByText('Conta Google ligada.')).toBeInTheDocument();
      expect(bodies).toEqual([{ id_token: 'id-token-1', current_password: CURRENT }]);
    });

    it('asks for the password first and sends nothing when it is empty', async () => {
      const link = vi.fn();
      server.use(
        http.post(`${API}/auth/google/link`, () => {
          link();
          return new HttpResponse(null, { status: 204 });
        })
      );
      const { google, card } = await openWithGoogle();

      google.pick('id-token-1');

      expect(await card.findByRole('alert')).toHaveTextContent(
        'Introduz a palavra-passe atual antes de ligar o Google.'
      );
      expect(link).not.toHaveBeenCalled();
    });

    it('explains an email mismatch and asks for a new challenge', async () => {
      let challenges = 0;
      server.use(
        http.post(`${API}/auth/google/link/challenge`, () => {
          challenges += 1;
          return HttpResponse.json({
            nonce: `n-${challenges}`,
            expires_at: '2099-01-01T00:00:00Z',
          });
        }),
        http.post(`${API}/auth/google/link`, () =>
          HttpResponse.json(problem('google_link_email_mismatch'), { status: 409 })
        )
      );
      const user = userEvent.setup();
      const { google, card } = await openWithGoogle();

      await user.type(card.getByLabelText('Palavra-passe para confirmar'), CURRENT);
      google.pick('id-token-1');

      expect(await card.findByRole('alert')).toHaveTextContent(
        'A conta Google tem de ter o mesmo email que esta conta.'
      );
      await waitFor(() => expect(challenges).toBe(2));
    });

    it('shows the wrong password message without leaving the page or refreshing the session', async () => {
      server.use(
        http.post(`${API}/auth/google/link`, () =>
          HttpResponse.json(problem('authentication_current_password_invalid'), { status: 401 })
        )
      );
      const user = userEvent.setup();
      const { google, card, router } = await openWithGoogle();

      await user.type(card.getByLabelText('Palavra-passe para confirmar'), 'errada');
      google.pick('id-token-1');

      expect(await card.findByRole('alert')).toHaveTextContent(
        'A palavra-passe atual não está correta.'
      );
      expect(router.state.location.pathname).toBe('/trainer/account');
      expect(getSession()).not.toBeNull();
    });
  });

  describe('delete account', () => {
    const DELETION = `${API}/portal/my-account/deletion`;

    function openClientAccount(overrides: Parameters<typeof accountResponse>[0] = {}) {
      answerAccount({ role: 'client', ...overrides });
      return openAccount('client');
    }

    async function confirmDeletion(password: string | null = CURRENT, word = 'ELIMINAR') {
      const user = userEvent.setup();
      const card = within(
        (await screen.findByRole('heading', { name: 'Eliminar conta', level: 2 })).closest(
          'section'
        )!
      );
      if (password !== null) await user.type(card.getByLabelText('Palavra-passe atual'), password);
      await user.type(card.getByLabelText('Escreve ELIMINAR para confirmar'), word);
      return { user, card };
    }

    it('is offered only to clients', async () => {
      openAccount('trainer');
      await screen.findByRole('heading', { name: 'Mudar palavra-passe' });
      expect(screen.queryByRole('heading', { name: 'Eliminar conta' })).not.toBeInTheDocument();
    });

    it('keeps the button disabled until ELIMINAR is typed', async () => {
      openClientAccount();
      const { card } = await confirmDeletion(CURRENT, 'ELIMINA');

      expect(card.getByRole('button', { name: 'Eliminar conta' })).toBeDisabled();
    });

    it('sends the password and the word, ends the local session and goes to login with a notice', async () => {
      const bodies: unknown[] = [];
      server.use(
        http.post(DELETION, async ({ request }) => {
          bodies.push(await request.json());
          return new HttpResponse(null, { status: 204 });
        })
      );
      const { router } = openClientAccount();
      const { user, card } = await confirmDeletion();

      await user.click(card.getByRole('button', { name: 'Eliminar conta' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
      expect(bodies).toEqual([{ password: CURRENT, confirmation: 'ELIMINAR' }]);
      expect(getSession()).toBeNull();
      expect(await screen.findByRole('status')).toHaveTextContent('A tua conta foi eliminada.');
    });

    it('a Google-only account confirms with the word alone', async () => {
      const bodies: unknown[] = [];
      server.use(
        http.post(DELETION, async ({ request }) => {
          bodies.push(await request.json());
          return new HttpResponse(null, { status: 204 });
        })
      );
      const { router } = openClientAccount({ has_password: false, google_linked: true });
      const { user, card } = await confirmDeletion(null);

      expect(card.queryByLabelText('Palavra-passe atual')).not.toBeInTheDocument();
      await user.click(card.getByRole('button', { name: 'Eliminar conta' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
      expect(bodies).toEqual([{ password: null, confirmation: 'ELIMINAR' }]);
    });

    it('keeps a wrong password on its field and keeps the session', async () => {
      server.use(
        http.post(DELETION, () =>
          HttpResponse.json(
            problem('validation_failed', {
              errors: [
                {
                  field: 'password',
                  code: 'portal_account_deletion_password_invalid',
                  message: 'The password is incorrect.',
                },
              ],
            }),
            { status: 400 }
          )
        )
      );
      const { router } = openClientAccount();
      const { user, card } = await confirmDeletion('Errada!Password1');

      await user.click(card.getByRole('button', { name: 'Eliminar conta' }));

      expect(await card.findByText('A palavra-passe não está correta.')).toBeInTheDocument();
      expect(card.getByLabelText('Palavra-passe atual')).toHaveAttribute('aria-invalid', 'true');
      expect(router.state.location.pathname).toBe('/portal/account');
      expect(getSession()).not.toBeNull();
    });
  });
});
