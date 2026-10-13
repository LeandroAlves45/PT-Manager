import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { portalKeys } from '@/features/portal';
import type { components } from '@/shared/api/schema';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { nutritionPlan } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

type NutritionPlan = components['schemas']['MyNutritionPlanResponse'];

function useNutrition(body: NutritionPlan) {
  server.use(http.get(`${API}/portal/my-nutrition`, () => HttpResponse.json(body)));
}

async function openNutrition() {
  server.use(...restorableSession({ role: 'client' }));
  const view = renderApp({ initialEntries: ['/portal/nutrition'] });
  await screen.findByRole('heading', { name: 'Nutrição' });
  return view;
}

describe('PortalNutritionPage', () => {
  it('shows the target next to what the plan adds up to, from a single request', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/portal/my-nutrition`, () => {
        requests += 1;
        return HttpResponse.json(nutritionPlan());
      })
    );

    await openNutrition();

    expect(screen.getByText('Desde 05/10/2026')).toBeInTheDocument();
    expect(screen.getByText('Manutenção - 2400 kcal')).toBeInTheDocument();
    expect(screen.getByText('Bebe pelo menos 2 litros de água por dia.')).toBeInTheDocument();

    const table = screen.getByRole('table', { name: 'Objetivo diário e o que este plano dá' });
    expect(within(table).getByRole('columnheader', { name: 'Objetivo' })).toBeInTheDocument();
    expect(within(table).getByRole('columnheader', { name: 'Este plano dá' })).toBeInTheDocument();
    expect(within(table).getByRole('row', { name: 'Energia 2400 kcal 926 kcal' })).toBeVisible();
    expect(within(table).getByRole('row', { name: 'Proteína 180 g 79,3 g' })).toBeVisible();
    expect(within(table).getByRole('row', { name: 'Hidratos 270 g 99,4 g' })).toBeVisible();
    expect(within(table).getByRole('row', { name: 'Gordura 67 g 19,1 g' })).toBeVisible();
    // A fibra não tem objetivo no plano: travessão, não um zero inventado.
    expect(within(table).getByRole('row', { name: 'Fibra — 9,1 g' })).toBeVisible();
    // O backend não sabe o que o cliente comeu: nada no ecrã o pode sugerir.
    expect(screen.queryByText(/comeste|consumid/i)).not.toBeInTheDocument();
    expect(requests).toBe(1);
  });

  it('lists each meal with its totals, foods in grams and kcal, and supplements', async () => {
    await openNutrition();

    const meals = screen.getAllByRole('article');
    expect(meals).toHaveLength(2);

    const breakfast = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(meals[0]).toBe(breakfast);
    expect(within(breakfast).getByRole('heading', { name: 'Pequeno-almoço' })).toBeVisible();
    expect(within(breakfast).getByText('434 kcal')).toBeInTheDocument();
    expect(
      within(breakfast).getByText('Proteína 19,4 g · Hidratos 57,4 g · Gordura 12,2 g')
    ).toBeInTheDocument();
    const foods = within(breakfast).getAllByRole('listitem');
    expect(foods[0]).toHaveTextContent('Aveia');
    expect(foods[0]).toHaveTextContent('80 g · 298 kcal');
    expect(foods[1]).toHaveTextContent('Iogurte natural');
    expect(foods[1]).toHaveTextContent('200 g · 136 kcal');
    expect(within(breakfast).getByRole('heading', { name: 'Suplementos' })).toBeVisible();
    expect(within(breakfast).getByText('Creatina · 5 g')).toBeInTheDocument();
    expect(within(breakfast).getByText('Com água.')).toBeInTheDocument();
    expect(within(breakfast).queryByText('Indisponível')).not.toBeInTheDocument();
    expect(within(breakfast).queryByText(/já não está disponível/)).not.toBeInTheDocument();

    const lunch = screen.getByRole('article', { name: 'Almoço' });
    expect(within(lunch).getByText('492 kcal')).toBeInTheDocument();
    expect(within(lunch).getByText('Peito de frango')).toBeInTheDocument();
    // Sem suplementos na refeição, não há um título vazio.
    expect(within(lunch).queryByRole('heading', { name: 'Suplementos' })).not.toBeInTheDocument();

    // Só leitura: nada para escrever nem registar.
    const main = screen.getByRole('main');
    expect(within(main).queryByRole('textbox')).not.toBeInTheDocument();
    expect(within(main).queryByRole('button')).not.toBeInTheDocument();
  });

  it('translates a blocked food and flags a supplement that is no longer available', async () => {
    const plan = nutritionPlan();
    const [breakfast, lunch] = plan.meals;
    useNutrition({
      ...plan,
      meals: [
        {
          ...breakfast!,
          items: [
            { ...breakfast!.items[0]!, food_name: 'Unavailable food', is_unavailable: true },
            breakfast!.items[1]!,
          ],
          supplements: [{ ...breakfast!.supplements[0]!, is_unavailable: true, notes: null }],
        },
        lunch!,
      ],
    });

    await openNutrition();

    const card = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(card).getByText('Alimento indisponível')).toBeInTheDocument();
    expect(screen.queryByText('Unavailable food')).not.toBeInTheDocument();
    // As gramas e as kcal do alimento bloqueado continuam a contar nos totais do backend.
    expect(within(card).getAllByRole('listitem')[0]).toHaveTextContent('80 g · 298 kcal');
    expect(within(card).getByText('Indisponível')).toBeInTheDocument();
    // A etiqueta sozinha não diz o que fazer: a frase diz.
    expect(
      within(card).getByText(
        'Este suplemento já não está disponível. Fala com o teu personal trainer.'
      )
    ).toBeInTheDocument();
    expect(within(card).queryByText('Com água.')).not.toBeInTheDocument();
  });

  it('shows the period when the plan ends and hides what the plan does not have', async () => {
    const plan = nutritionPlan();
    useNutrition({
      ...plan,
      description: null,
      ends_date: '2026-11-01',
      actual_totals: { ...plan.actual_totals, fiber_grams: 0 },
      meals: [{ ...plan.meals[0]!, items: [], supplements: [] }],
    });

    await openNutrition();

    expect(screen.getByText('05/10/2026 a 01/11/2026')).toBeInTheDocument();
    expect(screen.queryByText('Bebe pelo menos 2 litros de água por dia.')).not.toBeInTheDocument();
    expect(screen.queryByRole('row', { name: /Fibra/ })).not.toBeInTheDocument();
    expect(screen.getAllByRole('row')).toHaveLength(5);
    expect(screen.getByText('Esta refeição ainda não tem alimentos.')).toBeInTheDocument();
  });

  it('lists foods and supplements that share an order number', async () => {
    // Só as refeições têm ordem única na base de dados (`unique_meal_order`).
    const errors = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const plan = nutritionPlan();
    const meal = plan.meals[0]!;
    useNutrition({
      ...plan,
      meals: [
        {
          ...meal,
          items: [meal.items[0]!, { ...meal.items[1]!, order_number: 1 }],
          supplements: [
            meal.supplements[0]!,
            { ...meal.supplements[0]!, supplement_name: 'Magnésio', notes: null },
          ],
        },
      ],
    });

    await openNutrition();

    const card = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(card).getByText('Aveia')).toBeInTheDocument();
    expect(within(card).getByText('Iogurte natural')).toBeInTheDocument();
    expect(within(card).getByText('Creatina · 5 g')).toBeInTheDocument();
    expect(within(card).getByText('Magnésio · 5 g')).toBeInTheDocument();
    // Chaves repetidas fariam o React avisar ("two children with the same key").
    expect(errors.mock.calls.flat().join(' ')).not.toMatch(/same key/);
    errors.mockRestore();
  });

  it('says so when the plan has no meals yet', async () => {
    useNutrition(nutritionPlan({ meals: [] }));

    await openNutrition();

    expect(screen.getByText('O plano ainda não tem refeições.')).toBeInTheDocument();
    expect(screen.queryByRole('article')).not.toBeInTheDocument();
    // Os objetivos existem mesmo sem refeições.
    expect(screen.getByRole('row', { name: /Energia 2400 kcal/ })).toBeInTheDocument();
  });

  it('shows an empty state, without retry, when there is no active plan', async () => {
    server.use(
      http.get(`${API}/portal/my-nutrition`, () =>
        HttpResponse.json(problem('portal_nutrition_plan_not_available'), { status: 404 })
      ),
      ...restorableSession({ role: 'client' })
    );

    renderApp({ initialEntries: ['/portal/nutrition'] });

    expect(await screen.findByRole('heading', { name: 'Sem plano alimentar' })).toBeInTheDocument();
    // O título do ecrã mantém-se no estado vazio: o cliente sabe onde está.
    expect(screen.getByRole('heading', { name: 'Nutrição', level: 1 })).toBeInTheDocument();
    expect(
      screen.getByText('O teu personal trainer ainda não te atribuiu um plano alimentar.')
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
  });

  it('lets the client retry after a technical failure', async () => {
    let down = true;
    server.use(
      http.get(`${API}/portal/my-nutrition`, () =>
        down
          ? HttpResponse.json(problem('internal_error'), { status: 500 })
          : HttpResponse.json(nutritionPlan())
      ),
      ...restorableSession({ role: 'client' })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/nutrition'] });

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar.');

    down = false;
    await user.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    expect(await screen.findByRole('heading', { name: 'Nutrição' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('is reached from the bottom bar', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/portal'] });

    const nav = await screen.findByRole('navigation', { name: 'Navegação do portal' });
    await user.click(within(nav).getByRole('link', { name: 'Nutrição' }));

    expect(await screen.findByRole('heading', { name: 'Nutrição' })).toBeInTheDocument();
    expect(screen.getByRole('article', { name: 'Almoço' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/portal/nutrition');
  });

  it('sends a trainer back to their own area without asking for the plan', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/portal/my-nutrition`, () => {
        requests += 1;
        return HttpResponse.json(nutritionPlan());
      }),
      ...restorableSession({ role: 'trainer' })
    );

    const { router } = renderApp({ initialEntries: ['/portal/nutrition'] });

    await screen.findByRole('heading', { name: 'Bom treino' });
    expect(router.state.location.pathname).toBe('/trainer');
    expect(requests).toBe(0);
  });

  it('links the supplements of a meal to the screen where intakes are registered', async () => {
    const { router } = await openNutrition();

    const links = screen.getAllByRole('link', { name: 'Registar tomas' });
    await userEvent.click(links[0]!);

    expect(await screen.findByRole('heading', { name: 'Suplementos' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/portal/supplements');
  });

  it('offers no link to the intakes in a meal without supplements', async () => {
    const plan = nutritionPlan();
    useNutrition({ ...plan, meals: plan.meals.map((meal) => ({ ...meal, supplements: [] })) });

    await openNutrition();

    expect(screen.queryByRole('link', { name: 'Registar tomas' })).not.toBeInTheDocument();
  });

  it('keeps what was on screen and warns when a refresh fails, then recovers on retry', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const { queryClient } = renderApp({ initialEntries: ['/portal/nutrition'] });
    await screen.findByRole('heading', { name: 'Nutrição' });

    server.use(
      http.get(`${API}/portal/my-nutrition`, () =>
        HttpResponse.json(problem('Internal server error'), { status: 500 })
      )
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.nutrition() });

    expect(
      await screen.findByText(
        'Não foi possível atualizar. Estás a ver os últimos dados carregados.'
      )
    ).toBeInTheDocument();
    expect(screen.getByText('Desde 05/10/2026')).toBeInTheDocument();
    // É um aviso, não o ecrã de erro.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    server.use(http.get(`${API}/portal/my-nutrition`, () => HttpResponse.json(nutritionPlan())));
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    await waitFor(() =>
      expect(
        screen.queryByText('Não foi possível atualizar. Estás a ver os últimos dados carregados.')
      ).not.toBeInTheDocument()
    );
    expect(screen.getByText('Desde 05/10/2026')).toBeInTheDocument();
  });

  it('drops the cached data when the refresh says the plan is gone', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const { queryClient } = renderApp({ initialEntries: ['/portal/nutrition'] });
    await screen.findByRole('heading', { name: 'Nutrição' });

    server.use(
      http.get(`${API}/portal/my-nutrition`, () =>
        HttpResponse.json(problem('portal_nutrition_plan_not_available'), { status: 404 })
      )
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.nutrition() });

    expect(await screen.findByRole('heading', { name: 'Sem plano alimentar' })).toBeInTheDocument();
    expect(screen.queryByText('Desde 05/10/2026')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
  });
});
