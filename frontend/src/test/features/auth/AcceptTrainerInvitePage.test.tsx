import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, pendingRestore, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const INVITE = '/convite-treinador?token=tok-t1';
const PASSWORD = 'Str0ng!Password';
const EMAIL = 'novo.treinador@ptmanager.test';

type InviteStatus = 'valid' | 'expired' | 'used' | 'invalid';

/** Estado do convite; devolve os corpos recebidos para o teste ler o token enviado. */
function serveStatus(status: InviteStatus) {
  const bodies: unknown[] = [];
  server.use(
    http.post(`${API}/auth/trainer-invite/status`, async ({ request }) => {
      bodies.push(await request.json());
      return HttpResponse.json({ status, email: status === 'valid' ? EMAIL : null });
    })
  );
  return bodies;
}

async function fill(user: ReturnType<typeof userEvent.setup>, password = PASSWORD) {
  await user.type(await screen.findByLabelText('Nome'), 'Rui Costa');
  await user.type(screen.getByLabelText('Palavra-passe'), password);
  await user.type(screen.getByLabelText('Confirmar palavra-passe'), password);
}

async function submit(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Criar conta' }));
}

describe('AcceptTrainerInvitePage', () => {
  it('shows an invalid-invitation panel with a way out when the link has no token', async () => {
    renderApp({ initialEntries: ['/convite-treinador'] });

    expect(await screen.findByRole('heading', { name: 'Convite inválido' })).toHaveFocus();
    expect(screen.getByText('O convite não é válido.')).toBeVisible();
    expect(screen.getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', '/entrar');
  });

  it('shows a loading state while the invitation status is requested', async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.post(`${API}/auth/trainer-invite/status`, async () => {
        await gate;
        return HttpResponse.json({ status: 'valid', email: EMAIL });
      })
    );
    renderApp({ initialEntries: [INVITE] });

    expect(await screen.findByRole('status', { name: 'A verificar o convite…' })).toBeVisible();
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument();

    release();
    expect(await screen.findByLabelText('Nome')).toBeVisible();
  });

  it('sends the token in the body of the status request and shows the email read-only', async () => {
    const bodies = serveStatus('valid');
    renderApp({ initialEntries: [INVITE] });

    expect(await screen.findByRole('heading', { name: 'Criar conta de treinador' })).toBeVisible();
    expect(screen.getByText(EMAIL)).toBeVisible();
    expect(screen.queryByRole('textbox', { name: /email/i })).not.toBeInTheDocument();
    expect(bodies).toEqual([{ token: 'tok-t1' }]);
  });

  it('has no Google sign-in anywhere on the page', async () => {
    serveStatus('valid');
    renderApp({ initialEntries: [INVITE] });

    await screen.findByLabelText('Nome');
    expect(screen.queryByText(/google/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /google/i })).not.toBeInTheDocument();
  });

  it('registers with token, name and passwords, then offers the way to sign in', async () => {
    serveStatus('valid');
    const bodies: unknown[] = [];
    server.use(
      http.post(`${API}/auth/trainer-invite/register`, async ({ request }) => {
        bodies.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [INVITE] });

    await fill(user);
    await submit(user);

    expect(await screen.findByRole('heading', { name: 'Conta criada' })).toHaveFocus();
    expect(screen.getByText('Já podes entrar com o teu email e a palavra-passe.')).toBeVisible();
    expect(bodies).toEqual([
      {
        token: 'tok-t1',
        full_name: 'Rui Costa',
        password: PASSWORD,
        confirm_password: PASSWORD,
      },
    ]);

    await user.click(screen.getByRole('link', { name: 'Entrar' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeVisible();
  });

  it('accepts a Unicode symbol as the password symbol, like sign-up', async () => {
    serveStatus('valid');
    const bodies: Record<string, unknown>[] = [];
    server.use(
      http.post(`${API}/auth/trainer-invite/register`, async ({ request }) => {
        bodies.push((await request.json()) as Record<string, unknown>);
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [INVITE] });

    await fill(user, 'Segura1€abc');
    await submit(user);

    expect(await screen.findByRole('heading', { name: 'Conta criada' })).toBeVisible();
    expect(bodies[0]?.['password']).toBe('Segura1€abc');
  });

  it('validates the form before the request', async () => {
    serveStatus('valid');
    let calls = 0;
    server.use(
      http.post(`${API}/auth/trainer-invite/register`, () => {
        calls += 1;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [INVITE] });

    await user.type(await screen.findByLabelText('Palavra-passe'), 'curta');
    await user.type(screen.getByLabelText('Confirmar palavra-passe'), 'outra');
    await submit(user);

    expect(await screen.findByText('Indica o teu nome completo.')).toBeVisible();
    expect(screen.getByText('A palavra-passe deve conter pelo menos 8 caracteres.')).toBeVisible();
    expect(calls).toBe(0);

    await user.type(screen.getByLabelText('Nome'), 'Rui\u0007');
    await user.clear(screen.getByLabelText('Palavra-passe'));
    await user.type(screen.getByLabelText('Palavra-passe'), PASSWORD);
    await submit(user);

    expect(await screen.findByText('O nome tem caracteres inválidos.')).toBeVisible();
    expect(screen.getByText('As palavras-passe não coincidem.')).toBeVisible();
    expect(calls).toBe(0);
  });

  it('shows the password rejection from the server under the password field', async () => {
    serveStatus('valid');
    server.use(
      http.post(`${API}/auth/trainer-invite/register`, () =>
        HttpResponse.json(problem('authentication_registration_rejected'), { status: 400 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [INVITE] });

    await fill(user);
    await submit(user);

    expect(
      await screen.findByText(
        'Não foi possível criar a conta com estes dados. Verifica-os e tenta novamente.'
      )
    ).toBeVisible();
    expect(screen.getByLabelText('Palavra-passe')).toBeInvalid();
    expect(screen.queryByRole('heading', { name: 'Conta criada' })).not.toBeInTheDocument();
  });

  it.each([
    ['expired', 'Este convite expirou. Pede outro à equipa do PT Manager.'],
    ['invalid', 'O convite não é válido.'],
  ] as const)('shows the %s panel without a form', async (status, message) => {
    serveStatus(status);
    renderApp({ initialEntries: [INVITE] });

    expect(await screen.findByText(message)).toBeVisible();
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', '/entrar');
  });

  it('tells a used invitation to sign in with the account that was created', async () => {
    serveStatus('used');
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [INVITE] });

    expect(
      await screen.findByText('Este convite já foi usado. Entra com a conta que criaste.')
    ).toBeVisible();
    await user.click(screen.getByRole('link', { name: 'Entrar' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
  });

  it('maps a 409 «used» on registration to the used message, with no form left', async () => {
    serveStatus('valid');
    server.use(
      http.post(`${API}/auth/trainer-invite/register`, () =>
        HttpResponse.json(problem('trainer_invitation_used'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [INVITE] });

    await fill(user);
    await submit(user);

    expect(
      await screen.findByText('Este convite já foi usado. Entra com a conta que criaste.')
    ).toBeVisible();
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument();
  });

  it.each([
    ['trainer_invitation_invalid', 'O convite não é válido.'],
    ['trainer_invitation_expired', 'Este convite expirou. Pede outro à equipa do PT Manager.'],
  ] as const)('maps a 401 «%s» on registration to its message', async (code, message) => {
    serveStatus('valid');
    server.use(
      http.post(`${API}/auth/trainer-invite/register`, () =>
        HttpResponse.json(problem(code), { status: 401 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [INVITE] });

    await fill(user);
    await submit(user);

    expect(await screen.findByText(message)).toBeVisible();
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument();
  });

  it('shows a loading state, and not the form, while the session restores', async () => {
    serveStatus('valid');
    const restore = pendingRestore();
    server.use(...restore.handlers);
    renderApp({ initialEntries: [INVITE] });

    expect(await screen.findByRole('status', { name: 'A restaurar sessão…' })).toBeVisible();
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument();

    restore.release();
    expect(await screen.findByLabelText('Nome')).toBeVisible();
  });

  it('asks an authenticated user to sign out first, without asking for the invitation status', async () => {
    const bodies = serveStatus('valid');
    server.use(...restorableSession({ role: 'trainer' }));
    renderApp({ initialEntries: [INVITE] });

    // O foco chega num efeito depois do render: esperar por ele, não só pelo título.
    const heading = await screen.findByRole('heading', { name: 'Já tens sessão iniciada' });
    await waitFor(() => expect(heading).toHaveFocus());
    expect(screen.getByText('Termina a sessão para criar a conta de treinador.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Terminar sessão' })).toBeVisible();
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument();
    expect(bodies).toEqual([]);
  });
});
