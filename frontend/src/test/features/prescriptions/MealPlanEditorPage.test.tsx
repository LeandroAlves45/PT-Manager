import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import {
  CLIENT_ID,
  FOOD_ID,
  OTHER_FOOD_ID,
  PLAN_ID,
  PLAN_SUPPLEMENT_ID,
  SUPPLEMENT_ID,
  blockedEnforcement,
  blockedFoodPlan,
  catalogHandlers,
  clientHandlers,
  problem,
  replacementFoodHandlers,
  savedCalculation,
  savedPlan,
} from '@/test/features/prescriptions/mealPlanFixtures';
import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const NEW = `/trainer/meal-plans/new?client_id=${CLIENT_ID}`;

/** Preview 2000 kcal / 140 / 200 / 70, e conta os pedidos. */
function previewHandler(bodies: unknown[] = []) {
  return http.post(API + '/nutrition/preview', async ({ request }) => {
    bodies.push(await request.json());
    return HttpResponse.json(savedCalculation);
  });
}

/** Preenche o passo 1 de um plano novo com kcal à mão e macros em gramas, e calcula. */
async function fillTargets() {
  await userEvent.type(await screen.findByLabelText('Nome'), 'Fase de corte');
  await userEvent.type(screen.getByLabelText('Data de início'), '2026-09-30');
  await userEvent.selectOptions(screen.getByLabelText('Origem da energia'), 'manual_energy');
  await userEvent.type(screen.getByLabelText('Peso usado (kg)'), '80');
  await userEvent.type(screen.getByLabelText('Energia alvo (kcal)'), '2000');
  await userEvent.selectOptions(screen.getByLabelText('Modo dos macros'), 'manual_grams');
  await userEvent.type(screen.getByLabelText('Proteína (g)'), '140');
  await userEvent.type(screen.getByLabelText('Hidratos (g)'), '200');
  await userEvent.type(screen.getByLabelText('Gordura (g)'), '70');
  await userEvent.click(screen.getByRole('button', { name: 'Calcular' }));
}

/** Escolhe um item do catálogo no combobox dentro do grupo indicado. */
async function pick(group: HTMLElement, label: string, term: string, option: string) {
  await userEvent.click(within(group).getByRole('combobox', { name: label }));
  await userEvent.type(screen.getByPlaceholderText('Pesquisar no catálogo'), term);
  const list = await screen.findByRole('listbox');
  await userEvent.click(await within(list).findByText(option));
}

describe('Meal plan editor, step 1', () => {
  it('starts with an empty calculation, and copies the initial assessment only on click', async () => {
    server.use(...restorableSession(), ...catalogHandlers(), ...clientHandlers());
    renderApp({ initialEntries: [NEW] });

    expect(await screen.findByLabelText('Origem da energia')).toHaveValue('');
    expect(screen.getByLabelText('Peso usado (kg)')).toHaveValue(null);
    expect(screen.getByText('Passo 1 de 2 · Metas')).toBeInTheDocument();

    await userEvent.click(await screen.findByRole('button', { name: 'Usar avaliação inicial' }));
    expect(screen.getByLabelText('Peso usado (kg)')).toHaveValue(62.5);

    await userEvent.selectOptions(screen.getByLabelText('Origem da energia'), 'formula');
    expect(screen.getByLabelText('Altura (cm)')).toHaveValue(168);
    expect(screen.getByLabelText('Sexo biológico')).toHaveValue('female');
    expect(screen.getByLabelText('Atividade')).toHaveValue('lightly_active');
    expect(screen.getByLabelText('Gordura corporal (%)')).toHaveValue(24);
    expect(screen.getByLabelText('Idade')).not.toHaveValue(null);
  });

  it('offers only manual kcal to a client under 18', async () => {
    const today = new Date();
    const minor = `${today.getFullYear() - 15}-01-01`;
    server.use(...restorableSession(), ...catalogHandlers(), ...clientHandlers(minor));
    renderApp({ initialEntries: [NEW] });

    expect(await screen.findByText(/As fórmulas são para 18 anos ou mais/)).toBeInTheDocument();
    const origin = screen.getByLabelText('Origem da energia');
    expect(within(origin).queryByRole('option', { name: 'Fórmula' })).not.toBeInTheDocument();
    expect(within(origin).getByRole('option', { name: 'Kcal à mão' })).toBeInTheDocument();
  });

  it('requires a fresh preview before Next, and keeps the data when coming back', async () => {
    server.use(...restorableSession(), ...catalogHandlers(), ...clientHandlers(), previewHandler());
    renderApp({ initialEntries: [NEW] });

    expect(await screen.findByRole('button', { name: 'Seguinte' })).toBeDisabled();
    await fillTargets();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Seguinte' })).toBeEnabled());

    // Mudar o cálculo anula o preview.
    await userEvent.type(screen.getByLabelText('Energia alvo (kcal)'), '5');
    expect(screen.getByRole('button', { name: 'Seguinte' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Calcular' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Seguinte' })).toBeEnabled());

    await userEvent.click(screen.getByRole('button', { name: 'Seguinte' }));
    expect(await screen.findByText('Passo 2 de 2 · Refeições')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Voltar às metas' }));
    expect(await screen.findByLabelText('Nome')).toHaveValue('Fase de corte');
    expect(screen.getByLabelText('Energia alvo (kcal)')).toHaveValue(20005);
  });

  it('sends a new meals step without valid targets back to step 1', async () => {
    server.use(...restorableSession(), ...catalogHandlers(), ...clientHandlers());
    renderApp({ initialEntries: [`/trainer/meal-plans/new/meals?client_id=${CLIENT_ID}`] });

    expect(await screen.findByText('Passo 1 de 2 · Metas')).toBeInTheDocument();
  });
});

describe('Meal plan editor, step 2', () => {
  it('lowers what is missing as foods are added, goes negative without blocking, and creates with one POST', async () => {
    const saves: Array<Record<string, unknown>> = [];
    server.use(
      ...restorableSession(),
      ...catalogHandlers(),
      ...clientHandlers(),
      previewHandler(),
      http.post(API + '/meal-plans', async ({ request }) => {
        saves.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json({ id: PLAN_ID }, { status: 201 });
      })
    );
    const { router } = renderApp({ initialEntries: [NEW] });
    await fillTargets();
    await userEvent.click(await screen.findByRole('button', { name: 'Seguinte' }));

    const balance = await screen.findByTestId('nutrition-balance');
    expect(within(balance).getByText('faltam 2000 kcal')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Adicionar refeição' }));
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar alimento' }));
    const food = screen.getByRole('group', { name: 'Alimento 1 da refeição 1' });
    await pick(food, 'Alimento', 'Fra', 'Frango assado');
    await userEvent.type(within(food).getByLabelText('Quantidade'), '1000');

    // 1000 g × 220 kcal / 100 g = 2200 kcal: excede 200 kcal, sem bloquear nada.
    expect(await within(balance).findByText('excede 200 kcal')).toBeInTheDocument();
    expect(within(balance).getByText('−200')).toBeInTheDocument();
    expect(within(food).getByTestId('quantity-suffix')).toHaveTextContent('g');
    expect(screen.getByRole('button', { name: 'Criar' })).toBeEnabled();

    await userEvent.click(screen.getByRole('button', { name: 'Criar' }));
    await waitFor(() => expect(saves).toHaveLength(1));
    expect(saves[0]).toMatchObject({
      client_id: CLIENT_ID,
      name: 'Fase de corte',
      calculation: { calculation_origin: 'manual_energy', manual_target_kcal: 2000 },
      structure: {
        meals: [{ id: null, items: [{ food_id: FOOD_ID, quantity_in_grams: 1000 }] }],
      },
    });
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/trainer/clients/${CLIENT_ID}`)
    );
    expect(router.state.location.search).toBe('?tab=nutrition');
  });

  it('takes a numeric supplement quantity with the unit as a suffix', async () => {
    server.use(...restorableSession(), ...catalogHandlers(), ...clientHandlers(), previewHandler());
    renderApp({ initialEntries: [NEW] });
    await fillTargets();
    await userEvent.click(await screen.findByRole('button', { name: 'Seguinte' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Adicionar refeição' }));
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar suplemento' }));
    const row = screen.getByRole('group', { name: 'Suplemento 1 da refeição 1' });
    await pick(row, 'Suplemento', 'Cre', 'Creatina');

    const quantity = within(row).getByLabelText('Quantidade');
    expect(quantity).toHaveAttribute('type', 'number');
    await userEvent.type(quantity, '5gr');
    expect(quantity).toHaveValue(5);
    await waitFor(() => expect(within(row).getByTestId('quantity-suffix')).toHaveTextContent('g'));
  });

  it('asks before leaving with a draft, but not between steps', async () => {
    server.use(...restorableSession(), ...catalogHandlers(), ...clientHandlers(), previewHandler());
    renderApp({ initialEntries: [NEW] });
    await fillTargets();
    await userEvent.click(await screen.findByRole('button', { name: 'Seguinte' }));
    await screen.findByText('Passo 2 de 2 · Refeições');
    await userEvent.click(screen.getByRole('button', { name: 'Voltar às metas' }));
    await screen.findByText('Passo 1 de 2 · Metas');
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('link', { name: 'Voltar à ficha do cliente' }));
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument();
    expect(screen.getByText('Sair sem guardar?')).toBeInTheDocument();
  });
});

describe('Meal plan editor, existing plan', () => {
  it('edits with a PUT that keeps the calculation as null when it did not change', async () => {
    const saves: Array<Record<string, unknown>> = [];
    server.use(
      ...restorableSession(),
      ...catalogHandlers(),
      ...clientHandlers(),
      http.get(API + '/meal-plans/:id', () => HttpResponse.json(savedPlan())),
      http.put(API + '/meal-plans/:id', async ({ request }) => {
        saves.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json(savedPlan());
      })
    );
    renderApp({ initialEntries: [`/trainer/meal-plans/${PLAN_ID}/edit`] });

    const name = await screen.findByLabelText('Nome');
    expect(name).toHaveValue('Plano alimentar A');
    await userEvent.clear(name);
    await userEvent.type(name, 'Plano editado');
    await userEvent.click(screen.getByRole('button', { name: 'Seguinte' }));

    // O detalhe já traz os valores por 100 g: o que falta calcula-se sem pedir o alimento.
    const balance = await screen.findByTestId('nutrition-balance');
    expect(within(balance).getByText('faltam 1805 kcal')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(saves).toHaveLength(1));
    expect(saves[0]).toMatchObject({
      name: 'Plano editado',
      calculation: null,
      structure: { meals: [{ supplements: [{ supplement_id: SUPPLEMENT_ID, quantity: 5 }] }] },
    });
    // Gravar não apaga os suplementos de fora das refeições que o plano já tinha.
    expect(saves[0]).toMatchObject({
      structure: {
        plan_supplements: [
          {
            id: PLAN_SUPPLEMENT_ID,
            supplement_id: SUPPLEMENT_ID,
            quantity: 3,
            order_number: 1,
          },
        ],
      },
    });
  });

  it('shows the plan page with totals, what is missing and the edit buttons', async () => {
    server.use(
      ...restorableSession(),
      ...catalogHandlers(),
      http.get(API + '/meal-plans/:id', () => HttpResponse.json(savedPlan()))
    );
    renderApp({ initialEntries: [`/trainer/meal-plans/${PLAN_ID}`] });

    expect(await screen.findByRole('heading', { name: 'Plano alimentar A' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Editar metas' })).toHaveAttribute(
      'href',
      `/trainer/meal-plans/${PLAN_ID}/edit`
    );
    expect(screen.getByRole('link', { name: 'Editar refeições' })).toHaveAttribute(
      'href',
      `/trainer/meal-plans/${PLAN_ID}/edit/meals`
    );
    const balance = screen.getByTestId('nutrition-balance');
    expect(within(balance).getByText('faltam 1805 kcal')).toBeInTheDocument();
    const meal = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(meal).getAllByText('195 kcal').length).toBeGreaterThan(0);
    expect(within(meal).getByText('Arroz cozido')).toBeInTheDocument();
    expect(within(meal).getByText(/Creatina/)).toBeInTheDocument();
  });

  it('shows a clear message when the plan does not exist', async () => {
    server.use(
      ...restorableSession(),
      http.get(API + '/meal-plans/:id', () =>
        HttpResponse.json(problem('meal_plan_not_found'), { status: 404 })
      )
    );
    renderApp({ initialEntries: [`/trainer/meal-plans/${PLAN_ID}`] });
    expect(await screen.findByText('Plano alimentar não encontrado')).toBeInTheDocument();
  });
});

const EDIT_MEALS = `/trainer/meal-plans/${PLAN_ID}/edit/meals`;

/** Servidor com o plano dado e um PUT que guarda os corpos recebidos. */
function useExistingPlan(
  plan: ReturnType<typeof savedPlan>,
  saves: Array<Record<string, unknown>>
) {
  server.use(
    ...restorableSession(),
    ...replacementFoodHandlers(),
    ...catalogHandlers(),
    ...clientHandlers(),
    http.get(API + '/meal-plans/:id', () => HttpResponse.json(plan)),
    http.put(API + '/meal-plans/:id', async ({ request }) => {
      saves.push(JSON.parse(await request.text()) as Record<string, unknown>);
      return HttpResponse.json(plan);
    })
  );
}

describe('Meal plan editor, supplements outside the meals', () => {
  it('adds a plan supplement to a new plan and sends it in the POST body', async () => {
    const saves: Array<Record<string, unknown>> = [];
    server.use(
      ...restorableSession(),
      ...catalogHandlers(),
      ...clientHandlers(),
      previewHandler(),
      http.post(API + '/meal-plans', async ({ request }) => {
        saves.push(JSON.parse(await request.text()) as Record<string, unknown>);
        return HttpResponse.json({ id: PLAN_ID }, { status: 201 });
      })
    );
    renderApp({ initialEntries: [NEW] });
    await fillTargets();
    await userEvent.click(await screen.findByRole('button', { name: 'Seguinte' }));

    const section = await screen.findByRole('region', { name: 'Suplementos do plano' });
    expect(
      within(section).getByRole('heading', { name: 'Suplementos do plano (fora das refeições)' })
    ).toBeInTheDocument();
    await userEvent.click(
      within(section).getByRole('button', { name: 'Adicionar suplemento ao plano' })
    );
    const row = within(section).getByRole('group', { name: 'Suplemento 1 do plano' });
    await pick(row, 'Suplemento', 'Cre', 'Creatina');
    await userEvent.type(within(row).getByLabelText('Quantidade'), '7');
    await userEvent.type(within(row).getByLabelText('Notas'), 'ao deitar');
    await waitFor(() => expect(within(row).getByTestId('quantity-suffix')).toHaveTextContent('g'));

    await userEvent.click(screen.getByRole('button', { name: 'Criar' }));
    await waitFor(() => expect(saves).toHaveLength(1));
    expect((saves[0]!.structure as Record<string, unknown>).plan_supplements).toEqual([
      { id: null, supplement_id: SUPPLEMENT_ID, quantity: 7, notes: 'ao deitar', order_number: 1 },
    ]);
  });

  it('does not save a plan supplement without an item or quantity', async () => {
    const saves: Array<Record<string, unknown>> = [];
    useExistingPlan(savedPlan(), saves);
    renderApp({ initialEntries: [EDIT_MEALS] });

    await userEvent.click(
      await screen.findByRole('button', { name: 'Adicionar suplemento ao plano' })
    );
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Escolhe o alimento ou suplemento e indica a quantidade em todas as linhas'
    );
    expect(saves).toHaveLength(0);
  });

  it('edits one plan supplement and adds another with a sequential order_number', async () => {
    const saves: Array<Record<string, unknown>> = [];
    useExistingPlan(savedPlan(), saves);
    renderApp({ initialEntries: [EDIT_MEALS] });

    const first = await screen.findByRole('group', { name: 'Suplemento 1 do plano' });
    const quantity = within(first).getByLabelText('Quantidade');
    await userEvent.clear(quantity);
    await userEvent.type(quantity, '4');
    await userEvent.type(within(first).getByLabelText('Notas'), 'com água');

    await userEvent.click(screen.getByRole('button', { name: 'Adicionar suplemento ao plano' }));
    const second = screen.getByRole('group', { name: 'Suplemento 2 do plano' });
    await pick(second, 'Suplemento', 'Cre', 'Creatina');
    await userEvent.type(within(second).getByLabelText('Quantidade'), '2');

    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(saves).toHaveLength(1));
    expect((saves[0]!.structure as Record<string, unknown>).plan_supplements).toEqual([
      {
        id: PLAN_SUPPLEMENT_ID,
        supplement_id: SUPPLEMENT_ID,
        quantity: 4,
        notes: 'com água',
        order_number: 1,
      },
      { id: null, supplement_id: SUPPLEMENT_ID, quantity: 2, notes: null, order_number: 2 },
    ]);
  });

  it('removes the plan supplement and sends an empty list', async () => {
    const saves: Array<Record<string, unknown>> = [];
    useExistingPlan(savedPlan(), saves);
    renderApp({ initialEntries: [EDIT_MEALS] });

    await userEvent.click(
      await screen.findByRole('button', { name: 'Remover suplemento 1 do plano' })
    );
    expect(screen.queryByRole('group', { name: 'Suplemento 1 do plano' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(saves).toHaveLength(1));
    const structure = saves[0]!.structure as Record<string, unknown>;
    expect(structure.plan_supplements).toEqual([]);
    // As refeições seguem intactas: só a lista do plano mudou.
    expect(structure.meals).toHaveLength(1);
  });

  it('keeps an archived plan read-only, with the plan supplements listed and no buttons', async () => {
    useExistingPlan(savedPlan({ is_archived: true }), []);
    renderApp({ initialEntries: [EDIT_MEALS] });

    const row = await screen.findByRole('group', { name: 'Suplemento 1 do plano' });
    expect(within(row).getByLabelText('Quantidade')).toBeDisabled();
    expect(
      screen.queryByRole('button', { name: 'Adicionar suplemento ao plano' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Remover suplemento 1 do plano' })
    ).not.toBeInTheDocument();
  });
});

describe('Meal plan editor, blocked items', () => {
  it('marks a blocked food with the reason and note, leaves it out of the totals, and counts the replacement', async () => {
    const saves: Array<Record<string, unknown>> = [];
    useExistingPlan(blockedFoodPlan(), saves);
    renderApp({ initialEntries: [EDIT_MEALS] });

    const food = await screen.findByRole('group', { name: 'Alimento 1 da refeição 1' });
    expect(within(food).getByText('Bloqueado')).toBeInTheDocument();
    expect(
      within(food).getByText('Motivo: Informação perigosa · Nota: Quantidades fora do seguro')
    ).toBeInTheDocument();
    // A linha continua no plano, mas o arroz (195 kcal) fica fora do total.
    const balance = screen.getByTestId('nutrition-balance');
    expect(within(balance).getByText('faltam 2000 kcal')).toBeInTheDocument();
    expect(within(food).queryByText(/Este alimento:/)).not.toBeInTheDocument();
    // Os itens sem bloqueio não levam a marca.
    expect(
      within(screen.getByRole('group', { name: 'Suplemento 1 da refeição 1' })).queryByText(
        'Bloqueado'
      )
    ).not.toBeInTheDocument();

    // Trocar o alimento tira a marca e passa a contar nos totais: 150 g × 110 kcal / 100 g.
    await pick(food, 'Alimento', 'Pe', 'Peito de peru');
    await waitFor(() => expect(within(food).queryByText('Bloqueado')).not.toBeInTheDocument());
    expect(await within(balance).findByText('faltam 1835 kcal')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(saves).toHaveLength(1));
    const meals = (saves[0]!.structure as { meals: Array<{ items: unknown[] }> }).meals;
    expect(meals[0]!.items).toEqual([
      {
        id: expect.any(String) as string,
        food_id: OTHER_FOOD_ID,
        quantity_in_grams: 150,
        order_number: 1,
      },
    ]);
  });

  it('keeps a blocked food that is not replaced in the PUT, with its original ids', async () => {
    const saves: Array<Record<string, unknown>> = [];
    useExistingPlan(blockedFoodPlan(), saves);
    renderApp({ initialEntries: [EDIT_MEALS] });

    await screen.findByRole('group', { name: 'Alimento 1 da refeição 1' });
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(saves).toHaveLength(1));
    const meals = (saves[0]!.structure as { meals: Array<{ items: unknown[] }> }).meals;
    expect(meals[0]!.items).toEqual([
      {
        id: expect.any(String) as string,
        food_id: FOOD_ID,
        quantity_in_grams: 150,
        order_number: 1,
      },
    ]);
  });

  it('marks a blocked meal supplement and a blocked plan supplement, with and without a note', async () => {
    const base = savedPlan();
    const [meal] = base.meals;
    useExistingPlan(
      savedPlan({
        meals: [
          {
            ...meal!,
            supplements: meal!.supplements.map((item) => ({
              ...item,
              platform_enforcement: blockedEnforcement,
            })),
          },
        ],
        plan_supplements: base.plan_supplements.map((item) => ({
          ...item,
          platform_enforcement: { reason: 'prohibited_content', note: null },
        })),
      }),
      []
    );
    renderApp({ initialEntries: [EDIT_MEALS] });

    const inMeal = await screen.findByRole('group', { name: 'Suplemento 1 da refeição 1' });
    expect(within(inMeal).getByText('Bloqueado')).toBeInTheDocument();
    expect(
      within(inMeal).getByText('Motivo: Informação perigosa · Nota: Quantidades fora do seguro')
    ).toBeInTheDocument();

    const inPlan = screen.getByRole('group', { name: 'Suplemento 1 do plano' });
    expect(within(inPlan).getByText('Bloqueado')).toBeInTheDocument();
    expect(within(inPlan).getByText('Motivo: Conteúdo proibido')).toBeInTheDocument();
    expect(within(inPlan).queryByText(/Nota:/)).not.toBeInTheDocument();
  });
});
