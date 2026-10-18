import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { GoogleSignInSection } from '@/features/auth/components/GoogleSignInSection';
import { getSession } from '@/shared/api/session';
import { installGoogleFake } from '@/test/google-fake';
import { GOOGLE_PENDING } from '@/test/msw/auth-fixtures';
import { API, problem, sessionResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

/** Monta a secção dentro da app (providers reais) através de uma rota mínima. */
function mount(props: Partial<Parameters<typeof GoogleSignInSection>[0]> = {}) {
  const onSignedIn = vi.fn();
  const onPending = vi.fn();
  const view = renderApp({
    initialEntries: ['/secao'],
    routes: [
      {
        path: '/secao',
        element: <GoogleSignInSection onSignedIn={onSignedIn} onPending={onPending} {...props} />,
      },
    ],
  });
  return { onSignedIn, onPending, ...view };
}

function ready() {
  vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
  const google = installGoogleFake();
  return google;
}

describe('GoogleSignInSection', () => {
  it('stores the session and reports it on 200', async () => {
    const google = ready();
    const bodies: unknown[] = [];
    server.use(
      http.post(`${API}/auth/google/sign-in`, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(sessionResponse({ role: 'client', trainer_id: null }));
      })
    );
    const { onSignedIn, onPending } = mount({ invitationToken: 'inv-1' });
    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    google.pick('id-token-1');

    await waitFor(() => expect(onSignedIn).toHaveBeenCalledTimes(1));
    expect(onPending).not.toHaveBeenCalled();
    expect(getSession()?.role).toBe('client');
    expect(bodies).toEqual([
      { id_token: 'id-token-1', invitation_token: 'inv-1', create_account: false },
    ]);
  });

  it('reports a pending email confirmation on 202 and stores no session', async () => {
    const google = ready();
    server.use(
      http.post(`${API}/auth/google/sign-in`, () =>
        HttpResponse.json(GOOGLE_PENDING, { status: 202 })
      )
    );
    const { onSignedIn, onPending } = mount({ createAccount: true });
    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    google.pick('id-token-1');

    await waitFor(() => expect(onPending).toHaveBeenCalledTimes(1));
    expect(onSignedIn).not.toHaveBeenCalled();
    expect(getSession()).toBeNull();
  });

  it('tells the user to sign in with the password and link Google on 409 link_required', async () => {
    const google = ready();
    server.use(
      http.post(`${API}/auth/google/sign-in`, () =>
        HttpResponse.json(problem('google_account_link_required'), { status: 409 })
      )
    );
    const { onSignedIn } = mount();
    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    google.pick('id-token-1');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Já existe uma conta com este email. Entra com a palavra-passe e liga o Google em Conta e segurança.'
    );
    expect(onSignedIn).not.toHaveBeenCalled();
  });

  it('explains there is no account on 401 not_found', async () => {
    const google = ready();
    server.use(
      http.post(`${API}/auth/google/sign-in`, () =>
        HttpResponse.json(problem('google_account_not_found'), { status: 401 })
      )
    );
    mount();
    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    google.pick('id-token-1');

    expect(await screen.findByRole('alert')).toHaveTextContent('Não há conta com este Google.');
  });

  it('asks for a fresh challenge after every attempt, successful or not', async () => {
    const google = ready();
    let challenges = 0;
    server.use(
      http.post(`${API}/auth/google/challenge`, () => {
        challenges += 1;
        return HttpResponse.json({ nonce: `n-${challenges}`, expires_at: '2099-01-01T00:00:00Z' });
      })
    );
    mount();
    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    google.pick('id-token-1');

    await waitFor(() => expect(challenges).toBe(2));
    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(2));
  });

  it('renders nothing, separator included, when Google is not configured', async () => {
    const view = mount();

    await waitFor(() => expect(view.container).toBeEmptyDOMElement());
  });
});
