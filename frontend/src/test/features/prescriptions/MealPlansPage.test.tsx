import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import {
  PLAN_ID,
  savedCalculation,
  savedPlan,
} from '@/test/features/prescriptions/mealPlanFixtures';
import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { CLIENT_ID, clientDetails, clientSummaryOverview } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

function listWithOnePlan() {
  return http.get(API + '/meal-plans', () =>
    HttpResponse.json({
      items: [
        {
          ...savedPlan(),
          client_name: 'Marta Figueiredo',
          kcal_target: 2000,
          protein_target_grams: savedCalculation.protein_target_grams,
          carbs_target_grams: savedCalculation.carbs_target_grams,
          fats_target_grams: savedCalculation.fats_target_grams,
        },
      ],
      total_count: 1,
      page_number: 1,
      page_size: 25,
    })
  );
}

describe('Meal plans list', () => {
  it('only lists: New plan and each plan are links to their pages', async () => {
    server.use(...restorableSession(), listWithOnePlan());
    renderApp({ initialEntries: ['/trainer/meal-plans'] });

    expect(await screen.findByText('Plano alimentar A')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Novo plano' })).toHaveAttribute(
      'href',
      '/trainer/meal-plans/new'
    );
    expect(screen.getByRole('link', { name: 'Ver plano' })).toHaveAttribute(
      'href',
      `/trainer/meal-plans/${PLAN_ID}`
    );
  });

  it('carries client_id in the links when opened from the client page', async () => {
    server.use(
      ...restorableSession(),
      listWithOnePlan(),
      http.get(API + '/clients/:id', () => HttpResponse.json(clientDetails())),
      http.get(API + '/clients/:id/summary', () => HttpResponse.json(clientSummaryOverview())),
      http.get(API + '/clients/:id/invitation', () =>
        HttpResponse.json({ state: 'none', email: null, expires_at: null })
      )
    );
    renderApp({ initialEntries: [`/trainer/clients/${CLIENT_ID}?tab=nutrition`] });

    expect(
      await screen.findByRole('link', { name: 'Novo plano' }, { timeout: 12000 })
    ).toHaveAttribute('href', `/trainer/meal-plans/new?client_id=${CLIENT_ID}`);
    expect(await screen.findByRole('link', { name: 'Ver plano' })).toHaveAttribute(
      'href',
      `/trainer/meal-plans/${PLAN_ID}?client_id=${CLIENT_ID}`
    );
  }, 15000);
});
