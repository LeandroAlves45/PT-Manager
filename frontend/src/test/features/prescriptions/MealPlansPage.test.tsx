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
});
