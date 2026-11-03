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

/** Cartão "Metas do dia". */
function goals() {
  return within(screen.getByRole('region', { name: 'Metas do dia' }));
}

/** O mesmo plano com as macros escondidas, como o backend o envia: gramas a null, kcal ficam. */
function planWithoutMacros(): NutritionPlan {
  const plan = nutritionPlan();
  const kcalOnly = (totals: NutritionPlan['actual_totals']) => ({
    ...totals,
    protein_grams: null,
    carbs_grams: null,
    fats_grams: null,
    fiber_grams: null,
  });
  return {
    ...plan,
    show_macros: false,
    protein_target_grams: null,
    carbs_target_grams: null,
    fats_target_grams: null,
    actual_totals: kcalOnly(plan.actual_totals),
    meals: plan.meals.map((meal) => ({
      ...meal,
      totals: kcalOnly(meal.totals),
      items: meal.items.map((item) => ({ ...item, contribution: kcalOnly(item.contribution) })),
    })),
  };
}

async function openNutrition() {
  server.use(...restorableSession({ role: 'client' }));
  const view = renderApp({ initialEntries: ['/portal/nutrition'] });
  await screen.findByRole('heading', { name: 'Nutrição' });
  return view;
}

describe('PortalNutritionPage', () => {
  it('shows the daily targets with each macro in its colour, and no bars', async () => {
    await openNutrition();

    expect(goals().getByText('2400')).toBeInTheDocument();
    expect(goals().getByText('kcal por dia')).toBeInTheDocument();
    const macros = [
      ['Proteína', '180 g', 'text-macro-protein'],
      ['Hidratos', '270 g', 'text-macro-carbs'],
      ['Gordura', '67 g', 'text-macro-fats'],
    ] as const;
    for (const [label, grams, colour] of macros) {
      const term = goals().getByText(label);
      expect(term).toHaveClass(colour);
      expect(term.nextElementSibling).toHaveTextContent(grams);
    }
    // Só o objetivo: nada de barras nem de "quanto o plano já dá".
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(screen.queryByText(/Este plano dá|Objetivo/)).not.toBeInTheDocument();
    expect(goals().queryByText('Fibra')).not.toBeInTheDocument();
  });

  it('hides the meal chips when the flag is off, even if grams came in the response', async () => {
    useNutrition(nutritionPlan({ show_macros: false }));

    await openNutrition();

    const breakfast = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(breakfast).queryByText('Proteína')).not.toBeInTheDocument();
    expect(goals().queryByText('Proteína')).not.toBeInTheDocument();
  });

  it('shows only the kcal when the personal trainer hides the macros', async () => {
    useNutrition(planWithoutMacros());

    await openNutrition();

    expect(goals().getByText('2400')).toBeInTheDocument();
    expect(goals().queryByText(/Proteína|Hidratos|Gordura/)).not.toBeInTheDocument();
    const breakfast = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(breakfast).getByText('434 kcal')).toBeInTheDocument();
    expect(within(breakfast).queryByText('Proteína')).not.toBeInTheDocument();
    expect(within(breakfast).getByText('Aveia').closest('li')).toHaveTextContent('80 g · 298 kcal');
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(screen.queryByText(/null|NaN/)).not.toBeInTheDocument();
  });

  it('shows the goal word and never the formula, age, sex or adjustment', async () => {
    await openNutrition();

    expect(screen.getByText('Manutenção')).toBeInTheDocument();
    expect(screen.queryByText(/mifflin|harris|fórmula|formula|idade|sexo|ajuste/i)).toBeNull();
  });

  it.each([
    ['deficit', 'Défice'],
    ['surplus', 'Superávit'],
  ])('writes the goal %s as "%s"', async (goal, word) => {
    useNutrition(nutritionPlan({ goal_type: goal }));

    await openNutrition();

    expect(screen.getByText(word)).toBeInTheDocument();
  });

  it('shows no goal word when the plan has none', async () => {
    useNutrition(nutritionPlan({ goal_type: null }));

    await openNutrition();

    expect(screen.queryByText(/^(Manutenção|Défice|Superávit)$/)).not.toBeInTheDocument();
  });

  it('shows the plan and its targets from a single request', async () => {
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

    expect(goals().getByText('2400')).toBeInTheDocument();
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
    // Chips de macros: letra visível, nome por extenso para leitores de ecrã, gramas em texto.
    const chips = within(breakfast).getAllByRole('listitem').slice(0, 3);
    expect(chips[0]).toHaveTextContent('PProteína19,4 g');
    expect(chips[1]).toHaveTextContent('HHidratos57,4 g');
    expect(chips[2]).toHaveTextContent('GGordura12,2 g');
    expect(chips[0]).toHaveClass('text-macro-protein');
    expect(chips[1]).toHaveClass('text-macro-carbs');
    expect(chips[2]).toHaveClass('text-macro-fats');
    const oats = within(breakfast).getByText('Aveia').closest('li')!;
    expect(oats).toHaveTextContent('80 g · 298 kcal');
    const yogurt = within(breakfast).getByText('Iogurte natural').closest('li')!;
    expect(yogurt).toHaveTextContent('200 g · 136 kcal');
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

  it('flags a supplement that is no longer available', async () => {
    const plan = nutritionPlan();
    const [breakfast, lunch] = plan.meals;
    useNutrition({
      ...plan,
      meals: [
        {
          ...breakfast!,
          supplements: [{ ...breakfast!.supplements[0]!, is_unavailable: true, notes: null }],
        },
        lunch!,
      ],
    });

    await openNutrition();

    const card = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(card).getByText('Indisponível')).toBeInTheDocument();
    // A etiqueta sozinha não diz o que fazer: a frase diz.
    expect(
      within(card).getByText(
        'Este suplemento já não está disponível. Confirma com o teu personal trainer o que fazer.'
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
    expect(screen.queryByText('Fibra')).not.toBeInTheDocument();
    expect(goals().getByText('Proteína')).toBeInTheDocument();
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
            {
              ...meal.supplements[0]!,
              id: 'c0a80101-0000-4000-8000-0000000000a2',
              supplement_name: 'Magnésio',
              notes: null,
            },
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
    expect(goals().getByText('2400')).toBeInTheDocument();
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

    await screen.findByRole('heading', { level: 1, name: /Rita/ });
    expect(router.state.location.pathname).toBe('/trainer');
    expect(requests).toBe(0);
  });

  it('links the supplements of a meal to the plan section of the supplements page', async () => {
    await openNutrition();

    const breakfast = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(breakfast).getByText('Creatina · 5 g')).toBeInTheDocument();
    expect(within(breakfast).getByRole('link', { name: 'Registar tomas' })).toHaveAttribute(
      'href',
      '/portal/supplements#plano-alimentar'
    );
    // Uma refeição sem suplementos não tem link.
    const lunch = screen.getByRole('article', { name: 'Almoço' });
    expect(within(lunch).queryByRole('link', { name: 'Registar tomas' })).not.toBeInTheDocument();
  });

  it('lists the supplements outside the meals of the chosen plan', async () => {
    useNutrition({
      ...nutritionPlan(),
      plan_supplements: [
        {
          id: '5d8f2a64-8a7b-4c0e-9f3a-1b2c3d4e5f60',
          order_number: 1,
          supplement_name: 'Vitamina D',
          is_unavailable: false,
          unit_of_measure: 'cápsula',
          quantity: 1,
          notes: 'Com a refeição mais gorda.',
        },
      ],
    });

    await openNutrition();

    const section = screen.getByRole('region', { name: 'Fora das refeições' });
    expect(within(section).getByText('Vitamina D · 1 cápsula')).toBeInTheDocument();
    expect(within(section).getByText('Com a refeição mais gorda.')).toBeInTheDocument();
    expect(within(section).getByRole('link', { name: 'Registar tomas' })).toBeInTheDocument();
  });

  it('hides the outside-meals section when the plan has no such supplements', async () => {
    await openNutrition();

    expect(screen.queryByRole('region', { name: 'Fora das refeições' })).not.toBeInTheDocument();
  });

  it('does not list a blocked food: the backend no longer sends it', async () => {
    const plan = nutritionPlan();
    const [breakfast, lunch] = plan.meals;
    useNutrition({
      ...plan,
      meals: [{ ...breakfast!, items: [breakfast!.items[1]!] }, lunch!],
    });

    await openNutrition();

    const card = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(card).queryByText('Aveia')).not.toBeInTheDocument();
    expect(within(card).getByText('Iogurte natural')).toBeInTheDocument();
    expect(screen.queryByText('Alimento indisponível')).not.toBeInTheDocument();
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
