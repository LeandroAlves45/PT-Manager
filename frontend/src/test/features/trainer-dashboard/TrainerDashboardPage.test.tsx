import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { dashboardResponse, SESSION_ID } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

describe('TrainerDashboardPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('renders every block from a single aggregated request', async () => {
    let requests = 0;
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () => {
        requests += 1;
        return HttpResponse.json(dashboardResponse());
      })
    );
    renderApp({ initialEntries: ['/trainer'] });

    const reviews = await screen.findByRole('region', { name: 'Check-ins por rever' });
    expect(within(reviews).getByText('7')).toBeInTheDocument();
    expect(within(reviews).getByText(/3 acima de 48 h/)).toBeInTheDocument();
    // O mais antigo é o de menor `responded_at`, não o primeiro da lista.
    expect(
      within(reviews).getByText(/mais antigo: Marta Figueiredo \(11\/09\/2026\)/)
    ).toBeVisible();
    expect(within(reviews).getByRole('link', { name: 'Rever check-ins' })).toHaveAttribute(
      'href',
      '/trainer/check-ins?status=unreviewed'
    );
    expect(screen.getByText('Quarta-feira, 16/09/2026')).toBeInTheDocument();
    expect(screen.getByText('1 restante')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Atribuir plano a Rita Sá' })).toHaveAttribute(
      'href',
      `/trainer/training-plans?client_id=33333333-3333-3333-3333-333333333333`
    );
    expect(screen.getByText(/2480,00\s*€|2\s*480,00\s*€/)).toBeInTheDocument();
    expect(screen.getByText(/\+24 % vs agosto/)).toBeInTheDocument();
    await waitFor(() => expect(requests).toBe(1));
  }, 15000);

  it('omits empty session type and location instead of printing blanks', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () => HttpResponse.json(dashboardResponse()))
    );
    renderApp({ initialEntries: ['/trainer'] });

    expect(await screen.findByText('PT individual · 60 min')).toBeInTheDocument();
    expect(screen.getByText('45 min')).toBeInTheDocument();
    expect(screen.getByText('Realizada')).toBeInTheDocument();
  }, 15000);

  it('words days without a plan for 0 and 1 days and for a new record', async () => {
    const item = { client_name: 'Rita Sá', without_plan_since: '2026-09-15', has_had_plan: true };
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () =>
        HttpResponse.json(
          dashboardResponse({
            clients_without_training_plan: {
              total_count: 3,
              items: [
                { ...item, client_id: 'c-0', days_without_plan: 0 },
                { ...item, client_id: 'c-1', days_without_plan: 1 },
                { ...item, client_id: 'c-2', has_had_plan: false, days_without_plan: 1 },
              ],
            },
          })
        )
      )
    );
    renderApp({ initialEntries: ['/trainer'] });

    expect(await screen.findByText('sem plano desde hoje')).toBeInTheDocument();
    expect(screen.getByText('sem plano há 1 dia')).toBeInTheDocument();
    expect(screen.getByText('ficha nova · 15/09/2026')).toBeInTheDocument();
  }, 15000);

  it('shows onboarding when the trainer has no active clients', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () =>
        HttpResponse.json(dashboardResponse({ active_client_count: 0 }))
      )
    );
    renderApp({ initialEntries: ['/trainer'] });

    expect(await screen.findByText('Ainda não tens clientes')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Criar primeiro cliente' })).toHaveAttribute(
      'href',
      '/trainer/clients?new=true'
    );
    expect(screen.queryByRole('region', { name: 'Check-ins por rever' })).not.toBeInTheDocument();
  }, 15000);

  it('registers attendance and refreshes the dashboard', async () => {
    let dashboardRequests = 0;
    let completed: string | null = null;
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () => {
        dashboardRequests += 1;
        return HttpResponse.json(dashboardResponse());
      }),
      http.post(`${API}/sessions/:sessionId/complete`, ({ params }) => {
        completed = params.sessionId as string;
        return HttpResponse.json({});
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer'] });

    await user.click(
      await screen.findByRole('button', { name: 'Registar presença de Marta Figueiredo' })
    );

    await waitFor(() => expect(completed).toBe(SESSION_ID));
    await waitFor(() => expect(dashboardRequests).toBe(2));
    expect(toastMock.success).toHaveBeenCalledWith(
      'Presença de Marta Figueiredo registada com sucesso.'
    );
  }, 15000);

  it('translates a too-early attendance conflict', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () => HttpResponse.json(dashboardResponse())),
      http.post(`${API}/sessions/:sessionId/complete`, () =>
        HttpResponse.json(problem('session_transition_too_early'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer'] });

    await user.click(
      await screen.findByRole('button', { name: 'Registar presença de Marta Figueiredo' })
    );

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith(
        'Só podes registar a presença depois da hora de início.'
      )
    );
  }, 15000);

  it('does not compare sales in different currencies', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () =>
        HttpResponse.json(
          dashboardResponse({
            pack_sales: {
              current_month: {
                year: 2026,
                month: 9,
                totals: [{ currency: 'EUR', amount_cents: 10000, pack_count: 1 }],
              },
              previous_month: {
                year: 2026,
                month: 8,
                totals: [{ currency: 'GBP', amount_cents: 5000, pack_count: 1 }],
              },
            },
          })
        )
      )
    );
    renderApp({ initialEntries: ['/trainer'] });

    const sales = await screen.findByRole('region', { name: /Vendas de packs/ });
    expect(within(sales).getByText(/100,00\s*€/)).toBeInTheDocument();
    expect(within(sales).queryByText(/vs agosto/)).not.toBeInTheDocument();
  }, 15000);

  it('shows the error state with a retry', async () => {
    let requests = 0;
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () => {
        requests += 1;
        return requests === 1
          ? HttpResponse.json(problem('internal_error'), { status: 500 })
          : HttpResponse.json(dashboardResponse());
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer'] });

    await user.click(await screen.findByRole('button', { name: /Tentar novamente/ }));
    expect(await screen.findByRole('region', { name: 'Check-ins por rever' })).toBeInTheDocument();
  }, 15000);

  it.each(['superuser', 'client'] as const)(
    'redirects a %s before requesting the dashboard',
    async (role) => {
      let requests = 0;
      server.use(
        ...restorableSession({ role, trainer_id: null }),
        http.get(`${API}/dashboard`, () => {
          requests += 1;
          return HttpResponse.json(dashboardResponse());
        }),
        http.get(`${API}/admin/overview`, () => HttpResponse.json({}, { status: 500 }))
      );
      const { router } = renderApp({ initialEntries: ['/trainer'] });

      await waitFor(() =>
        expect(router.state.location.pathname).toBe(
          role === 'superuser' ? '/admin' : '/portal/today'
        )
      );
      expect(requests).toBe(0);
    }
  );
});
