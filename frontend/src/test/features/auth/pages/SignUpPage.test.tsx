import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { installGoogleFake } from '@/test/google-fake';
import { GOOGLE_PENDING } from '@/test/msw/auth-fixtures';
import { API, problem, sessionResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const VALID_PASSWORD = 'Str0ng!Password';

function openSignUp() {
  return renderApp({ initialEntries: ['/criar-conta'] });
}

async function fill(
  { name = 'Ana Sousa', email = 'ana@ptmanager.test', password = VALID_PASSWORD } = {},
  user = userEvent.setup()
) {
  await user.type(await screen.findByLabelText('Nome completo'), name);
  await user.type(screen.getByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Palavra-passe'), password);
  await user.click(screen.getByRole('button', { name: 'Criar conta' }));
}

describe('SignUpPage', () => {
  it('shows the password rule and keeps the password field new-password', async () => {
    openSignUp();

    const password = await screen.findByLabelText('Palavra-passe');
    expect(password).toHaveAttribute('autocomplete', 'new-password');
    expect(password).toHaveAccessibleDescription(
      'Entre 8 e 128 caracteres, com maiúscula, minúscula, número e símbolo.'
    );
  });

  it('answers 202 with the neutral "check your email" screen and a resend link with state', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post(`${API}/auth/signup`, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json({ status: 'confirmation_email_sent' }, { status: 202 });
      })
    );
    const user = userEvent.setup();
    const { router } = openSignUp();

    await fill({}, user);

    expect(await screen.findByRole('heading', { name: 'Verifica o teu email' })).toHaveFocus();
    expect(bodies).toEqual([
      { full_name: 'Ana Sousa', email: 'ana@ptmanager.test', password: VALID_PASSWORD },
    ]);
    // A mensagem não diz se o email é novo ou já existia.
    expect(screen.queryByText(/ana@ptmanager\.test/)).not.toBeInTheDocument();
    // Nem o URL: o email não pode ir para a barra de endereço, histórico ou logs.
    expect(router.state.location.pathname).toBe('/criar-conta');
    expect(router.state.location.search).toBe('');

    await user.click(screen.getByRole('link', { name: 'pede um novo email' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/reenviar-confirmacao'));
    expect(router.state.location.search).toBe('');
    expect(router.state.location.state).toEqual({ email: 'ana@ptmanager.test' });
  });

  it('validates the password composition before calling the server', async () => {
    const calls = vi.fn();
    server.use(
      http.post(`${API}/auth/signup`, () => {
        calls();
        return HttpResponse.json({ status: 'confirmation_email_sent' }, { status: 202 });
      })
    );
    openSignUp();

    await fill({ password: 'semsimbolo1A' });

    expect(await screen.findByText('Inclui pelo menos um símbolo.')).toBeInTheDocument();
    expect(calls).not.toHaveBeenCalled();
  });

  it('refuses control characters in the name before calling the server (U+0000, D1)', async () => {
    const calls = vi.fn();
    server.use(
      http.post(`${API}/auth/signup`, () => {
        calls();
        return HttpResponse.json({ status: 'confirmation_email_sent' }, { status: 202 });
      })
    );
    const user = userEvent.setup();
    openSignUp();

    fireEvent.change(await screen.findByLabelText('Nome completo'), {
      target: { value: 'Ana\u0000Sousa' },
    });
    await user.type(screen.getByLabelText('Email'), 'ana@ptmanager.test');
    await user.type(screen.getByLabelText('Palavra-passe'), VALID_PASSWORD);
    await user.click(screen.getByRole('button', { name: 'Criar conta' }));

    expect(await screen.findByText('O nome tem caracteres inválidos.')).toBeInTheDocument();
    expect(calls).not.toHaveBeenCalled();
  });

  it('puts server validation errors on the matching fields', async () => {
    server.use(
      http.post(`${API}/auth/signup`, () =>
        HttpResponse.json(
          problem('validation_failed', {
            errors: [
              {
                field: 'FullName',
                code: 'authentication_full_name_required',
                message: 'Full name is required.',
              },
              {
                field: 'Password',
                code: 'authentication_registration_rejected',
                message: 'The account could not be created.',
              },
            ],
          }),
          { status: 400 }
        )
      )
    );
    openSignUp();

    await fill();

    expect(await screen.findByText('Indica o teu nome completo.')).toBeInTheDocument();
    expect(
      screen.getByText(
        'Não foi possível criar a conta com estes dados. Verifica-os e tenta novamente.'
      )
    ).toBeInTheDocument();
  });

  it('shows the rate limit message on 429', async () => {
    server.use(
      http.post(`${API}/auth/signup`, () =>
        HttpResponse.json({ title: 'Too many requests', status: 429 }, { status: 429 })
      )
    );
    openSignUp();

    await fill();

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiadas tentativas.');
  });

  describe('Google', () => {
    async function openWithGoogle() {
      vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
      const google = installGoogleFake();
      const view = openSignUp();
      await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));
      return { google, ...view };
    }

    it('sends create_account true and enters on 200', async () => {
      const bodies: unknown[] = [];
      server.use(
        http.post(`${API}/auth/google/sign-in`, async ({ request }) => {
          bodies.push(await request.json());
          return HttpResponse.json(sessionResponse());
        })
      );
      const { google, router } = await openWithGoogle();

      google.pick('id-token-1');

      await waitFor(() => expect(router.state.location.pathname).toBe('/trainer'));
      expect(bodies).toEqual([
        { id_token: 'id-token-1', invitation_token: null, create_account: true },
      ]);
    });

    it('asks to confirm the email on 202 instead of entering', async () => {
      server.use(
        http.post(`${API}/auth/google/sign-in`, () =>
          HttpResponse.json(GOOGLE_PENDING, { status: 202 })
        )
      );
      const { google, router } = await openWithGoogle();

      google.pick('id-token-1');

      expect(
        await screen.findByRole('heading', { name: 'Verifica o teu email' })
      ).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/criar-conta');
    });
  });
});
