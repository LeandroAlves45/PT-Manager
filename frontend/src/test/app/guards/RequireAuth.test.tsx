import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { pendingRestore, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('RequireAuth', () => {
  it('neither redirects nor shows protected content while restoring', async () => {
    const restore = pendingRestore();
    server.use(...restore.handlers);

    const { router } = renderApp({ initialEntries: ['/trainer/clients'] });

    expect(await screen.findByText('A restaurar sessão...')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Clientes' })).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/trainer/clients');

    restore.release();
    await waitFor(() => expect(router.state.location.pathname).toBe('/auth/login'));
  });

  it('sends an anonymous visitor to login keeping pathname and search in state.from', async () => {
    const { router } = renderApp({ initialEntries: ['/trainer/clients?search=ana&page=2'] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/auth/login'));
    expect(router.state.location.state).toEqual({ from: '/trainer/clients?search=ana&page=2' });
  });

  it('lets an authenticated user reach the full internal destination', async () => {
    server.use(...restorableSession({ role: 'trainer' }));

    const { router } = renderApp({ initialEntries: ['/trainer/clients?search=ana'] });

    expect(await screen.findByRole('heading', { name: 'Clientes' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/trainer/clients');
    expect(router.state.location.search).toBe('?search=ana');
  });
});
