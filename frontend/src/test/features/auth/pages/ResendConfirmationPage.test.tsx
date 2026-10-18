import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('ResendConfirmationPage', () => {
  it('prefills the email received in state and answers 204 with a neutral message', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post(`${API}/auth/email-confirmation/request`, async ({ request }) => {
        bodies.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({
      initialEntries: [
        { pathname: '/reenviar-confirmacao', state: { email: 'ana@ptmanager.test' } },
      ],
    });

    expect(await screen.findByLabelText('Email')).toHaveValue('ana@ptmanager.test');
    await user.click(screen.getByRole('button', { name: 'Reenviar email' }));

    expect(await screen.findByRole('heading', { name: 'Verifica o teu email' })).toHaveFocus();
    expect(screen.getByText(/Se existir uma conta por confirmar/)).toBeInTheDocument();
    expect(bodies).toEqual([{ email: 'ana@ptmanager.test' }]);
  });

  it('starts empty when nothing was passed in state', async () => {
    renderApp({ initialEntries: ['/reenviar-confirmacao'] });

    expect(await screen.findByLabelText('Email')).toHaveValue('');
  });

  it('asks to wait on 429 and lets the user try again', async () => {
    server.use(
      http.post(`${API}/auth/email-confirmation/request`, () =>
        HttpResponse.json({ title: 'Too many requests', status: 429 }, { status: 429 })
      )
    );
    const user = userEvent.setup();
    renderApp({
      initialEntries: [
        { pathname: '/reenviar-confirmacao', state: { email: 'ana@ptmanager.test' } },
      ],
    });

    await user.click(await screen.findByRole('button', { name: 'Reenviar email' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiadas tentativas.');
    expect(screen.getByRole('button', { name: 'Reenviar email' })).toBeEnabled();
  });
});
