import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('Meal plan preview', () => {
  it('requires a fresh preview and sends null for fields forbidden in manual energy mode', async () => {
    const previews: unknown[] = [];
    const saves: unknown[] = [];
    server.use(
      ...restorableSession(),
      http.get(API + '/meal-plans', () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      ),
      http.get(API + '/clients', () =>
        HttpResponse.json({
          items: [
            {
              id: '11111111-1111-1111-1111-111111111111',
              name: 'Marta Figueiredo',
              phone: '912345678',
            },
          ],
          total_count: 1,
          page_number: 1,
          page_size: 5,
        })
      ),
      http.get(API + '/clients/:id', () =>
        HttpResponse.json({
          id: '11111111-1111-1111-1111-111111111111',
          name: 'Marta Figueiredo',
          birth_date: '1990-07-01',
          sex: 'female',
        })
      ),
      http.get(API + '/clients/:id/initial-assessment', () =>
        HttpResponse.json(problem('not_found'), { status: 404 })
      ),
      http.post(API + '/nutrition/preview', async ({ request }) => {
        previews.push(await request.json());
        return HttpResponse.json({
          target_kcal: 2000,
          protein_target_grams: 140,
          carbs_target_grams: 200,
          fats_target_grams: 70,
        });
      }),
      http.post(API + '/meal-plans', async ({ request }) => {
        saves.push(await request.json());
        return HttpResponse.json({ id: '22222222-2222-2222-2222-222222222222' }, { status: 201 });
      })
    );

    renderApp({ initialEntries: ['/trainer/meal-plans'] });
    await userEvent.click(await screen.findByRole('button', { name: 'Novo plano' }));
    await userEvent.click(screen.getByRole('combobox', { name: 'Cliente' }));
    await userEvent.type(screen.getByPlaceholderText('Pesquisar por nome'), 'Mar');
    await userEvent.click(await screen.findByText('Marta Figueiredo'));
    await userEvent.type(screen.getByLabelText('Nome'), 'Plano alimentar A');
    await userEvent.type(screen.getByLabelText('Data de início'), '2026-09-30');
    await userEvent.selectOptions(screen.getByLabelText('Origem da energia'), 'manual_energy');
    await userEvent.clear(screen.getByLabelText('Peso usado (kg)'));
    await userEvent.type(screen.getByLabelText('Peso usado (kg)'), '80');
    await userEvent.type(screen.getByLabelText('Energia alvo (kcal)'), '2000');
    await userEvent.selectOptions(screen.getByLabelText('Modo dos macros'), 'manual_grams');
    await userEvent.type(screen.getByLabelText('Proteína (g)'), '140');
    await userEvent.type(screen.getByLabelText('Hidratos (g)'), '200');
    await userEvent.type(screen.getByLabelText('Gordura (g)'), '70');

    expect(screen.getByRole('button', { name: 'Guardar plano alimentar' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Calcular' }));
    await waitFor(() => expect(previews).toHaveLength(1));
    expect(screen.getByRole('button', { name: 'Guardar plano alimentar' })).toBeEnabled();

    await userEvent.clear(screen.getByLabelText('Peso usado (kg)'));
    await userEvent.type(screen.getByLabelText('Peso usado (kg)'), '82');
    expect(screen.getByRole('button', { name: 'Guardar plano alimentar' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Calcular' }));
    await waitFor(() => expect(previews).toHaveLength(2));
    await userEvent.click(screen.getByRole('button', { name: 'Guardar plano alimentar' }));
    await waitFor(() => expect(saves).toHaveLength(1));

    const saved = saves[0] as {
      calculation: {
        energy_formula: null | string;
        height_cm: null | number;
        age: null | number;
        sex: null | string;
        weight_kg: number;
      };
    };
    expect(saved.calculation.energy_formula).toBeNull();
    expect(saved.calculation.height_cm).toBeNull();
    expect(saved.calculation.age).toBeNull();
    expect(saved.calculation.sex).toBeNull();
    expect(saved.calculation.weight_kg).toBe(82);
  });

  it('opens an existing plan without client requests, keeps the draft on refetch and sends calculation null', async () => {
    const PLAN_ID = '33333333-3333-3333-3333-333333333333';
    const CLIENT_ID = '11111111-1111-1111-1111-111111111111';
    const calculation = {
      schema_version: 1,
      calculation_origin: 'manual_energy',
      calculated_at: '2026-09-01T10:00:00Z',
      energy_formula: null,
      weight_kg_used: 80,
      height_cm_used: null,
      age_used: null,
      sex_used: null,
      body_fat_percentage_used: null,
      activity_level: null,
      activity_factor: null,
      goal_type: null,
      goal_adjustment_kcal: null,
      resting_energy_expenditure_kcal: null,
      total_daily_energy_expenditure_kcal: null,
      target_kcal: 2000,
      macro_distribution_mode: 'manual_grams',
      protein_percentage_input: null,
      carbs_percentage_input: null,
      fats_percentage_input: null,
      protein_grams_per_kg_input: null,
      fats_grams_per_kg_input: null,
      protein_target_grams: 140,
      carbs_target_grams: 200,
      fats_target_grams: 70,
      protein_energy_percentage: 28,
      carbs_energy_percentage: 40,
      fats_energy_percentage: 32,
      calculated_macro_kcal: 1990,
      kcal_difference: -10,
    };
    const plan = {
      id: PLAN_ID,
      client_id: CLIENT_ID,
      name: 'Plano alimentar A',
      description: null,
      starts_date: '2026-09-01',
      ends_date: null,
      calculation,
      actual_totals: { protein_grams: 0, carbs_grams: 0, fats_grams: 0, kcal: 0, fiber_grams: 0 },
      is_active: true,
      is_archived: false,
      needs_review: false,
      meals: [],
      created_at: '2026-09-01T10:00:00Z',
      updated_at: '2026-09-01T10:00:00Z',
    };
    let detailRequests = 0;
    let clientRequests = 0;
    const saves: Array<Record<string, unknown>> = [];
    server.use(
      ...restorableSession(),
      http.get(API + '/meal-plans', () =>
        HttpResponse.json({
          items: [
            {
              ...plan,
              client_name: 'Marta Figueiredo',
              kcal_target: 2000,
              protein_target_grams: 140,
              carbs_target_grams: 200,
              fats_target_grams: 70,
            },
          ],
          total_count: 1,
          page_number: 1,
          page_size: 25,
        })
      ),
      http.get(API + '/meal-plans/:id', () => {
        detailRequests++;
        // Dados diferentes no refetch: com dados iguais o structural sharing escondia o defeito.
        return HttpResponse.json(
          detailRequests === 1 ? plan : { ...plan, name: 'Nome no servidor' }
        );
      }),
      http.get(API + '/clients/:id', () => {
        clientRequests++;
        return HttpResponse.json(problem('not_found'), { status: 404 });
      }),
      http.put(API + '/meal-plans/:id', async ({ request }) => {
        saves.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json(plan);
      })
    );

    const { queryClient } = renderApp({ initialEntries: ['/trainer/meal-plans'] });
    await userEvent.click(await screen.findByRole('button', { name: 'Ver plano' }));
    expect(await screen.findByLabelText('Nome')).toHaveValue('Plano alimentar A');
    expect(screen.queryByText('A carregar dados do cliente…')).not.toBeInTheDocument();

    await userEvent.clear(screen.getByLabelText('Nome'));
    await userEvent.type(screen.getByLabelText('Nome'), 'Plano editado');
    await queryClient.invalidateQueries({ queryKey: ['meal-plans'] });
    await waitFor(() => expect(detailRequests).toBe(2));
    expect(screen.getByLabelText('Nome')).toHaveValue('Plano editado');

    await userEvent.click(screen.getByRole('button', { name: 'Guardar plano alimentar' }));
    await waitFor(() => expect(saves).toHaveLength(1));
    expect(saves[0]).toMatchObject({ name: 'Plano editado', calculation: null });
    expect(clientRequests).toBe(0);
  });

  it('ignores the error of a preview whose calculation was changed meanwhile', async () => {
    let releaseFirst: () => void = () => undefined;
    const firstHeld = new Promise<void>((resolve) => {
      releaseFirst = resolve;
    });
    let previews = 0;
    server.use(
      ...restorableSession(),
      http.get(API + '/meal-plans', () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      ),
      http.post(API + '/nutrition/preview', async () => {
        previews++;
        await firstHeld;
        return HttpResponse.json(problem('validation_failed', { code: 'validation_failed' }), {
          status: 400,
        });
      })
    );

    renderApp({ initialEntries: ['/trainer/meal-plans'] });
    await userEvent.click(await screen.findByRole('button', { name: 'Novo plano' }));
    await userEvent.selectOptions(screen.getByLabelText('Origem da energia'), 'manual_energy');
    await userEvent.type(screen.getByLabelText('Energia alvo (kcal)'), '2000');
    await userEvent.click(screen.getByRole('button', { name: 'Calcular' }));
    await waitFor(() => expect(previews).toBe(1));

    // O trainer corrige o cálculo enquanto o pedido antigo ainda decorre.
    await userEvent.type(screen.getByLabelText('Energia alvo (kcal)'), '0');
    releaseFirst();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Calcular' })).toBeEnabled());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByText('Preview por calcular.')).toBeInTheDocument();
  });
});
