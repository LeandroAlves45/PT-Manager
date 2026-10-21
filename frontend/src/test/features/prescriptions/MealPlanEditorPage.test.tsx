import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import {
  CLIENT_ID,
  FOOD_ID,
  PLAN_ID,
  SUPPLEMENT_ID,
  catalogHandlers,
  clientHandlers,
  problem,
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
  await userEvent.click(await screen.findByText(option));
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
