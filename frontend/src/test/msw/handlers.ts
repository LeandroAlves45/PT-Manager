import { http, HttpResponse } from 'msw';

import type { components } from '@/shared/api/schema';
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
  http.get(`${API}/billing/subscription`, () =>
    HttpResponse.json({
      tier: 'BASIC',
      status: 'active',
      client_limit: 10,
      current_client_count: 3,
    })
  ),
  http.get(`${API}/clients`, () =>
    HttpResponse.json({ items: [], page_number: 1, page_size: 5, total_count: 0 })
  ),
  // [6E] NOVO: `/trainer` deixou de ser um placeholder; qualquer teste que entre como
  // trainer carrega o painel. Sem clientes, para o ecrã ficar no estado mais leve.
  http.get(`${API}/dashboard`, () =>
    HttpResponse.json(dashboardResponse({ active_client_count: 0 }))
  ),
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
