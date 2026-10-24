import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { toast } from 'sonner';
import type * as Sonner from 'sonner';
import { describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import type { components } from '@/shared/api/schema';
import {
  CLIENT_ID,
  clientDetails,
  clientSummaryOverview,
  initialAssessment,
} from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: { success: vi.fn(), error: vi.fn() },
}));

type Schemas = components['schemas'];

const DETAIL = `/trainer/clients/${CLIENT_ID}`;
const ROUTE = `${DETAIL}/initial-assessment`;
const ASSESSMENT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

/** Avaliação guardada com valores reais do domínio. */
function savedAssessment(overrides: Partial<Schemas['InitialAssessmentResponse']> = {}) {
  return initialAssessment({ id: ASSESSMENT_ID, ...overrides });
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

  it('refuses a weight above 1000 kg and more than two decimals before sending', async () => {
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

    const weight = await screen.findByLabelText('Peso (kg)');
    await user.type(weight, '1000.01');
    await user.type(screen.getByLabelText('Altura (cm)'), '168');
    await user.type(screen.getByLabelText('Objetivos'), 'Força');
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));
    expect(await screen.findByText('O peso não pode exceder 1000 kg.')).toBeInTheDocument();

    await user.clear(weight);
    await user.type(weight, '64.855');
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));
    expect(await screen.findByText('Usa no máximo duas casas decimais.')).toBeInTheDocument();
    expect(posts).toBe(0);
  }, 25000);

  it('marks a client-submitted assessment as reviewed and then shows the review date', async () => {
    let reviewed = false;
    handlers(null);
    server.use(
      http.get(`${API}/clients/:clientId/initial-assessment`, () =>
        HttpResponse.json(
          savedAssessment({
            submitted_by_client: true,
            reviewed_at: reviewed ? '2026-10-20T09:00:00Z' : null,
          })
        )
      ),
      http.post(`${API}/initial-assessments/:assessmentId/review`, ({ params }) => {
        expect(params.assessmentId).toBe(ASSESSMENT_ID);
        reviewed = true;
        return HttpResponse.json(
          savedAssessment({ submitted_by_client: true, reviewed_at: '2026-10-20T09:00:00Z' })
        );
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Preenchida pelo cliente')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Marcar como revista' }));

    expect(await screen.findByText('Revista a 20/10/2026')).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('Avaliação inicial marcada como revista.');
    expect(screen.queryByRole('button', { name: 'Marcar como revista' })).not.toBeInTheDocument();
  }, 25000);

  it('offers no review for an assessment made by the trainer', async () => {
    handlers(savedAssessment());
    renderApp({ initialEntries: [ROUTE] });

    expect(
      await screen.findByRole('heading', { name: 'Corrigir avaliação inicial' })
    ).toBeInTheDocument();
    expect(screen.queryByText('Preenchida pelo cliente')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Marcar como revista' })).not.toBeInTheDocument();
  }, 25000);
});
