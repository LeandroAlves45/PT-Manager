import { waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('RootRedirect', () => {
  it('sends an anonymous visitor from the root to login', async () => {
    const { router } = renderApp({ initialEntries: ['/'] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/auth/login'));
  });

  it.each([
    ['superuser', '/admin'],
    ['trainer', '/trainer'],
    ['client', '/portal/today'],
  ] as const)('sends a %s from the root to %s', async (role, home) => {
    server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));

    const { router } = renderApp({ initialEntries: ['/'] });

    await waitFor(() => expect(router.state.location.pathname).toBe(home));
  });

  it('routes an unknown path through the root decision', async () => {
    server.use(...restorableSession({ role: 'trainer' }));

    const { router } = renderApp({ initialEntries: ['/does-not-exist'] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer'));
  });
});
