import { http, HttpResponse } from 'msw';

import type { components } from '@/shared/api/schema';
import { trainerSettings } from '@/test/msw/account-fixtures';
import { SIGN_UP_ACCEPTED, accountResponse, googleChallenge } from '@/test/msw/auth-fixtures';
import {
  dueCheckIn,
  nextCheckIn,
  nutritionPlan,
  portalBranding,
  portalHome,
  portalProfile,
  supplementAssignmentsPage,
  supplementIntakesToday,
  trainingPlan,
  workoutToday,
} from '@/test/msw/portal-fixtures';
import { dashboardResponse } from '@/test/msw/trainer-fixtures';

/**
 * Handlers base do MSW: o caminho feliz da API usada pelas fundações da 6C.
 *
 * Cada teste substitui o que precisa com `server.use(...)`; `setup.ts` repõe estes
 * handlers depois de cada teste, por isso nenhum estado mutável sobrevive entre testes.
 */

export const API = 'https://localhost:7186/api/v1';

type SessionResponse = components['schemas']['SessionResponse'];

/** Resposta de sessão válida, com overrides por teste. */
export function sessionResponse(overrides: Partial<SessionResponse> = {}): SessionResponse {
  return {
    user_id: '11111111-1111-1111-1111-111111111111',
    trainer_id: '22222222-2222-2222-2222-222222222222',
    role: 'trainer',
    access_token: 'access-1',
    access_token_expires_at: '2026-09-23T12:00:00Z',
    csrf_token: 'csrf-session-1',
    ...overrides,
  };
}

/** Corpo Problem Details tal como o backend o devolve. */
export function problem(
  title: string,
  overrides: Record<string, unknown> = {}
): Record<string, unknown> {
  return {
    type: 'about:blank',
    title,
    detail: `detail for ${title}`,
    correlation_id: 'corr-123',
    ...overrides,
  };
}

export const handlers = [
  // Sem cookie de refresh: o bootstrap CSRF devolve 401 e o arranque termina anónimo.
  http.post(`${API}/auth/csrf`, () =>
    HttpResponse.json(problem('authentication_refresh_token_required'), { status: 401 })
  ),
  http.post(`${API}/auth/refresh`, () =>
    HttpResponse.json(problem('authentication_refresh_token_required'), { status: 401 })
  ),
  http.post(`${API}/auth/login`, () => HttpResponse.json(sessionResponse())),
  http.post(`${API}/auth/logout`, () => new HttpResponse(null, { status: 204 })),

  // 6GA: ciclo de vida da conta. Os 204 e o 202 do signup não dependem de a conta existir.
  http.post(`${API}/auth/signup`, () => HttpResponse.json(SIGN_UP_ACCEPTED, { status: 202 })),
  http.post(
    `${API}/auth/email-confirmation/request`,
    () => new HttpResponse(null, { status: 204 })
  ),
  http.post(`${API}/auth/confirm-email`, () => new HttpResponse(null, { status: 204 })),
  http.post(`${API}/auth/password-reset/request`, () => new HttpResponse(null, { status: 204 })),
  http.post(`${API}/auth/password-reset/complete`, () => new HttpResponse(null, { status: 204 })),
  http.post(`${API}/auth/change-password`, () => new HttpResponse(null, { status: 204 })),
  http.post(`${API}/auth/accept-invite`, () => HttpResponse.json(sessionResponse())),
  http.post(`${API}/auth/accept-invite/register`, () =>
    HttpResponse.json(sessionResponse({ role: 'client', trainer_id: null }))
  ),
  http.get(`${API}/auth/account`, () => HttpResponse.json(accountResponse())),

  // Google: o sign-in tem dois sucessos (200 sessão, 202 por confirmar); a rota nunca é
  // chamada em testes que não ligam `VITE_GOOGLE_CLIENT_ID`.
  http.post(`${API}/auth/google/challenge`, () => HttpResponse.json(googleChallenge())),
  http.post(`${API}/auth/google/sign-in`, () => HttpResponse.json(sessionResponse())),
  http.post(`${API}/auth/google/link/challenge`, () => HttpResponse.json(googleChallenge())),
  http.post(`${API}/auth/google/link`, () => new HttpResponse(null, { status: 204 })),
  http.get(`${API}/billing/subscription`, () =>
    HttpResponse.json({
      tier: 'STARTER',
      status: 'ACTIVE',
      client_limit: 25,
      current_client_count: 3,
      trial_ends_at: null,
    })
  ),
  http.get(`${API}/clients`, () =>
    HttpResponse.json({ items: [], page_number: 1, page_size: 5, total_count: 0 })
  ),
  http.get(`${API}/dashboard`, () =>
    HttpResponse.json(dashboardResponse({ active_client_count: 0 }))
  ),
  // A ficha do cliente lê o estado do convite (U63). Por omissão, nunca enviado.
  http.get(`${API}/clients/:clientId/invitation`, () =>
    HttpResponse.json({ state: 'none', email: null, expires_at: null })
  ),
  // A shell do trainer lê a marca (U65). Por omissão, um trainer que nunca a configurou.
  http.get(`${API}/trainer-settings`, () =>
    HttpResponse.json(trainerSettings({ app_name: 'PT Manager' }))
  ),
  http.get(`${API}/portal/branding`, () => HttpResponse.json(portalBranding())),
  http.get(`${API}/portal/home`, () => HttpResponse.json(portalHome())),
  http.get(`${API}/portal/my-workout/today`, () => HttpResponse.json(workoutToday())),
  http.get(`${API}/portal/my-plan`, () => HttpResponse.json(trainingPlan())),
  http.get(`${API}/portal/my-nutrition`, () => HttpResponse.json(nutritionPlan())),
  http.get(`${API}/portal/my-supplements/intakes/today`, () =>
    HttpResponse.json(supplementIntakesToday())
  ),
  http.get(`${API}/portal/my-supplements`, () => HttpResponse.json(supplementAssignmentsPage())),
  // O menu de perfil do portal lê o perfil em todas as páginas do cliente.
  http.get(`${API}/portal/my-profile`, () => HttpResponse.json(portalProfile())),
  http.get(`${API}/portal/my-check-ins/due`, () => HttpResponse.json(dueCheckIn())),
  http.get(`${API}/portal/my-check-ins/next`, () => HttpResponse.json(nextCheckIn())),
];

/** Arranque com cookie de refresh válido: a sessão é restaurada com os dados indicados. */
export function restorableSession(overrides: Partial<SessionResponse> = {}) {
  return [
    http.post(`${API}/auth/csrf`, () => HttpResponse.json({ csrf_token: 'csrf-bootstrap' })),
    http.post(`${API}/auth/refresh`, () => HttpResponse.json(sessionResponse(overrides))),
  ];
}

/**
 * Arranque suspenso: o `AuthProvider` fica em `restoring` até o teste chamar `release`,
 * que responde como sessão anónima.
 *
 * O teste tem de libertar sempre o arranque antes de terminar: o `inFlightRefresh` do
 * cliente é estado de módulo e ficaria pendurado para os testes seguintes do ficheiro.
 */
export function pendingRestore() {
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });

  return {
    handlers: [
      http.post(`${API}/auth/csrf`, async () => {
        await released;
        return HttpResponse.json(problem('authentication_refresh_token_required'), {
          status: 401,
        });
      }),
    ],
    release: () => release(),
  };
}
