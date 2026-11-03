import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { getSession } from '@/shared/api/session';
import { accountResponse } from '@/test/msw/auth-fixtures';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { portalProfile } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const PROFILE = `${API}/portal/my-profile`;
const ACCOUNT = `${API}/auth/account`;
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
    ['superuser', '/admin', ['Conta e segurança']],
    ['trainer', '/trainer', ['Definições', 'Conta e segurança']],
    ['client', '/portal/today', ['O meu perfil', 'Conta e segurança']],
  ] as const)('offers the %s only the routes that exist', async (role, entry, expected) => {
    await openProfileMenu(role, entry);

    const items = screen.getAllByRole('menuitem').map((item) => item.textContent);
    expect(items).toEqual([...expected, 'Terminar sessão']);
  });

  it.each([
    ['superuser', '/admin', '/admin/account'],
    ['trainer', '/trainer', '/trainer/account'],
    ['client', '/portal/today', '/portal/account'],
  ] as const)('opens the %s account page inside their own area', async (role, entry, target) => {
    const { user, router } = await openProfileMenu(role, entry);

    await user.click(screen.getByRole('menuitem', { name: 'Conta e segurança' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(target));
    expect(
      await screen.findByRole('heading', { name: 'Conta e segurança', level: 1 })
    ).toBeInTheDocument();
  });

  it('signs out and goes back to login', async () => {
    const { user, router } = await openProfileMenu('trainer', '/trainer');

    await user.click(screen.getByRole('menuitem', { name: 'Terminar sessão' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
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

  it('keeps working with a person icon, never the identifier, when the profile fails to load', async () => {
    let accountRequests = 0;
    server.use(
      http.get(PROFILE, () =>
        HttpResponse.json(problem('portal_profile_not_available'), { status: 404 })
      ),
      http.get(ACCOUNT, () => {
        accountRequests += 1;
        return HttpResponse.json(accountResponse({ role: 'client' }));
      })
    );
    await openProfileMenu('client', '/portal/today');

    const trigger = screen.getByRole('button', { name: 'Menu de perfil', hidden: true });
    expect(within(trigger).getByTestId('account-avatar-icon')).toBeInTheDocument();
    expect(screen.getByText('Cliente')).toBeInTheDocument();
    expect(screen.queryByText(USER_ID)).not.toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'O meu perfil' })).toBeInTheDocument();
    // O cliente tem o perfil do portal: a conta não é pedida só para o menu.
    expect(accountRequests).toBe(0);
  });

  it.each([
    ['superuser', '/admin', 'Administrador'],
    ['trainer', '/trainer', 'Personal Trainer'],
  ] as const)(
    'shows the %s initials and name from the account, without a client profile',
    async (role, entry, label) => {
      let profileRequests = 0;
      server.use(
        http.get(PROFILE, () => {
          profileRequests += 1;
          return HttpResponse.json(portalProfile());
        }),
        http.get(ACCOUNT, () =>
          HttpResponse.json(accountResponse({ role, full_name: 'Rita Moreira' }))
        )
      );
      await openProfileMenu(role, entry);

      const trigger = screen.getByRole('button', { name: 'Menu de perfil', hidden: true });
      expect(await within(trigger).findByTestId('account-avatar-initials')).toHaveTextContent('RM');
      const menu = screen.getByRole('menu');
      expect(within(menu).getByText('Rita Moreira')).toBeInTheDocument();
      expect(within(menu).getByText(label)).toBeInTheDocument();
      expect(screen.queryByText(USER_ID)).not.toBeInTheDocument();
      expect(screen.queryByText('João Pereira')).not.toBeInTheDocument();
      expect(profileRequests).toBe(0);
    }
  );

  it.each([
    ['without a name', null],
    ['with a blank name', '   '],
  ] as const)('shows a person icon and only the role for an account %s', async (_, fullName) => {
    server.use(
      http.get(ACCOUNT, () =>
        HttpResponse.json(accountResponse({ role: 'superuser', full_name: fullName }))
      )
    );
    await openProfileMenu('superuser', '/admin');

    const trigger = screen.getByRole('button', { name: 'Menu de perfil', hidden: true });
    expect(await within(trigger).findByTestId('account-avatar-icon')).toBeInTheDocument();
    expect(within(trigger).queryByTestId('account-avatar-initials')).not.toBeInTheDocument();
    expect(screen.getByText('Administrador')).toBeInTheDocument();
    expect(screen.queryByText(USER_ID)).not.toBeInTheDocument();
  });
});
