import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { RequireRole } from '@/app/guards/RequireRole';
import { pendingRestore, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('RequireRole', () => {
  it('lets the authorised role in', async () => {
    server.use(...restorableSession({ role: 'trainer' }));

    renderApp({ initialEntries: ['/trainer'] });

    // [6E] ALTERADO: '/trainer' é agora o painel real (título "Bom treino"), não o placeholder.
    expect(await screen.findByRole('heading', { name: 'Bom treino' })).toBeInTheDocument();
  });

  it.each([
    ['client', '/trainer/clients', '/portal/today'],
    ['superuser', '/portal/today', '/admin'],
    ['trainer', '/admin/moderation', '/trainer'],
  ] as const)('sends a %s outside its area back to its own home', async (role, entry, home) => {
    server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));

    const { router } = renderApp({ initialEntries: [entry] });

    await waitFor(() => expect(router.state.location.pathname).toBe(home));
  });

  it('leaves anonymous visitors to the RequireAuth flow', async () => {
    const { router } = renderApp({ initialEntries: ['/admin'] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/auth/login'));
    expect(router.state.location.state).toEqual({ from: '/admin' });
  });

  it('waits for the session to be restored before deciding', async () => {
    const restore = pendingRestore();
    server.use(...restore.handlers);

    const { router } = renderApp({
      initialEntries: ['/secret'],
      routes: [
        {
          element: <RequireRole role="trainer" />,
          children: [{ path: '/secret', element: <p>secret</p> }],
        },
        { path: '/auth/login', element: <p>login</p> },
      ],
    });

    expect(await screen.findByText('A restaurar sessão...')).toBeInTheDocument();
    expect(screen.queryByText('secret')).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/secret');

    restore.release();
    expect(await screen.findByText('login')).toBeInTheDocument();
  });
});
