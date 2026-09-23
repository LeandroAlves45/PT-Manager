import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { getSession } from '@/shared/api/session';
import { restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

async function openProfileMenu(role: 'superuser' | 'trainer' | 'client', entry: string) {
  server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));
  const user = userEvent.setup();
  const view = renderApp({ initialEntries: [entry] });

  // Abre pelo teclado: no jsdom o clique no trigger do Radix só abre no primeiro teste do
  // ficheiro. O teclado é também o caminho de acessibilidade que interessa garantir.
  (await screen.findByRole('button', { name: 'Menu de perfil' })).focus();
  await user.keyboard('{Enter}');
  await screen.findByRole('menuitem', { name: 'Terminar sessão' });
  return { user, ...view };
}

describe('ProfileMenu', () => {
  it.each([
    ['superuser', '/admin', []],
    ['trainer', '/trainer', ['Definições']],
    ['client', '/portal/today', ['O meu perfil']],
  ] as const)('offers the %s only the routes that exist', async (role, entry, expected) => {
    await openProfileMenu(role, entry);

    const items = screen.getAllByRole('menuitem').map((item) => item.textContent);
    expect(items).toEqual([...expected, 'Terminar sessão']);
  });

  it('signs out and goes back to login', async () => {
    const { user, router } = await openProfileMenu('trainer', '/trainer');

    await user.click(screen.getByRole('menuitem', { name: 'Terminar sessão' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/auth/login'));
    expect(getSession()).toBeNull();
  });
});
