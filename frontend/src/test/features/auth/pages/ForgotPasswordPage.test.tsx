import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

async function request(email = 'ana@ptmanager.test') {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Email'), email);
  await user.click(screen.getByRole('button', { name: 'Enviar link' }));
}

describe('ForgotPasswordPage', () => {
  it('answers 204 with a conditional message that does not echo the email', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post(`${API}/auth/password-reset/request`, async ({ request: incoming }) => {
        bodies.push(await incoming.json());
        return new HttpResponse(null, { status: 204 });
      })
    );
    renderApp({ initialEntries: ['/recuperar-password'] });

    await request();

    expect(await screen.findByRole('heading', { name: 'Verifica o teu email' })).toHaveFocus();
    expect(screen.getByText(/Se existir uma conta com esse email/)).toBeInTheDocument();
    expect(screen.queryByText(/ana@ptmanager\.test/)).not.toBeInTheDocument();
    expect(bodies).toEqual([{ email: 'ana@ptmanager.test' }]);
  });

  it('rejects a malformed email without calling the server', async () => {
    renderApp({ initialEntries: ['/recuperar-password'] });

    await request('isto-nao-e-email');

    expect(await screen.findByText('Introduz um email válido.')).toBeInTheDocument();
  });

  it('asks to wait on 429 and keeps the form', async () => {
    server.use(
      http.post(`${API}/auth/password-reset/request`, () =>
        HttpResponse.json({ title: 'Too many requests', status: 429 }, { status: 429 })
      )
    );
    renderApp({ initialEntries: ['/recuperar-password'] });

    await request();

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiadas tentativas.');
    expect(screen.getByRole('button', { name: 'Enviar link' })).toBeEnabled();
  });
});
