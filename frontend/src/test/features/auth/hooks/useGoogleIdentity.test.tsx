import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { useGoogleIdentity, type GooglePurpose } from '@/features/auth/hooks/useGoogleIdentity';
import { resetGoogleIdentityLoader } from '@/features/auth/lib/google-identity';
import { installGoogleFake } from '@/test/google-fake';
import { API, problem } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

function Probe({
  purpose = 'sign-in',
  onCredential,
}: {
  purpose?: GooglePurpose;
  onCredential: Parameters<typeof useGoogleIdentity>[0]['onCredential'];
}) {
  const { available, attachContainer, error, reset } = useGoogleIdentity({
    purpose,
    onCredential,
  });

  return (
    <div>
      <p data-testid="available">{String(available)}</p>
      <p data-testid="error">{error ?? ''}</p>
      <div ref={attachContainer} data-testid="container" />
      <button onClick={reset}>reset</button>
    </div>
  );
}

function mount(props: Parameters<typeof Probe>[0]) {
  return renderApp({
    initialEntries: ['/probe'],
    routes: [{ path: '/probe', element: <Probe {...props} /> }],
  });
}

describe('useGoogleIdentity', () => {
  it('neither asks for a challenge nor loads the script without a client id', async () => {
    const challenges = vi.fn();
    server.use(
      http.post(`${API}/auth/google/challenge`, () => {
        challenges();
        return HttpResponse.json({ nonce: 'n', expires_at: '2099-01-01T00:00:00Z' });
      })
    );
    resetGoogleIdentityLoader();
    mount({ onCredential: vi.fn() });

    expect(await screen.findByTestId('available')).toHaveTextContent('false');
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(challenges).not.toHaveBeenCalled();
    expect(document.querySelector('script[src*="accounts.google.com"]')).toBeNull();
  });

  it('initialises the library with the server nonce and renders the button in pt-PT', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    const google = installGoogleFake();
    server.use(
      http.post(`${API}/auth/google/challenge`, () =>
        HttpResponse.json({ nonce: 'nonce-xyz', expires_at: '2099-01-01T00:00:00Z' })
      )
    );
    const onCredential = vi.fn();
    mount({ onCredential });

    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    expect(google.initialize).toHaveBeenCalledWith(
      expect.objectContaining({ client_id: 'client-1', nonce: 'nonce-xyz', auto_select: false })
    );
    expect(google.renderButton).toHaveBeenCalledWith(
      screen.getByTestId('container'),
      expect.objectContaining({ locale: 'pt-PT', theme: 'outline' })
    );

    google.pick('id-token-1');
    await waitFor(() => expect(onCredential).toHaveBeenCalledWith('id-token-1'));
  });

  it('uses the link challenge endpoint for the link purpose', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    const google = installGoogleFake();
    const sign = vi.fn();
    const link = vi.fn();
    server.use(
      http.post(`${API}/auth/google/challenge`, () => {
        sign();
        return HttpResponse.json({ nonce: 'a', expires_at: '2099-01-01T00:00:00Z' });
      }),
      http.post(`${API}/auth/google/link/challenge`, () => {
        link();
        return HttpResponse.json({ nonce: 'b', expires_at: '2099-01-01T00:00:00Z' });
      })
    );
    mount({ purpose: 'link', onCredential: vi.fn() });

    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    expect(link).toHaveBeenCalledTimes(1);
    expect(sign).not.toHaveBeenCalled();
    expect(google.initialize).toHaveBeenCalledWith(expect.objectContaining({ nonce: 'b' }));
  });

  it('requests a new challenge and redraws the button on reset', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    const google = installGoogleFake();
    let challenges = 0;
    server.use(
      http.post(`${API}/auth/google/challenge`, () => {
        challenges += 1;
        return HttpResponse.json({ nonce: `n-${challenges}`, expires_at: '2099-01-01T00:00:00Z' });
      })
    );
    const user = userEvent.setup();
    mount({ onCredential: vi.fn() });
    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    await user.click(screen.getByRole('button', { name: 'reset' }));

    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(2));
    expect(google.initialize).toHaveBeenLastCalledWith(expect.objectContaining({ nonce: 'n-2' }));
    expect(screen.getByTestId('container').children).toHaveLength(1);
  });

  it.each([
    ['succeeds', () => Promise.resolve()],
    ['fails', () => Promise.reject(new Error('o pedido falhou'))],
  ])('asks for a fresh challenge when the credential handler %s', async (_outcome, handler) => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    const google = installGoogleFake();
    let challenges = 0;
    server.use(
      http.post(`${API}/auth/google/challenge`, () => {
        challenges += 1;
        return HttpResponse.json({ nonce: `n-${challenges}`, expires_at: '2099-01-01T00:00:00Z' });
      })
    );
    mount({ onCredential: handler });
    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1));

    google.pick('id-token-1');

    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(2));
    expect(google.initialize).toHaveBeenLastCalledWith(expect.objectContaining({ nonce: 'n-2' }));
  });

  it('shows the rate limit message when the challenge answers 429', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    const google = installGoogleFake();
    server.use(
      http.post(`${API}/auth/google/challenge`, () =>
        HttpResponse.json({ title: 'Too many requests', status: 429 }, { status: 429 })
      )
    );
    mount({ onCredential: vi.fn() });

    await waitFor(() =>
      expect(screen.getByTestId('error')).toHaveTextContent('Demasiadas tentativas.')
    );
    expect(google.renderButton).not.toHaveBeenCalled();
  });

  it('maps a provider outage to its own message', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    installGoogleFake();
    server.use(
      http.post(`${API}/auth/google/challenge`, () =>
        HttpResponse.json(problem('google_provider_unavailable'), { status: 503 })
      )
    );
    mount({ onCredential: vi.fn() });

    await waitFor(() =>
      expect(screen.getByTestId('error')).toHaveTextContent('não está disponível agora')
    );
  });
});
