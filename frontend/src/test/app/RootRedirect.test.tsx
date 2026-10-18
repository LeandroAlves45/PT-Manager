import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('RootRedirect', () => {
  it('sends an anonymous visitor from the root to login', async () => {
    const { router } = renderApp({ initialEntries: ['/'] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
  });

  it.each([
    ['superuser', '/admin'],
    ['trainer', '/trainer'],
    ['client', '/portal'],
  ] as const)('sends a %s from the root to %s', async (role, home) => {
    server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));

    const { router } = renderApp({ initialEntries: ['/'] });

    await waitFor(() => expect(router.state.location.pathname).toBe(home));
  });

  it('keeps an unknown path on the 404 page instead of redirecting through the root', async () => {
    server.use(...restorableSession({ role: 'trainer' }));

    const { router } = renderApp({ initialEntries: ['/does-not-exist'] });

    expect(
      await screen.findByRole('heading', { name: 'Página não encontrada' })
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/does-not-exist');
  });
});
