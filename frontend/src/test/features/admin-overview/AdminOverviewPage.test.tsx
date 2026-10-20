import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const catalogs = {
  global_foods: { total_count: 1284, active_count: 1200, archived_count: 84 },
  global_exercises: { total_count: 72, active_count: 70, archived_count: 2 },
  global_supplements: { total_count: 10, active_count: 9, archived_count: 1 },
  private_foods: { total_count: 3, blocked_count: 1 },
  private_exercises: { total_count: 4, blocked_count: 2 },
};
const emptyCatalogs = {
  global_foods: { total_count: 0, active_count: 0, archived_count: 0 },
  global_exercises: { total_count: 0, active_count: 0, archived_count: 0 },
  global_supplements: { total_count: 0, active_count: 0, archived_count: 0 },
  private_foods: { total_count: 0, blocked_count: 0 },
  private_exercises: { total_count: 0, blocked_count: 0 },
};

function trainers(over: Partial<Record<string, number>> = {}) {
  return {
    active_subscription_count: 5,
    suspended_subscription_count: 0,
    exempt_count: 2,
    revenue_this_month_cents: 2789,
    ...over,
  };
}

function overview(body: Record<string, unknown>) {
  return [
    ...restorableSession({ role: 'superuser', trainer_id: null }),
    http.get(`${API}/admin/overview`, () => HttpResponse.json(body)),
  ];
}

const TRAINER_CARDS = [
  'Subscrições ativas',
  'Suspensas por pagamento',
  'Isentos',
  'Recebido este mês',
];

describe('AdminOverviewPage', () => {
  it('shows counts from one aggregated request', async () => {
    let requests = 0;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/admin/overview`, () => {
        requests += 1;
        return HttpResponse.json({ ...catalogs, trainers: trainers() });
      })
    );
    renderApp({ initialEntries: ['/admin'] });
    expect(await screen.findByText(/1\s*284/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Alimentos globais/ })).toHaveAttribute(
      'href',
      '/admin/catalog/foods'
    );
    await waitFor(() => expect(requests).toBe(1));
  }, 15000);

  it('shows the four trainer figures, euro formatted, and none is a link', async () => {
    server.use(...overview({ ...catalogs, trainers: trainers() }));
    renderApp({ initialEntries: ['/admin'] });
    const active = await screen.findByRole('region', { name: 'Subscrições ativas' });
    expect(within(active).getByText('5 treinadores')).toBeInTheDocument();
    expect(within(active).getByText(/Inclui o período experimental/)).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Suspensas por pagamento' })).getByText(
        '0 treinadores'
      )
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Isentos' })).getByText('2 treinadores isentos')
    ).toBeInTheDocument();
    const revenue = screen.getByRole('region', { name: 'Recebido este mês' });
    // O Intl pt-PT usa espaço não separável; o matcher do Testing Library normaliza-o.
    expect(within(revenue).getByText(/^27,89\s€$/)).toBeInTheDocument();
    for (const name of TRAINER_CARDS) {
      expect(
        within(screen.getByRole('region', { name })).queryByRole('link')
      ).not.toBeInTheDocument();
    }
    expect(
      screen.getByRole('heading', { level: 2, name: 'Treinadores e faturação' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 2, name: 'Catálogos e moderação' })
    ).toBeInTheDocument();
  }, 15000);

  it('uses the singular for one and the plural otherwise', async () => {
    server.use(
      ...overview({
        ...catalogs,
        global_foods: { total_count: 1, active_count: 1, archived_count: 0 },
        trainers: trainers({ active_subscription_count: 1, exempt_count: 1 }),
      })
    );
    renderApp({ initialEntries: ['/admin'] });
    expect(
      within(await screen.findByRole('region', { name: 'Subscrições ativas' })).getByText(
        '1 treinador'
      )
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Isentos' })).getByText('1 treinador isento')
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('link', { name: /Alimentos globais/ })).getByText('1 ativo')
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('link', { name: /Alimentos privados/ })).getByText('1 bloqueado')
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('link', { name: /Exercícios privados/ })).getByText('2 bloqueados')
    ).toBeInTheDocument();
  }, 15000);

  it('highlights the suspended card when there are suspended trainers', async () => {
    server.use(
      ...overview({ ...catalogs, trainers: trainers({ suspended_subscription_count: 3 }) })
    );
    renderApp({ initialEntries: ['/admin'] });
    const suspended = await screen.findByRole('region', { name: 'Suspensas por pagamento' });
    expect(within(suspended).getByText('3 treinadores')).toBeInTheDocument();
    expect(suspended).toHaveAttribute('data-attention', 'true');
    for (const name of TRAINER_CARDS.filter((card) => card !== 'Suspensas por pagamento')) {
      expect(screen.getByRole('region', { name })).not.toHaveAttribute('data-attention');
    }
  }, 15000);

  it('does not highlight the suspended card when there are none', async () => {
    server.use(...overview({ ...catalogs, trainers: trainers() }));
    renderApp({ initialEntries: ['/admin'] });
    expect(
      await screen.findByRole('region', { name: 'Suspensas por pagamento' })
    ).not.toHaveAttribute('data-attention');
  }, 15000);

  it('keeps the trainer section when the catalogs are empty', async () => {
    server.use(...overview({ ...emptyCatalogs, trainers: trainers() }));
    renderApp({ initialEntries: ['/admin'] });
    expect(await screen.findByText('Plataforma sem conteúdo')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Recebido este mês' })).toBeInTheDocument();
  }, 15000);

  it.each(['trainer', 'client'] as const)(
    'redirects a %s before requesting admin data',
    async (role) => {
      let requests = 0;
      server.use(
        ...restorableSession({
          role,
          trainer_id: role === 'trainer' ? '22222222-2222-2222-2222-222222222222' : null,
        }),
        http.get(`${API}/admin/overview`, () => {
          requests += 1;
          return HttpResponse.json({});
        })
      );
      const { router } = renderApp({ initialEntries: ['/admin'] });
      await waitFor(() =>
        expect(router.state.location.pathname).toBe(role === 'trainer' ? '/trainer' : '/portal')
      );
      expect(requests).toBe(0);
    }
  );
});
