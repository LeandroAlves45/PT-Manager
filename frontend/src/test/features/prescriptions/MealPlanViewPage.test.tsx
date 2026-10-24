import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import {
  PLAN_ID,
  blockedEnforcement,
  blockedFoodPlan,
  catalogHandlers,
  savedPlan,
} from '@/test/features/prescriptions/mealPlanFixtures';
import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const VIEW = `/trainer/meal-plans/${PLAN_ID}`;

/** Abre a página de um plano com a resposta dada. */
function openPlan(plan: ReturnType<typeof savedPlan>) {
  server.use(
    ...restorableSession(),
    ...catalogHandlers(),
    http.get(API + '/meal-plans/:id', () => HttpResponse.json(plan))
  );
  renderApp({ initialEntries: [VIEW] });
}

describe('Meal plan page, supplements outside the meals', () => {
  it('lists the plan supplements with quantity, unit and notes, outside any meal', async () => {
    const base = savedPlan();
    openPlan(
      savedPlan({
        plan_supplements: [{ ...base.plan_supplements[0]!, notes: 'em jejum', quantity: 3 }],
      })
    );

    const section = await screen.findByRole('region', { name: 'Suplementos do plano' });
    expect(
      within(section).getByRole('heading', { name: 'Suplementos do plano (fora das refeições)' })
    ).toBeInTheDocument();
    expect(within(section).getByText(/Creatina/)).toHaveTextContent('Creatina 3 g · em jejum');
    expect(within(section).queryByText('Bloqueado')).not.toBeInTheDocument();
    // Está fora da refeição: o artigo da refeição só tem o suplemento dela.
    const meal = screen.getByRole('article', { name: 'Pequeno-almoço' });
    expect(within(meal).queryByText(/em jejum/)).not.toBeInTheDocument();
  });

  it('hides the section when the plan has no supplements outside the meals', async () => {
    openPlan(savedPlan({ plan_supplements: [] }));

    await screen.findByRole('heading', { name: 'Plano alimentar A' });
    expect(screen.queryByRole('region', { name: 'Suplementos do plano' })).not.toBeInTheDocument();
    expect(screen.queryByText('Suplementos do plano (fora das refeições)')).not.toBeInTheDocument();
  });
});

describe('Meal plan page, blocked items', () => {
  it('marks a blocked food with reason and note and does not show its kcal', async () => {
    openPlan(blockedFoodPlan());

    const meal = await screen.findByRole('article', { name: 'Pequeno-almoço' });
    expect(within(meal).getByText('Arroz cozido')).toBeInTheDocument();
    expect(within(meal).getByText('Bloqueado')).toBeInTheDocument();
    expect(
      within(meal).getByText('Motivo: Informação perigosa · Nota: Quantidades fora do seguro')
    ).toBeInTheDocument();
    // O servidor já não conta o arroz nos totais, e a linha não mostra os 195 kcal dele.
    expect(within(meal).queryByText('195 kcal')).not.toBeInTheDocument();
    expect(
      within(screen.getByTestId('nutrition-balance')).getByText('faltam 2000 kcal')
    ).toBeInTheDocument();
  });

  it('marks blocked supplements in the meal and in the plan, and nothing else', async () => {
    const base = savedPlan();
    const [meal] = base.meals;
    openPlan(
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
          platform_enforcement: { reason: 'malicious_content', note: null },
        })),
      })
    );

    const article = await screen.findByRole('article', { name: 'Pequeno-almoço' });
    expect(within(article).getByText('Bloqueado')).toBeInTheDocument();
    expect(within(article).getByText(/Nota: Quantidades fora do seguro/)).toBeInTheDocument();
    // O alimento da refeição não está bloqueado: mantém os kcal.
    expect(within(article).getAllByText('195 kcal').length).toBeGreaterThan(0);

    const section = screen.getByRole('region', { name: 'Suplementos do plano' });
    expect(within(section).getByText('Bloqueado')).toBeInTheDocument();
    expect(within(section).getByText('Motivo: Conteúdo malicioso')).toBeInTheDocument();
    expect(within(section).queryByText(/Nota:/)).not.toBeInTheDocument();
  });
});
