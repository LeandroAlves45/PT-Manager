import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const ROUTE = '/admin/trainers/invite';

const INVITATION = {
  invitation_id: '55555555-5555-5555-5555-555555555555',
  email: 'novo@ptmanager.test',
  sent_at: '2099-01-03T09:00:00Z',
  expires_at: '2099-01-10T09:00:00Z',
};

function asSuperuser() {
  server.use(
    ...restorableSession({ role: 'superuser', trainer_id: null }),
    http.get(`${API}/admin/trainers/exempt`, () =>
      HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
    ),
    http.get(`${API}/admin/trainer-invitations`, () => HttpResponse.json([]))
  );
}

async function submit(user: ReturnType<typeof userEvent.setup>, email: string) {
  const input = await screen.findByLabelText('Email do treinador');
  await user.clear(input);
  await user.type(input, email);
  await user.click(screen.getByRole('button', { name: 'Enviar convite' }));
}

describe('InviteTrainerPage', () => {
  it('explains the invitation and links back to Treinadores', async () => {
    asSuperuser();
    renderApp({ initialEntries: [ROUTE] });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Convidar treinador' })
    ).toBeVisible();
    expect(
      screen.getByText(
        'O convite é de uso único, vale 7 dias e cria a conta já no plano Pro, sem pagamento. A conta cria-se com email e palavra-passe.'
      )
    ).toBeVisible();
    expect(screen.getByRole('link', { name: 'Voltar a Treinadores' })).toHaveAttribute(
      'href',
      '/admin/trainers'
    );
    expect(screen.getByLabelText('Email do treinador')).toHaveValue('');
  });

  it('prefills the email from the navigation state', async () => {
    asSuperuser();
    renderApp({
      initialEntries: [{ pathname: ROUTE, state: { email: 'novo@ptmanager.test' } }],
    });

    expect(await screen.findByLabelText('Email do treinador')).toHaveValue('novo@ptmanager.test');
  });

  it('sends the invitation, shows the validity in Lisbon time and returns to Treinadores', async () => {
    asSuperuser();
    const bodies: unknown[] = [];
    server.use(
      http.post(`${API}/admin/trainer-invitations`, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(INVITATION, { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [ROUTE] });

    await submit(user, 'novo@ptmanager.test');

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/trainers'));
    expect(bodies).toEqual([{ email: 'novo@ptmanager.test' }]);
    expect(toastMock.success).toHaveBeenCalledTimes(1);
    expect(String(toastMock.success.mock.calls[0]?.[0])).toMatch(
      /^Convite enviado\. Válido até 10\/01\/2099.*09:00\.$/
    );
  });

  it('tells the superuser that the email already has an account', async () => {
    asSuperuser();
    server.use(
      http.post(`${API}/admin/trainer-invitations`, () =>
        HttpResponse.json(problem('trainer_invitation_email_registered'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [ROUTE] });

    await submit(user, 'ana@ptmanager.test');

    expect(
      await screen.findByText('Este email já tem conta. Procura-o em Treinadores e isenta a conta.')
    ).toBeVisible();
    expect(router.state.location.pathname).toBe(ROUTE);
  });

  it('shows the server validation failure under the field', async () => {
    asSuperuser();
    server.use(
      http.post(`${API}/admin/trainer-invitations`, () =>
        HttpResponse.json(
          problem('validation_failed', {
            errors: [{ field: 'email', code: 'admin_trainer_email_invalid', message: 'x' }],
          }),
          { status: 400 }
        )
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await submit(user, 'a@b.c');

    expect(await screen.findByText('Escreve um email completo e válido.')).toBeVisible();
    expect(screen.getByLabelText('Email do treinador')).toBeInvalid();
  });

  it('does not call the server for text that is not an email', async () => {
    asSuperuser();
    let calls = 0;
    server.use(
      http.post(`${API}/admin/trainer-invitations`, () => {
        calls += 1;
        return HttpResponse.json(INVITATION, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await submit(user, 'ana');

    expect(await screen.findByText('Escreve um email completo e válido.')).toBeVisible();
    expect(calls).toBe(0);
  });

  it('reports an email delivery failure and stays on the form', async () => {
    asSuperuser();
    server.use(
      http.post(`${API}/admin/trainer-invitations`, () =>
        HttpResponse.json(problem('authentication_email_delivery_unavailable'), { status: 503 })
      )
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [ROUTE] });

    await submit(user, 'novo@ptmanager.test');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Não foi possível enviar o email. Tenta novamente.'
    );
    expect(router.state.location.pathname).toBe(ROUTE);
    expect(toastMock.success).not.toHaveBeenCalled();
  });
});
