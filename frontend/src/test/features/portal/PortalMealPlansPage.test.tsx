import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import {
  mealPlans,
  mealPlanSummary,
  nutritionPlan,
  REST_DAY_MEAL_PLAN_ID,
} from '@/test/msw/portal-fixtures';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const MAINTENANCE_ID = mealPlanSummary().id;

/**
 * Simula a conta: o `PUT` grava a escolha e as leituras seguintes (`my-meal-plans` e
 * `my-nutrition`) respondem de acordo com ela. `selected` começa como pedido (`null` = sem escolha).
 */
function useAccount(initial: string | null) {
  const state = { selected: initial, bodies: [] as unknown[] };
  server.use(
    http.get(`${API}/portal/my-meal-plans`, () =>
      HttpResponse.json(
        mealPlans(
          mealPlans().items.map((plan) => ({ ...plan, is_selected: plan.id === state.selected }))
        )
      )
    ),
    http.get(`${API}/portal/my-nutrition`, () => {
      if (state.selected === null)
        return HttpResponse.json(problem('portal_nutrition_plan_not_selected'), { status: 404 });
      return HttpResponse.json(
        state.selected === REST_DAY_MEAL_PLAN_ID
          ? nutritionPlan({ id: REST_DAY_MEAL_PLAN_ID, name: 'Dias de descanso' })
          : nutritionPlan()
      );
    }),
    http.put(`${API}/portal/my-meal-plans/selection`, async ({ request }) => {
      const body = (await request.json()) as { meal_plan_id: string };
      state.bodies.push(body);
      state.selected = body.meal_plan_id;
      return new HttpResponse(null, { status: 204 });
    })
  );
  return state;
}

function planCard(name: string): HTMLElement {
  const card = screen.getByRole('heading', { name }).closest('li');
  if (card === null) throw new Error(`Sem cartão para ${name}`);
  return card;
}

describe('Escolha do plano alimentar', () => {
  it('without a choice shows the list on the nutrition page and never picks one alone', async () => {
    useAccount(null);
    server.use(...restorableSession({ role: 'client' }));

    renderApp({ initialEntries: ['/portal/nutrition'] });

    expect(await screen.findByRole('heading', { name: 'Nutrição', level: 1 })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Dias de descanso' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Manutenção - 2400 kcal' })).toBeInTheDocument();
    // Nenhuma refeição nem barras: não há plano aberto.
    expect(screen.queryByRole('article')).not.toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(screen.queryByText('A seguir')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Escolher este plano' })).toHaveLength(2);
  });

  it('does not pick the only plan for the client either', async () => {
    useAccount(null);
    server.use(
      http.get(`${API}/portal/my-meal-plans`, () =>
        HttpResponse.json(mealPlans([mealPlanSummary({ is_selected: false })]))
      ),
      ...restorableSession({ role: 'client' })
    );

    renderApp({ initialEntries: ['/portal/nutrition'] });

    expect(await screen.findByRole('button', { name: 'Escolher este plano' })).toBeEnabled();
    expect(screen.queryByRole('article')).not.toBeInTheDocument();
  });

  it('saves the chosen plan id with a PUT and then shows that plan', async () => {
    const account = useAccount(null);
    server.use(...restorableSession({ role: 'client' }));
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/nutrition'] });
    await screen.findByRole('heading', { name: 'Dias de descanso' });

    await user.click(within(planCard('Dias de descanso')).getByRole('button'));

    expect(await screen.findByRole('article', { name: 'Almoço' })).toBeInTheDocument();
    expect(account.bodies).toEqual([{ meal_plan_id: REST_DAY_MEAL_PLAN_ID }]);
    expect(screen.getByText('Dias de descanso')).toBeInTheDocument();
    expect(screen.getAllByRole('progressbar').length).toBeGreaterThan(0);
    // O botão escolhido desapareceu com a lista: o foco passa para o título do plano.
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1, name: 'Nutrição' })).toHaveFocus()
    );
  });

  it('with a choice shows the plan and a "Trocar de plano" link to the list', async () => {
    useAccount(MAINTENANCE_ID);
    server.use(...restorableSession({ role: 'client' }));
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/portal/nutrition'] });

    const link = await screen.findByRole('link', { name: 'Trocar de plano' });
    await user.click(link);

    expect(await screen.findByRole('heading', { name: 'Planos alimentares' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/portal/nutrition/planos');
  });

  it('hides "Trocar de plano" when there is only one active plan', async () => {
    useAccount(MAINTENANCE_ID);
    server.use(
      http.get(`${API}/portal/my-meal-plans`, () =>
        HttpResponse.json(mealPlans([mealPlanSummary()]))
      ),
      ...restorableSession({ role: 'client' })
    );

    renderApp({ initialEntries: ['/portal/nutrition'] });

    await screen.findByRole('article', { name: 'Almoço' });
    expect(screen.queryByRole('link', { name: 'Trocar de plano' })).not.toBeInTheDocument();
  });

  it('lists every plan with goal word, kcal, meal count and dates, and marks the one in use', async () => {
    useAccount(MAINTENANCE_ID);
    server.use(...restorableSession({ role: 'client' }));
    renderApp({ initialEntries: ['/portal/nutrition/planos'] });

    await screen.findByRole('heading', { name: 'Planos alimentares', level: 1 });

    const current = planCard('Manutenção - 2400 kcal');
    expect(within(current).getByText('A seguir')).toBeInTheDocument();
    expect(within(current).getByText('Manutenção')).toBeInTheDocument();
    expect(current).toHaveTextContent('2400 kcal');
    expect(current).toHaveTextContent('2 refeições · Desde 05/10/2026');
    // O plano em uso não tem botão: escolher o mesmo não faz nada.
    expect(within(current).queryByRole('button')).not.toBeInTheDocument();

    const rest = planCard('Dias de descanso');
    expect(within(rest).queryByText('A seguir')).not.toBeInTheDocument();
    expect(within(rest).getByText('Défice')).toBeInTheDocument();
    expect(rest).toHaveTextContent('2000 kcal');
    expect(rest).toHaveTextContent('3 refeições');
    expect(within(rest).getByRole('button', { name: 'Escolher este plano' })).toBeEnabled();
    // Fórmula, idade, sexo e ajuste não se mostram.
    expect(screen.queryByText(/mifflin|harris|fórmula|idade|sexo|ajuste/i)).toBeNull();
  });

  it('choosing on the list saves and goes back to the nutrition page with that plan', async () => {
    const account = useAccount(MAINTENANCE_ID);
    server.use(...restorableSession({ role: 'client' }));
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/portal/nutrition/planos'] });
    await screen.findByRole('heading', { name: 'Dias de descanso' });

    await user.click(
      within(planCard('Dias de descanso')).getByRole('button', { name: 'Escolher este plano' })
    );

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal/nutrition'));
    expect(await screen.findByText('Dias de descanso')).toBeInTheDocument();
    expect(account.bodies).toEqual([{ meal_plan_id: REST_DAY_MEAL_PLAN_ID }]);
  });

  it('disables the buttons and says it is saving while the choice is written', async () => {
    useAccount(MAINTENANCE_ID);
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.put(`${API}/portal/my-meal-plans/selection`, async () => {
        await gate;
        return new HttpResponse(null, { status: 204 });
      }),
      ...restorableSession({ role: 'client' })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/nutrition/planos'] });
    await screen.findByRole('heading', { name: 'Dias de descanso' });

    await user.click(within(planCard('Dias de descanso')).getByRole('button'));

    const saving = await within(planCard('Dias de descanso')).findByRole('button');
    expect(saving).toBeDisabled();
    expect(saving).toHaveTextContent('A guardar…');
    release();
    await screen.findByRole('heading', { name: 'Nutrição', level: 1 });
  });

  it('says the plan is gone on 404 portal_meal_plan_not_found and reads the list again', async () => {
    let lists = 0;
    let gone = false;
    server.use(
      http.get(`${API}/portal/my-meal-plans`, () => {
        lists += 1;
        return HttpResponse.json(gone ? mealPlans([mealPlanSummary()]) : mealPlans());
      }),
      http.put(`${API}/portal/my-meal-plans/selection`, () => {
        gone = true;
        return HttpResponse.json(problem('portal_meal_plan_not_found'), { status: 404 });
      }),
      ...restorableSession({ role: 'client' })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/portal/nutrition/planos'] });
    await screen.findByRole('heading', { name: 'Dias de descanso' });
    const before = lists;

    await user.click(within(planCard('Dias de descanso')).getByRole('button'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Este plano já não está disponível');
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: 'Dias de descanso' })).not.toBeInTheDocument()
    );
    expect(lists).toBeGreaterThan(before);
    // Fica na lista, sem navegar.
    expect(router.state.location.pathname).toBe('/portal/nutrition/planos');
  });

  it('shows a general message when saving fails for another reason', async () => {
    server.use(
      http.put(`${API}/portal/my-meal-plans/selection`, () =>
        HttpResponse.json(problem('internal_error'), { status: 500 })
      ),
      ...restorableSession({ role: 'client' })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/nutrition/planos'] });
    await screen.findByRole('heading', { name: 'Dias de descanso' });

    await user.click(within(planCard('Dias de descanso')).getByRole('button'));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Não foi possível guardar a escolha'
    );
  });

  it('shows an empty state when there are no active plans', async () => {
    server.use(
      http.get(`${API}/portal/my-meal-plans`, () => HttpResponse.json(mealPlans([]))),
      ...restorableSession({ role: 'client' })
    );

    renderApp({ initialEntries: ['/portal/nutrition/planos'] });

    expect(await screen.findByRole('heading', { name: 'Sem plano alimentar' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Escolher este plano' })).not.toBeInTheDocument();
  });

  it('shows an error with retry when the list fails', async () => {
    let down = true;
    server.use(
      http.get(`${API}/portal/my-meal-plans`, () =>
        down
          ? HttpResponse.json(problem('internal_error'), { status: 500 })
          : HttpResponse.json(mealPlans())
      ),
      ...restorableSession({ role: 'client' })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/nutrition/planos'] });

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar.');

    down = false;
    await user.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    expect(await screen.findByRole('heading', { name: 'Dias de descanso' })).toBeInTheDocument();
  });
});
