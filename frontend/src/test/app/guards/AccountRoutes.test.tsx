import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('account routes', () => {
  it.each([
    ['/admin/account', 'trainer', '/trainer'],
    ['/admin/account', 'client', '/portal'],
    ['/trainer/account', 'superuser', '/admin'],
    ['/trainer/account', 'client', '/portal'],
    ['/portal/account', 'trainer', '/trainer'],
    ['/portal/account', 'superuser', '/admin'],
  ] as const)('sends %s to the own home of a %s', async (path, role, home) => {
    server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));

    const { router } = renderApp({ initialEntries: [path] });

    await waitFor(() => expect(router.state.location.pathname).toBe(home));
    expect(screen.queryByRole('heading', { name: 'Conta e segurança' })).not.toBeInTheDocument();
  });

  it.each([
    ['/admin/account', 'superuser'],
    ['/trainer/account', 'trainer'],
  ] as const)('names the account page in the breadcrumbs of %s', async (path, role) => {
    server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));

    renderApp({ initialEntries: [path] });

    const trail = await screen.findByRole('navigation', { name: 'Caminho' });
    expect(within(trail).getByText('Conta e segurança')).toBeInTheDocument();
    expect(within(trail).queryByText('account')).not.toBeInTheDocument();
  });

  it.each(['/admin/account', '/trainer/account', '/portal/account'])(
    'sends an anonymous visitor from %s to login, remembering where they were going',
    async (path) => {
      const { router } = renderApp({ initialEntries: [path] });

      await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
      expect(router.state.location.state).toEqual({ from: path });
    }
  );
});

describe('legacy login routes', () => {
  it.each(['/auth', '/auth/login'])('redirects %s to /entrar keeping the state', async (path) => {
    const { router } = renderApp({
      initialEntries: [{ pathname: path, state: { from: '/trainer/clients' } }],
    });

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
    expect(router.state.location.state).toEqual({ from: '/trainer/clients' });
    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
  });
});
