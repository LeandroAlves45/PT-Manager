import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { CLIENT_ID, clientDetails, clientSummaryOverview } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: { success: vi.fn(), error: vi.fn() },
}));

const DETAIL = `/trainer/clients/${CLIENT_ID}`;
const ROUTE = `${DETAIL}/initial-assessment`;
const ASSESSMENT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

/** Avaliação guardada com valores reais do domínio. */
function savedAssessment() {
  return {
    id: ASSESSMENT_ID,
    client_id: CLIENT_ID,
    weight_kg: 66,
    height_cm: 168,
    body_fat_percentage: null,
    medical_conditions: null,
    fitness_level: 'recreational',
    activity_level: 'moderately_active',
    goals: 'Força',
    profession: null,
    body_measurements: {
      waist_cm: null,
      hip_cm: null,
      chest_cm: null,
      right_arm_cm: null,
      left_arm_cm: null,
      right_thigh_cm: null,
      left_thigh_cm: null,
      right_calf_cm: null,
      left_calf_cm: null,
    },
    nutrition_intake: {
      food_preferences: null,
      disliked_foods: null,
      food_intolerances: null,
      food_allergies: null,
      dietary_restrictions: null,
      daily_routine: null,
      sleep_quality: null,
      mood: null,
      stress_level: null,
      avg_water_liters_per_day: null,
      hungriest_time_of_day: null,
      uses_supplements: null,
      current_supplements: null,
      other_notes: null,
    },
    created_at: '2026-09-01T10:00:00Z',
    updated_at: '2026-09-01T10:00:00Z',
  };
}

/** Ficha e convite (a ficha é o destino ao guardar); a avaliação é opcional por teste. */
function handlers(assessment: ReturnType<typeof savedAssessment> | null) {
  server.use(
    ...restorableSession(),
    http.get(`${API}/clients/:clientId`, () => HttpResponse.json(clientDetails())),
    http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(clientSummaryOverview())),
    http.get(`${API}/clients/:clientId/invitation`, () =>
      HttpResponse.json({ state: 'none', email: null, expires_at: null })
    ),
    http.get(`${API}/clients/:clientId/initial-assessment`, () =>
      assessment === null
        ? HttpResponse.json(problem('resource_not_found'), { status: 404 })
        : HttpResponse.json(assessment)
    )
  );
}

describe('InitialAssessmentPage', () => {
  it('creates the assessment on its own page and goes back to the record', async () => {
    let body: Record<string, unknown> | null = null;
    handlers(null);
    server.use(
      http.post(`${API}/initial-assessments`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({}, { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [ROUTE] });

    expect(
      await screen.findByRole('heading', { name: 'Criar avaliação inicial' })
    ).toBeInTheDocument();
    expect(screen.getByText('Marta Figueiredo')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));
    expect(await screen.findByText('Indica o peso.')).toBeInTheDocument();
    expect(screen.getByText('Indica a altura.')).toBeInTheDocument();
    expect(screen.getByText('Indica os objetivos.')).toBeInTheDocument();
    expect(body).toBeNull();

    await user.type(screen.getByLabelText('Peso (kg)'), '64.8');
    await user.type(screen.getByLabelText('Altura (cm)'), '168');
    await user.type(screen.getByLabelText('Objetivos'), 'Recomposição corporal');
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(DETAIL));
    expect(body).toMatchObject({
      client_id: CLIENT_ID,
      weight_kg: 64.8,
      height_cm: 168,
      body_fat_percentage: null,
      fitness_level: 'beginner',
      activity_level: 'sedentary',
      goals: 'Recomposição corporal',
      profession: null,
    });
  }, 25000);

  it('corrects the saved assessment with a PUT and maps server field errors', async () => {
    let method: string | null = null;
    handlers(savedAssessment());
    server.use(
      http.put(`${API}/initial-assessments/:assessmentId`, ({ request }) => {
        method = request.method;
        return HttpResponse.json(
          problem('validation_failed', {
            errors: [{ field: 'WeightKg', code: 'weight_invalid', message: 'Peso inválido.' }],
          }),
          { status: 400 }
        );
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [ROUTE] });

    expect(
      await screen.findByRole('heading', { name: 'Corrigir avaliação inicial' })
    ).toBeInTheDocument();
    // Um nível guardado fora da lista sugerida continua selecionável (não é apagado).
    expect(screen.getByLabelText('Condição física')).toHaveValue('recreational');
    expect(screen.getByLabelText('Peso (kg)')).toHaveValue(66);
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));

    expect(await screen.findByText('Peso inválido.')).toBeInTheDocument();
    expect(method).toBe('PUT');
    expect(router.state.location.pathname).toBe(ROUTE);
  }, 25000);

  it('goes back to the record after a successful correction', async () => {
    handlers(savedAssessment());
    server.use(
      http.put(`${API}/initial-assessments/:assessmentId`, () =>
        HttpResponse.json(savedAssessment())
      )
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [ROUTE] });

    const weight = await screen.findByLabelText('Peso (kg)');
    await user.clear(weight);
    await user.type(weight, '65');
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(DETAIL));
  }, 25000);

  it('asks before leaving with unsaved changes', async () => {
    handlers(null);
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [ROUTE] });

    await user.type(await screen.findByLabelText('Peso (kg)'), '70');
    await user.click(screen.getByRole('link', { name: /Voltar à ficha/ }));

    const dialog = await screen.findByRole('alertdialog');
    expect(within(dialog).getByText('Sair sem guardar?')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Continuar a editar' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe(ROUTE);
    expect(screen.getByLabelText('Peso (kg)')).toHaveValue(70);
  }, 25000);

  it('applies the domain limit of body fat below 100 %', async () => {
    let posts = 0;
    handlers(null);
    server.use(
      http.post(`${API}/initial-assessments`, () => {
        posts += 1;
        return HttpResponse.json({}, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.type(await screen.findByLabelText('Peso (kg)'), '64.8');
    await user.type(screen.getByLabelText('Altura (cm)'), '168');
    await user.type(screen.getByLabelText('Massa gorda (%)'), '100');
    await user.type(screen.getByLabelText('Objetivos'), 'Força');
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));

    expect(await screen.findByText('Indica uma percentagem entre 0 e 100.')).toBeInTheDocument();
    expect(screen.getByLabelText('Massa gorda (%)')).toHaveAccessibleDescription(
      'Indica uma percentagem entre 0 e 100.'
    );
    expect(posts).toBe(0);
  }, 25000);
});
