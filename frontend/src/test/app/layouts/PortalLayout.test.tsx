import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { THEME_STORAGE_KEY } from '@/app/providers/theme-context';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { portalBranding, seededBranding } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

function useBranding(body: ReturnType<typeof portalBranding>) {
  server.use(http.get(`${API}/portal/branding`, () => HttpResponse.json(body)));
}

async function openPortal(entry = '/portal') {
  server.use(...restorableSession({ role: 'client' }));
  const view = renderApp({ initialEntries: [entry] });
  const nav = await screen.findByRole('navigation', { name: 'Navegação do portal' });
  return { ...view, nav };
}

const rootStyle = () => document.documentElement.style;

describe('PortalLayout', () => {
  it('shows five bottom items and marks only the home as active on /portal', async () => {
    const { nav } = await openPortal();

    const links = within(nav).getAllByRole('link');
    expect(links.map((link) => link.textContent)).toEqual([
      'Início',
      'Treino',
      'Nutrição',
      'Suplementos',
      'Perfil',
    ]);
    expect(within(nav).getByRole('link', { name: 'Início' })).toHaveAttribute(
      'aria-current',
      'page'
    );
    expect(within(nav).getByRole('link', { name: 'Treino' })).not.toHaveAttribute('aria-current');
  });

  it('does not keep the home active on another portal page', async () => {
    const { nav } = await openPortal('/portal/nutrition');

    expect(within(nav).getByRole('link', { name: 'Nutrição' })).toHaveAttribute(
      'aria-current',
      'page'
    );
    expect(within(nav).getByRole('link', { name: 'Início' })).not.toHaveAttribute('aria-current');
  });

  it('shows the PT Manager logo and keeps the PT Manager tokens without a brand', async () => {
    await openPortal();

    const header = screen.getByTestId('portal-header');
    expect(await within(header).findByText('PT Manager')).toBeInTheDocument();
    expect(within(header).getByTestId('brand-default-logo')).toBeInTheDocument();
    expect(header).not.toHaveAttribute('style');
    expect(rootStyle().getPropertyValue('--primary')).toBe('');
  });

  it('applies the seeded brand adjusted to the light theme, with a monogram', async () => {
    useBranding(seededBranding);
    await openPortal();

    const header = screen.getByTestId('portal-header');
    expect(await within(header).findByText('Salgado Performance')).toBeInTheDocument();
    expect(within(header).getByTestId('brand-monogram')).toHaveTextContent('S');
    expect(header).toHaveStyle({ backgroundColor: '#1f1a17', color: '#ffffff' });
    await waitFor(() => expect(rootStyle().getPropertyValue('--primary')).toBe('#ba5022'));
    expect(rootStyle().getPropertyValue('--primary-foreground')).toBe('#ffffff');
    expect(rootStyle().getPropertyValue('--ring')).toBe('#ba5022');
  });

  it('keeps the original brand colour in the dark theme, with dark text on it', async () => {
    // eslint-disable-next-line no-restricted-globals -- preferência de tema, não é sessão
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');
    useBranding(seededBranding);
    await openPortal();

    await waitFor(() => expect(rootStyle().getPropertyValue('--primary')).toBe('#e8642a'));
    expect(rootStyle().getPropertyValue('--primary-foreground')).toBe('#000000');
  });

  it('shows the trainer logo instead of the monogram', async () => {
    useBranding(
      portalBranding({
        app_name: 'Salgado Performance',
        logo_url: 'https://res.cloudinary.com/demo/image/upload/logo.webp',
      })
    );
    await openPortal();

    const header = screen.getByTestId('portal-header');
    expect(await within(header).findByTestId('brand-logo')).toHaveAttribute(
      'src',
      'https://res.cloudinary.com/demo/image/upload/logo.webp'
    );
    expect(within(header).queryByTestId('brand-monogram')).not.toBeInTheDocument();
  });

  it('removes the brand from the document when the client signs out', async () => {
    useBranding(seededBranding);
    const { router } = await openPortal();
    await waitFor(() => expect(rootStyle().getPropertyValue('--primary')).toBe('#ba5022'));

    await userEvent.click(screen.getByRole('button', { name: 'Menu de perfil' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Terminar sessão' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/auth/login'));
    expect(rootStyle().getPropertyValue('--primary')).toBe('');
    expect(rootStyle().getPropertyValue('--primary-foreground')).toBe('');
    expect(rootStyle().getPropertyValue('--ring')).toBe('');
  });

  it('keeps the portal usable with the PT Manager brand when the branding fails', async () => {
    server.use(
      http.get(`${API}/portal/branding`, () =>
        HttpResponse.json(problem('branding_not_available'), { status: 404 })
      )
    );
    await openPortal();

    const header = screen.getByTestId('portal-header');
    expect(await within(header).findByText('PT Manager')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Hoje' })).toBeInTheDocument();
    expect(rootStyle().getPropertyValue('--primary')).toBe('');
  });
});
