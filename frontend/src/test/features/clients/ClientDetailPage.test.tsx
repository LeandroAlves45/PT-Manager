import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  CLIENT_ID,
  clientDetails,
  clientPack,
  clientPackPage,
  clientSummaryOverview,
  sessionPage,
  trainingSession,
} from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const ROUTE = `/trainer/clients/${CLIENT_ID}`;
const ASSESSMENT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

/** Handlers do detalhe; devolve o contador de pedidos à avaliação inicial. */
function detailHandlers(
  details = clientDetails(),
  summary = clientSummaryOverview()
): { assessmentRequests: () => number } {
  let assessmentRequests = 0;
  server.use(
    ...restorableSession(),
    http.get(`${API}/clients/:clientId`, () => HttpResponse.json(details)),
    http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(summary)),
    http.get(`${API}/clients/:clientId/initial-assessment`, () => {
      assessmentRequests += 1;
      return HttpResponse.json(problem('resource_not_found'), { status: 404 });
    })
  );
  return { assessmentRequests: () => assessmentRequests };
}

describe('ClientDetailPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('shows header and summary KPIs without requesting the assessment', async () => {
    const { assessmentRequests } = detailHandlers();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByRole('heading', { name: 'Marta Figueiredo' })).toBeInTheDocument();
    expect(screen.getByText('Pack 10 sessões · 3 restantes')).toBeInTheDocument();
    expect(await screen.findByText('86 %')).toBeInTheDocument();
    expect(screen.getByText('96 de 112 séries (28 dias)')).toBeInTheDocument();
    expect(screen.getByText('-1,4 kg desde 22/07/2026')).toBeInTheDocument();
    expect(screen.getByText('180 g HC · 140 g P · 70 g G')).toBeInTheDocument();
    expect(screen.getByText('fim previsto 30/09/2026')).toBeInTheDocument();
    expect(screen.getByText('válido até 30/09/2026')).toBeInTheDocument();
    expect(screen.getByText(/64,8 kg · 168 cm · cliente desde 04\/02\/2026/)).toBeInTheDocument();
    expect(assessmentRequests()).toBe(0);
  }, 15000);

  it('shows explicit texts for missing summary data instead of zeros', async () => {
    detailHandlers(
      clientDetails({ usable_packs: [] }),
      clientSummaryOverview({
        weight: null,
        height_cm: null,
        training_plan: null,
        adherence: null,
        nutrition: null,
        packs: { usable_pack_count: 0, sessions_remaining: 0, next_expected_end_date: null },
      })
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Sem registos de peso')).toBeInTheDocument();
    expect(screen.getByText('Sem plano de treino ativo')).toBeInTheDocument();
    expect(screen.getByText('Sem plano alimentar ativo')).toBeInTheDocument();
    expect(screen.getByText('Sem packs ativos')).toBeInTheDocument();
    expect(screen.getByText('Este cliente não tem plano ativo.')).toBeInTheDocument();
  }, 15000);

  it('reports a client from another tenant as not found', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients/:clientId`, () =>
        HttpResponse.json(problem('client_not_found'), { status: 404 })
      ),
      http.get(`${API}/clients/:clientId/summary`, () =>
        HttpResponse.json(problem('client_not_found'), { status: 404 })
      )
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Cliente não encontrado.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Marta Figueiredo' })).not.toBeInTheDocument();
  }, 15000);

  it('invites with the contact email of the record', async () => {
    let body: unknown = null;
    detailHandlers();
    server.use(
      http.post(`${API}/auth/invite-client`, async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: /Convidar para o portal/ }));
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Enviar convite',
      })
    );

    await waitFor(() => expect(body).toEqual({ client_id: CLIENT_ID, email: 'marta@example.com' }));
    expect(toastMock.success).toHaveBeenCalledWith('Convite enviado para marta@example.com.');
  }, 15000);

  it.each([
    ['a portal account', { user_id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' }],
    ['no contact email', { contact_email: null }],
    ['an archived record', { is_active: false }],
  ])(
    'hides the invite for %s',
    async (_, overrides) => {
      detailHandlers(clientDetails(overrides));
      renderApp({ initialEntries: [ROUTE] });

      expect(await screen.findByRole('heading', { name: 'Marta Figueiredo' })).toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: /Convidar para o portal/ })
      ).not.toBeInTheDocument();
    },
    15000
  );

  it('translates an invite refused by the email delivery', async () => {
    detailHandlers();
    server.use(
      http.post(`${API}/auth/invite-client`, () =>
        HttpResponse.json(problem('authentication_email_delivery_unavailable'), { status: 503 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: /Convidar para o portal/ }));
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Enviar convite',
      })
    );

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith(
        'O email de convite não pôde ser enviado agora. Tenta mais tarde.'
      )
    );
  }, 15000);

  it('creates the initial assessment only after opening it', async () => {
    let body: Record<string, unknown> | null = null;
    detailHandlers();
    server.use(
      http.post(`${API}/initial-assessments`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({}, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: /Avaliação inicial/ }));
    await user.click(await screen.findByRole('button', { name: 'Guardar avaliação' }));
    expect(await screen.findByText('Indica o peso.')).toBeInTheDocument();
    expect(screen.getByText('Indica a altura.')).toBeInTheDocument();
    expect(screen.getByText('Indica os objetivos.')).toBeInTheDocument();
    expect(body).toBeNull();

    await user.type(screen.getByLabelText('Peso (kg)'), '64.8');
    await user.type(screen.getByLabelText('Altura (cm)'), '168');
    await user.type(screen.getByLabelText('Objetivos'), 'Recomposição corporal');
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));

    await waitFor(() => expect(body).not.toBeNull());
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
  }, 20000);

  it('applies the domain limit of body fat below 100 %', async () => {
    let posts = 0;
    detailHandlers();
    server.use(
      http.post(`${API}/initial-assessments`, () => {
        posts += 1;
        return HttpResponse.json({}, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: /Avaliação inicial/ }));
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
  }, 20000);

  it('updates an existing assessment and maps server field errors', async () => {
    let method: string | null = null;
    detailHandlers();
    server.use(
      http.get(`${API}/clients/:clientId/initial-assessment`, () =>
        HttpResponse.json({
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
        })
      ),
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
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: /Avaliação inicial/ }));
    // Um nível guardado fora da lista sugerida continua selecionável (não é apagado).
    expect(await screen.findByLabelText('Condição física')).toHaveValue('recreational');
    await user.click(screen.getByRole('button', { name: 'Guardar avaliação' }));

    expect(await screen.findByText('Peso inválido.')).toBeInTheDocument();
    expect(method).toBe('PUT');
  }, 20000);

  // [6E2] NOVO: separador "Sessões" (packs e sessões do cliente), sincronizado com `?tab=`.
  describe('Sessões tab', () => {
    /** Sessões e packs do cliente; devolve os URLs pedidos a cada lista. */
    function sessionsTabHandlers(): { sessionUrls: URL[]; packUrls: URL[] } {
      const sessionUrls: URL[] = [];
      const packUrls: URL[] = [];
      server.use(
        http.get(`${API}/sessions`, ({ request }) => {
          sessionUrls.push(new URL(request.url));
          return HttpResponse.json(sessionPage([trainingSession()]));
        }),
        http.get(`${API}/client-session-packs`, ({ request }) => {
          packUrls.push(new URL(request.url));
          return HttpResponse.json(clientPackPage([clientPack()]));
        })
      );
      return { sessionUrls, packUrls };
    }

    it('does not load sessions or packs while the summary tab is open', async () => {
      detailHandlers();
      const { sessionUrls, packUrls } = sessionsTabHandlers();
      renderApp({ initialEntries: [ROUTE] });

      expect(await screen.findByText('86 %')).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: 'Resumo' })).toHaveAttribute('aria-selected', 'true');
      expect(sessionUrls).toEqual([]);
      expect(packUrls).toEqual([]);
    }, 15000);

    it("opens from the URL and lists only this client's sessions and packs", async () => {
      detailHandlers();
      const { sessionUrls, packUrls } = sessionsTabHandlers();
      renderApp({ initialEntries: [`${ROUTE}?tab=sessions`] });

      expect(await screen.findByText('3 de 10 restantes')).toBeInTheDocument();
      expect(await screen.findByText('PT individual · Estúdio A · 60 min')).toBeInTheDocument();
      expect(sessionUrls[0]?.searchParams.get('client_id')).toBe(CLIENT_ID);
      expect(packUrls[0]?.searchParams.get('client_id')).toBe(CLIENT_ID);
      // A coluna Cliente seria redundante no próprio cliente.
      expect(screen.queryByRole('columnheader', { name: 'Cliente' })).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Marcar sessão/ })).toBeInTheDocument();
    }, 15000);

    it('writes the selected tab to the URL', async () => {
      detailHandlers();
      sessionsTabHandlers();
      const user = userEvent.setup();
      renderApp({ initialEntries: [ROUTE] });

      await user.click(await screen.findByRole('tab', { name: 'Sessões' }));

      await waitFor(() => expect(window.location.search).toBe('?tab=sessions'));
      expect(await screen.findByText('3 de 10 restantes')).toBeInTheDocument();
    }, 15000);

    it('hides "Marcar sessão" for an archived client', async () => {
      detailHandlers(clientDetails({ is_active: false }));
      sessionsTabHandlers();
      renderApp({ initialEntries: [`${ROUTE}?tab=sessions`] });

      expect(await screen.findByText('3 de 10 restantes')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Marcar sessão/ })).not.toBeInTheDocument();
    }, 15000);
  });
});
