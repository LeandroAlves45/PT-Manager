import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { pendingRestore, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('NotFoundPage', () => {
  it('shows the 404 to an anonymous visitor, focuses the title and leads to login', async () => {
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/nao-existe'] });

    const title = await screen.findByRole('heading', { name: 'Página não encontrada' });
    expect(title).toHaveFocus();
    expect(router.state.location.pathname).toBe('/nao-existe');

    await user.click(screen.getByRole('link', { name: 'Voltar ao início' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
  });

  it.each([
    ['superuser', '/admin'],
    ['trainer', '/trainer'],
    ['client', '/portal'],
  ] as const)('leads a %s back to %s', async (role, home) => {
    server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/nao-existe'] });

    // O texto do link é igual antes e depois do arranque; esperar pelo destino certo.
    await waitFor(() =>
      expect(screen.getByRole('link', { name: 'Voltar ao início' })).toHaveAttribute('href', home)
    );
    await user.click(screen.getByRole('link', { name: 'Voltar ao início' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(home));
  });

  it('points to the root while the session is still restoring', async () => {
    const restore = pendingRestore();
    server.use(...restore.handlers);
    renderApp({ initialEntries: ['/nao-existe'] });

    expect(await screen.findByRole('link', { name: 'Voltar ao início' })).toHaveAttribute(
      'href',
      '/'
    );

    restore.release();
    await waitFor(() =>
      expect(screen.getByRole('link', { name: 'Voltar ao início' })).toHaveAttribute(
        'href',
        '/entrar'
      )
    );
  });

  it('keeps the 404 for an unknown path inside the trainer area', async () => {
    server.use(...restorableSession({ role: 'trainer', trainer_id: 't-1' }));
    const { router } = renderApp({ initialEntries: ['/trainer/nao-existe'] });

    expect(
      await screen.findByRole('heading', { name: 'Página não encontrada' })
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/trainer/nao-existe');
  });
});
