import { QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { StrictMode } from 'react';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthProvider } from '@/app/providers/AuthProvider';
import { useAuth } from '@/app/providers/useAuth';
import { getSession, setSession, toSession } from '@/shared/api/session';
import { FakeBroadcastChannel, installBroadcastChannel } from '@/test/browser-fakes';
import { API, problem, sessionResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { createTestQueryClient } from '@/test/render';

function AuthProbe() {
  const { status, session, signOut } = useAuth();
  return (
    <div>
      <p data-testid="status">{status}</p>
      <p data-testid="user">{session?.userId ?? 'none'}</p>
      <button onClick={() => void signOut()}>sign out</button>
    </div>
  );
}

function renderProvider({ strict = false } = {}) {
  const queryClient = createTestQueryClient();
  queryClient.setQueryData(['clients', 'list'], ['cached']);

  const tree = (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    </QueryClientProvider>
  );

  render(strict ? <StrictMode>{tree}</StrictMode> : tree);
  return { queryClient };
}

function useSuccessfulRefresh(userId = '11111111-1111-1111-1111-111111111111') {
  const calls = { csrf: 0, refresh: 0 };
  server.use(
    http.post(`${API}/auth/csrf`, () => {
      calls.csrf += 1;
      return HttpResponse.json({ csrf_token: 'csrf-bootstrap' });
    }),
    http.post(`${API}/auth/refresh`, () => {
      calls.refresh += 1;
      return HttpResponse.json(sessionResponse({ user_id: userId }));
    })
  );
  return calls;
}

beforeEach(() => {
  installBroadcastChannel();
});

describe('AuthProvider', () => {
  it('leaves restoring even when the bootstrap throws', async () => {
    server.use(
      http.post(`${API}/auth/csrf`, () =>
        HttpResponse.json(problem('service_unavailable'), { status: 503 })
      )
    );

    renderProvider();

    expect(screen.getByTestId('status')).toHaveTextContent('restoring');
    expect(await screen.findByText('anonymous')).toBeInTheDocument();
  });

  it('restores the session once under StrictMode', async () => {
    const calls = useSuccessfulRefresh();

    renderProvider({ strict: true });

    expect(await screen.findByText('authenticated')).toBeInTheDocument();
    expect(calls).toEqual({ csrf: 1, refresh: 1 });
  });

  it('clears the query cache and the session on local sign-out', async () => {
    useSuccessfulRefresh();
    const { queryClient } = renderProvider();
    await screen.findByText('authenticated');

    await userEvent.click(screen.getByRole('button', { name: 'sign out' }));

    expect(await screen.findByText('anonymous')).toBeInTheDocument();
    expect(queryClient.getQueryData(['clients', 'list'])).toBeUndefined();
    expect(FakeBroadcastChannel.published).toEqual([{ type: 'session-invalidated' }]);
  });

  it('still signs out locally when the logout request fails', async () => {
    useSuccessfulRefresh();
    server.use(http.post(`${API}/auth/logout`, () => HttpResponse.error()));
    const { queryClient } = renderProvider();
    await screen.findByText('authenticated');

    await userEvent.click(screen.getByRole('button', { name: 'sign out' }));

    expect(await screen.findByText('anonymous')).toBeInTheDocument();
    expect(queryClient.getQueryData(['clients', 'list'])).toBeUndefined();
  });

  it('clears the query cache and the session on a remote invalidation', async () => {
    useSuccessfulRefresh();
    const { queryClient } = renderProvider();
    await screen.findByText('authenticated');

    act(() => {
      new FakeBroadcastChannel('pt-manager.session.events').postMessage({
        type: 'session-invalidated',
      });
    });

    expect(await screen.findByText('anonymous')).toBeInTheDocument();
    expect(getSession()).toBeNull();
    expect(queryClient.getQueryData(['clients', 'list'])).toBeUndefined();
  });

  it('clears the query cache when the authenticated user changes', async () => {
    useSuccessfulRefresh();
    const { queryClient } = renderProvider();
    await screen.findByText('authenticated');
    queryClient.setQueryData(['clients', 'list'], ['from first user']);

    act(() => {
      setSession(toSession(sessionResponse({ user_id: '99999999-9999-9999-9999-999999999999' })));
    });

    expect(await screen.findByText('99999999-9999-9999-9999-999999999999')).toBeInTheDocument();
    expect(queryClient.getQueryData(['clients', 'list'])).toBeUndefined();
  });
});
