import createClient, { type Middleware } from 'openapi-fetch';

import type { components, paths } from '@/shared/api/schema';
import { toApiProblem } from '@/shared/api/problem';
import {
  clearSession,
  getAccessToken,
  getCsrfToken,
  setSession,
  toSession,
} from '@/shared/api/session';
import { publishSessionInvalidated } from '@/shared/api/session-events';
import { env } from '@/shared/config/env';

/**
 * Cliente HTTP único da aplicação.
 *
 * Tipado pelo `schema.d.ts` gerado do OpenAPI: rotas, parâmetros de query, corpos de
 * pedido e corpos de resposta vêm todos do contrato. Nenhum componente faz `fetch` nem
 * declara tipos de resposta à mão.
 *
 * Responsabilidades do middleware:
 * 1. `Authorization: Bearer` com o token em memória;
 * 2. `X-CSRF-Token` nas rotas de autenticação, que usam o cookie de refresh;
 * 3. refresh único e serializado quando a API devolve 401, com uma só repetição.
 */

const AUTH_PREFIX = `${env.apiPrefix}/auth`;

/** Rotas que nunca devem despoletar refresh: são elas próprias o mecanismo de sessão. */
const SESSION_ENDPOINTS = new Set([
  `${AUTH_PREFIX}/refresh`,
  `${AUTH_PREFIX}/csrf`,
  `${AUTH_PREFIX}/login`,
  `${AUTH_PREFIX}/logout`,
]);

/** Nome do lock partilhado entre separadores.*/
const REFRESH_LOCK = 'pt-manager.session.refresh';

let inFlightRefresh: Promise<boolean> | null = null;
const retryableRequests = new WeakMap<Request, Request>();

/**
 * Renova a sessão a partir do cookie de refresh.
 *
 * Serializado em duas camadas porque o backend deteta reutilização do refresh token e
 * mata a sessão ao primeiro sinal de duplicação:
 *
 * - dentro do separador, `inFlightRefresh` junta todos os pedidos concorrentes num só;
 * - entre separadores, a Web Locks API garante que dois separadores nunca rodam o mesmo
 *   token ao mesmo tempo.
 *
 * @returns `true` quando há sessão nova; `false` quando é preciso voltar ao login.
 */
export function refreshSession(): Promise<boolean> {
  inFlightRefresh ??= runExclusive(async () => {
    // O CSRF é obtido já dentro do lock. Assim, outro separador não o consegue substituir
    // entre o bootstrap e a rotação do refresh cookie partilhado.
    const csrfResponse = await fetch(`${env.apiBaseUrl}${AUTH_PREFIX}/csrf`, {
      method: 'POST',
      credentials: 'include',
    });

    if (csrfResponse.status === 401 || csrfResponse.status === 403) {
      clearSession();
      return false;
    }

    if (!csrfResponse.ok) {
      throw toApiProblem(csrfResponse.status, await readResponseBody(csrfResponse));
    }

    const csrf = (await csrfResponse.json()) as components['schemas']['CsrfResponse'];
    const response = await fetch(`${env.apiBaseUrl}${AUTH_PREFIX}/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'X-CSRF-Token': csrf.csrf_token },
    });

    if (response.status === 401 || response.status === 403) {
      clearSession();
      publishSessionInvalidated();
      return false;
    }

    if (!response.ok) throw toApiProblem(response.status, await readResponseBody(response));

    setSession(toSession((await response.json()) as components['schemas']['SessionResponse']));
    return true;
  }).finally(() => {
    inFlightRefresh = null;
  });

  return inFlightRefresh;
}

async function readResponseBody(response: Response): Promise<unknown> {
  const text = await response.text();

  if (text.length === 0) return undefined;

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}

/**
 * Corre a operação com exclusão mútua entre separadores.
 *
 * `navigator.locks` não existe em contextos inseguros nem no jsdom dos testes; nesses
 * casos degrada para execução directa, que continua correcta porque o `inFlightRefresh`
 * já serializa dentro do separador.
 */
async function runExclusive<T>(operation: () => Promise<T>): Promise<T> {
  if (typeof navigator === 'undefined' || navigator.locks === undefined) {
    return operation();
  }

  return await navigator.locks.request(REFRESH_LOCK, operation);
}

function isSessionEndpoint(url: string): boolean {
  return SESSION_ENDPOINTS.has(new URL(url, env.apiBaseUrl).pathname);
}

const authMiddleware: Middleware = {
  onRequest({ request }) {
    const accessToken = getAccessToken();
    if (accessToken !== null) {
      request.headers.set('Authorization', `Bearer ${accessToken}`);
    }

    // O CSRF só protege o que usa o cookie de refresh, e é aí que o backend o exige.
    if (request.url.includes(AUTH_PREFIX)) {
      const csrfToken = getCsrfToken();
      if (csrfToken !== null) request.headers.set('X-CSRF-Token', csrfToken);
    }

    // O corpo de um Request só pode ser consumido uma vez. A cópia para uma eventual
    // repetição tem de existir antes do primeiro fetch, nunca dentro de onResponse.
    if (!isSessionEndpoint(request.url)) retryableRequests.set(request, request.clone());

    return request;
  },

  async onResponse({ request, response }) {
    if (response.status !== 401 || isSessionEndpoint(request.url)) {
      retryableRequests.delete(request);
      return response;
    }

    const retry = retryableRequests.get(request);
    retryableRequests.delete(request);
    if (retry === undefined) return response;

    // Uma só repetição, e só depois de o refresh ter sucedido. Repetir um POST após um
    // refresh falhado arriscaria duplicar uma escrita sem qualquer hipótese de sucesso.
    const refreshed = await refreshSession();
    if (!refreshed) return response;

    const accessToken = getAccessToken();
    if (accessToken !== null) retry.headers.set('Authorization', `Bearer ${accessToken}`);

    return fetch(retry);
  },
};

export const apiClient = createClient<paths>({
  baseUrl: env.apiBaseUrl,
  credentials: 'include',
  fetch: (request) => globalThis.fetch(request),
});

apiClient.use(authMiddleware);

/**
 * Lança `ApiProblem` quando a chamada falhou, devolvendo os dados quando correu bem.
 *
 * Usado por todos os hooks de feature: o TanStack Query trata um `throw` como erro e
 * um valor devolvido como sucesso, por isso é aqui que o contrato de erro se fecha.
 *
 * @param result Resultado devolvido por `apiClient.GET`/`POST`/...
 */
export function unwrap<TData>(result: {
  data?: TData;
  error?: unknown;
  response: Response;
}): TData {
  if (result.error !== undefined || !result.response.ok)
    throw toApiProblem(result.response.status, result.error);

  return result.data as TData;
}
