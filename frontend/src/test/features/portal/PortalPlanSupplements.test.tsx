import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import {
  PLAN_MEAL_SUPPLEMENT_ID,
  PLAN_OUTSIDE_SUPPLEMENT_ID,
  planSupplementItem,
  planSupplementsToday,
} from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const PLAN_INTAKE_PATH = `${API}/portal/my-plan-supplements/:itemId/intakes/today`;

function usePlanSupplements(response: ReturnType<typeof planSupplementsToday>) {
  server.use(
    http.get(`${API}/portal/my-plan-supplements/today`, () => HttpResponse.json(response))
  );
}

async function openSupplements() {
  server.use(...restorableSession({ role: 'client' }));
  const view = renderApp({ initialEntries: ['/portal/supplements'] });
  await screen.findByRole('heading', { name: 'Suplementos' });
  return view;
}

function planSection(): HTMLElement {
  return screen.getByRole('region', { name: 'Do teu plano alimentar' });
}

describe('PortalSupplementsPage: supplements of the meal plan', () => {
  beforeEach(() => {
    toastMock.error.mockClear();
  });

  it('shows the two sections, the plan one first', async () => {
    usePlanSupplements(
      planSupplementsToday({
        items: [
          planSupplementItem(),
          planSupplementItem({
            id: PLAN_OUTSIDE_SUPPLEMENT_ID,
            placement: 'plan',
            meal_type: null,
            meal_order_number: null,
            supplement_name: 'Vitamina D',
            unit_of_measure: 'cápsula',
            quantity: 1,
            order_number: 2,
            notes: 'Com a refeição mais gorda.',
          }),
        ],
      })
    );

    await openSupplements();

    const headings = screen
      .getAllByRole('heading', { level: 2 })
      .map((heading) => heading.textContent);
    expect(headings.indexOf('Do teu plano alimentar')).toBeLessThan(
      headings.indexOf('Do teu personal trainer')
    );

    const plan = await waitFor(() => {
      const section = planSection();
      expect(within(section).getAllByRole('listitem')).toHaveLength(2);
      return section;
    });
    const rows = within(plan).getAllByRole('listitem');
    expect(rows[0]).toHaveTextContent('Creatina do plano');
    expect(rows[0]).toHaveTextContent('5 g · Pequeno-almoço');
    expect(rows[1]).toHaveTextContent('Vitamina D');
    expect(rows[1]).toHaveTextContent('1 cápsula · Fora das refeições');
    expect(rows[1]).toHaveTextContent('Com a refeição mais gorda.');
    expect(within(plan).getByText(/0 de 2 tomadas hoje/)).toBeInTheDocument();
  });

  it('asks the client to choose a plan, with a link and no intake, when none is chosen', async () => {
    server.use(
      http.get(`${API}/portal/my-plan-supplements/today`, () =>
        HttpResponse.json(problem('portal_nutrition_plan_not_selected'), { status: 404 })
      )
    );

    await openSupplements();

    const card = await screen.findByRole('region', { name: 'Escolhe o teu plano' });
    expect(within(card).getByRole('link', { name: 'Escolher plano alimentar' })).toHaveAttribute(
      'href',
      '/portal/nutrition'
    );
    expect(
      within(planSection()).queryByRole('button', { name: /toma de/ })
    ).not.toBeInTheDocument();
    // As tomas diretas do personal trainer não dependem do plano.
    expect(screen.getByRole('button', { name: 'Desmarcar toma de Creatina' })).toBeInTheDocument();
  });

  it('marks and unmarks the intake of a plan item with PUT and DELETE on its id', async () => {
    let item = planSupplementItem();
    const calls: string[] = [];
    server.use(
      http.get(`${API}/portal/my-plan-supplements/today`, () =>
        HttpResponse.json(planSupplementsToday({ items: [item] }))
      ),
      http.put(PLAN_INTAKE_PATH, ({ params }) => {
        calls.push(`PUT ${String(params.itemId)}`);
        item = { ...item, taken_at: '2026-10-06T09:00:00Z' };
        return HttpResponse.json({
          item_id: PLAN_MEAL_SUPPLEMENT_ID,
          local_date: '2026-10-06',
          taken_at: item.taken_at,
        });
      }),
      http.delete(PLAN_INTAKE_PATH, ({ params }) => {
        calls.push(`DELETE ${String(params.itemId)}`);
        item = { ...item, taken_at: null };
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    await openSupplements();

    await user.click(
      await screen.findByRole('button', {
        name: 'Marcar toma de Creatina do plano (Pequeno-almoço)',
      })
    );
    const unmark = await screen.findByRole('button', {
      name: 'Desmarcar toma de Creatina do plano (Pequeno-almoço)',
    });
    expect(unmark).toHaveAttribute('aria-pressed', 'true');
    expect(within(planSection()).getByText('Tomado')).toBeInTheDocument();

    await user.click(unmark);
    await screen.findByRole('button', {
      name: 'Marcar toma de Creatina do plano (Pequeno-almoço)',
    });

    expect(calls).toEqual([`PUT ${PLAN_MEAL_SUPPLEMENT_ID}`, `DELETE ${PLAN_MEAL_SUPPLEMENT_ID}`]);
  });

  it('shows an unavailable item without a way to mark it, but lets today intake be unmarked', async () => {
    usePlanSupplements(
      planSupplementsToday({
        items: [
          planSupplementItem({ is_unavailable: true }),
          planSupplementItem({
            id: PLAN_OUTSIDE_SUPPLEMENT_ID,
            placement: 'plan',
            meal_type: null,
            supplement_name: 'Vitamina D',
            is_unavailable: true,
            taken_at: '2026-10-06T08:00:00Z',
          }),
        ],
      })
    );

    await openSupplements();

    expect(
      await screen.findByRole('button', {
        name: 'Marcar toma de Creatina do plano (Pequeno-almoço)',
      })
    ).toBeDisabled();
    expect(
      screen.getByRole('button', { name: 'Desmarcar toma de Vitamina D (Fora das refeições)' })
    ).toBeEnabled();
    expect(within(planSection()).getAllByText('Indisponível')).toHaveLength(2);
    // Só a toma ainda marcada conta para o total.
    expect(within(planSection()).getByText(/1 de 1 tomadas hoje/)).toBeInTheDocument();
  });

  it('explains a plan intake rejected by the server with the real error body', async () => {
    usePlanSupplements(planSupplementsToday({ items: [planSupplementItem()] }));
    server.use(
      http.put(PLAN_INTAKE_PATH, () =>
        HttpResponse.json(problem('portal_plan_supplement_not_found'), { status: 404 })
      )
    );
    const user = userEvent.setup();
    await openSupplements();

    await user.click(
      await screen.findByRole('button', {
        name: 'Marcar toma de Creatina do plano (Pequeno-almoço)',
      })
    );

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith(
        'Este suplemento já não faz parte do teu plano alimentar.'
      )
    );
  });

  it('keeps the trainer section working when the plan section fails', async () => {
    server.use(
      http.get(`${API}/portal/my-plan-supplements/today`, () =>
        HttpResponse.json(problem('internal_server_error'), { status: 500 })
      )
    );

    await openSupplements();

    expect(
      await within(planSection()).findByText(
        'Não foi possível carregar os suplementos do plano alimentar.'
      )
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Desmarcar toma de Creatina' })).toBeInTheDocument();
  });
});
