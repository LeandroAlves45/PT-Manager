import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { setViewport } from '@/test/browser-fakes';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

function useSubscription(body: Record<string, unknown>, status = 200) {
  server.use(http.get(`${API}/billing/subscription`, () => HttpResponse.json(body, { status })));
}

async function openAs(role: 'superuser' | 'trainer', entry: string) {
  server.use(...restorableSession({ role, trainer_id: role === 'trainer' ? 't-1' : null }));
  const view = renderApp({ initialEntries: [entry] });
  const nav = await screen.findByRole('navigation', { name: 'Navegação principal' });
  return { ...view, nav };
}

describe('AppShell on desktop', () => {
  beforeEach(() => setViewport(1440));

  it('shows the trainer navigation and the client usage of a limited plan', async () => {
    useSubscription({ tier: 'BASIC', status: 'active', client_limit: 10, current_client_count: 3 });

    const { nav } = await openAs('trainer', '/trainer');

    expect(within(nav).getByRole('link', { name: 'Clientes' })).toHaveAttribute(
      'href',
      '/trainer/clients'
    );
    expect(await screen.findByText('3 de 10 clientes')).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(screen.queryByText('Pagamento por regularizar')).not.toBeInTheDocument();
  });

  it('shows an unlimited plan without a progress bar', async () => {
    useSubscription({
      tier: 'PRO',
      status: 'trialing',
      client_limit: null,
      current_client_count: 42,
    });

    await openAs('trainer', '/trainer');

    expect(await screen.findByText('42 clientes · ilimitado')).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('flags a subscription with a payment problem', async () => {
    useSubscription({
      tier: 'BASIC',
      status: 'past_due',
      client_limit: 10,
      current_client_count: 3,
    });

    await openAs('trainer', '/trainer');

    expect(await screen.findByText('Pagamento por regularizar')).toBeInTheDocument();
  });

  it('keeps the navigation working when the subscription cannot be loaded', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/billing/subscription`, () => {
        requests += 1;
        return HttpResponse.json(problem('internal_error'), { status: 500 });
      })
    );

    const { nav } = await openAs('trainer', '/trainer');

    await waitFor(() => expect(requests).toBeGreaterThan(0));
    await waitFor(() => expect(screen.queryByText(/clientes/)).not.toBeInTheDocument());
    expect(within(nav).getByRole('link', { name: 'Clientes' })).toBeInTheDocument();
  });

  it('collapses the sidebar and hides the subscription card', async () => {
    useSubscription({ tier: 'BASIC', status: 'active', client_limit: 10, current_client_count: 3 });
    await openAs('trainer', '/trainer');
    await screen.findByText('3 de 10 clientes');

    await userEvent.click(screen.getByRole('button', { name: 'Recolher navegação' }));

    expect(screen.getByRole('button', { name: 'Expandir navegação' })).toBeInTheDocument();
    expect(screen.queryByText('3 de 10 clientes')).not.toBeInTheDocument();
  });

  it('does not load a subscription for the superuser', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/billing/subscription`, () => {
        requests += 1;
        return HttpResponse.json({});
      })
    );

    const { nav } = await openAs('superuser', '/admin');

    expect(within(nav).getByRole('link', { name: 'Moderação' })).toBeInTheDocument();
    expect(requests).toBe(0);
  });
});

describe('AppShell on mobile', () => {
  it('opens the navigation drawer and closes it after navigating', async () => {
    server.use(...restorableSession({ role: 'trainer' }));
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer'] });

    await user.click(await screen.findByRole('button', { name: 'Abrir navegação' }));
    const nav = await screen.findByRole('navigation', { name: 'Navegação principal' });
    await user.click(within(nav).getByRole('link', { name: 'Check-ins' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer/check-ins'));
    await waitFor(() =>
      expect(
        screen.queryByRole('navigation', { name: 'Navegação principal' })
      ).not.toBeInTheDocument()
    );
  });
});
