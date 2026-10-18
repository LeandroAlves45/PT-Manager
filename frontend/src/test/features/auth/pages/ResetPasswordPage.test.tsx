import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { getSession, setSession, toSession } from '@/shared/api/session';
import { API, problem, sessionResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const NEW_PASSWORD = 'N0va!Password';

async function submit(newPassword = NEW_PASSWORD, confirmation = newPassword) {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Nova palavra-passe'), newPassword);
  await user.type(screen.getByLabelText('Confirmar nova palavra-passe'), confirmation);
  await user.click(screen.getByRole('button', { name: 'Guardar palavra-passe' }));
}

describe('ResetPasswordPage', () => {
  it('shows the invalid link screen, with a way to ask for another, when there is no token', async () => {
    renderApp({ initialEntries: ['/repor-password'] });

    expect(await screen.findByRole('heading', { name: 'Link inválido' })).toHaveFocus();
    expect(screen.getByRole('link', { name: 'Pedir um novo link' })).toHaveAttribute(
      'href',
      '/recuperar-password'
    );
  });

  it('sends the token with both passwords, clears the local session and goes to login with a notice', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post(`${API}/auth/password-reset/complete`, async ({ request }) => {
        bodies.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      })
    );
    // Uma sessão antiga neste separador: o servidor revoga-as todas, a local tem de sair.
    setSession(toSession(sessionResponse()));
    const { router } = renderApp({ initialEntries: ['/repor-password?token=tok%2B1'] });

    await submit();

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
    expect(bodies).toEqual([
      { token: 'tok+1', new_password: NEW_PASSWORD, confirm_new_password: NEW_PASSWORD },
    ]);
    expect(getSession()).toBeNull();
    expect(await screen.findByRole('status')).toHaveTextContent(
      'Palavra-passe alterada. Entra com a nova.'
    );
  });

  it('shows the expired link screen on 401 and does not try to refresh the session', async () => {
    server.use(
      http.post(`${API}/auth/password-reset/complete`, () =>
        HttpResponse.json(problem('authentication_password_reset_invalid'), { status: 401 })
      )
    );
    renderApp({ initialEntries: ['/repor-password?token=tok'] });

    await submit();

    expect(await screen.findByRole('heading', { name: 'Link inválido' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('não é válido ou já expirou');
  });

  it('keeps a password refused by the server policy on the field', async () => {
    server.use(
      http.post(`${API}/auth/password-reset/complete`, () =>
        HttpResponse.json(problem('authentication_new_password_rejected'), { status: 400 })
      )
    );
    renderApp({ initialEntries: ['/repor-password?token=tok'] });

    await submit();

    expect(
      await screen.findByText('A nova palavra-passe não é aceite. Escolhe outra.')
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Nova palavra-passe')).toHaveAttribute('aria-invalid', 'true');
  });

  it('does not call the server when the confirmation differs', async () => {
    renderApp({ initialEntries: ['/repor-password?token=tok'] });

    await submit(NEW_PASSWORD, 'Outra!Password1');

    expect(await screen.findByText('As palavras-passe não coincidem.')).toBeInTheDocument();
  });
});
