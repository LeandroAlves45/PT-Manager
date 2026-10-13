import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { getSession } from '@/shared/api/session';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { portalProfile } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const PROFILE = `${API}/portal/my-profile`;
const USER_ID = '11111111-1111-1111-1111-111111111111';

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

  it("shows the client's name and initials, from the portal profile", async () => {
    server.use(...restorableSession({ role: 'client' }));
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/today'] });

    const trigger = await screen.findByRole('button', { name: 'Menu de perfil' });
    expect(await within(trigger).findByTestId('client-avatar-initials')).toHaveTextContent('JP');

    trigger.focus();
    await user.keyboard('{Enter}');
    const menu = await screen.findByRole('menu');
    expect(within(menu).getByText('João Pereira')).toBeInTheDocument();
    expect(within(menu).getByText('Cliente')).toBeInTheDocument();
    // O identificador técnico deixa de aparecer quando há nome.
    expect(within(menu).queryByText(USER_ID)).not.toBeInTheDocument();
  });

  it("shows the client's photo when there is one", async () => {
    const photo = 'https://res.cloudinary.com/demo/image/upload/v1/pt-manager/avatar-1.webp';
    server.use(
      ...restorableSession({ role: 'client' }),
      http.get(PROFILE, () => HttpResponse.json(portalProfile({ avatar_url: photo })))
    );
    renderApp({ initialEntries: ['/portal/today'] });

    const trigger = await screen.findByRole('button', { name: 'Menu de perfil' });
    expect(await within(trigger).findByTestId('client-avatar-image')).toHaveAttribute('src', photo);
  });

  it('opens the profile page from the menu', async () => {
    const { user, router } = await openProfileMenu('client', '/portal/today');

    await user.click(screen.getByRole('menuitem', { name: 'O meu perfil' }));

    expect(await screen.findByRole('heading', { name: 'Perfil' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/portal/profile');
  });

  it('keeps working with the technical identifier when the profile fails to load', async () => {
    server.use(
      http.get(PROFILE, () =>
        HttpResponse.json(problem('portal_profile_not_available'), { status: 404 })
      )
    );
    await openProfileMenu('client', '/portal/today');

    expect(screen.getByText('Cliente')).toBeInTheDocument();
    expect(screen.getByText(USER_ID)).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'O meu perfil' })).toBeInTheDocument();
  });

  it.each([
    ['superuser', '/admin', 'Administrador'],
    ['trainer', '/trainer', 'Personal Trainer'],
  ] as const)('never asks for a client profile as %s', async (role, entry, label) => {
    let requests = 0;
    server.use(
      http.get(PROFILE, () => {
        requests += 1;
        return HttpResponse.json(portalProfile());
      })
    );
    await openProfileMenu(role, entry);

    expect(screen.getByText(label)).toBeInTheDocument();
    expect(screen.getByText(USER_ID)).toBeInTheDocument();
    expect(screen.queryByText('João Pereira')).not.toBeInTheDocument();
    expect(requests).toBe(0);
  });
});
