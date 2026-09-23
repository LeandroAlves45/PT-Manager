import type { components } from '@/shared/api/schema';

/**
 * Sessão do utilizador em memória.
 *
 * O access token e o token CSRF vivem apenas aqui — nunca em `localStorage` ou
 * `sessionStorage`. O refresh token nunca chega ao JavaScript: fica no cookie HttpOnly
 * `__Secure-ptm-refresh`, que o browser envia sozinho para `/api/v1/auth`.
 *
 * O módulo é deliberadamente independente do React: o cliente HTTP precisa de ler o token
 * fora de qualquer componente. A ligação ao React faz-se por `subscribe`.
 */

/** Papéis que o backend emite na claim `role`. */
export const APP_ROLES = ['superuser', 'trainer', 'client'] as const;

export type AppRole = (typeof APP_ROLES)[number];

type SessionResponse = components['schemas']['SessionResponse'];

/** Sessão ativa, já normalizada a partir da resposta da API. */
export interface Session {
  readonly userId: string;
  readonly trainerId: string | null;
  readonly role: AppRole;
  readonly accessToken: string;
  readonly accessTokenExpiresAt: string;
  readonly csrfToken: string;
}

let current: Session | null = null;
const listeners = new Set<() => void>();

/** Converte a resposta da API na sessão interna, validando o papel. */
export function toSession(response: SessionResponse): Session {
  const role = response.role as AppRole;

  if (!APP_ROLES.includes(role))
    throw new Error(`[pt-manager] Papel desconhecido devolvido pela API: ${response.role}`);

  return {
    userId: response.user_id,
    trainerId: response.trainer_id,
    role,
    accessToken: response.access_token,
    accessTokenExpiresAt: response.access_token_expires_at,
    csrfToken: response.csrf_token,
  };
}

/** Sessão atual, ou `null` quando ninguém está autenticado. */
export function getSession(): Session | null {
  return current;
}

/** Token a enviar em 'Authorization: Bearer'. */
export function getAccessToken(): string | null {
  return current?.accessToken ?? null;
}

/**
 * Token CSRF a enviar em `X-CSRF-Token`.
 *
 * Existe separado da sessão porque o arranque da app obtém um token CSRF com
 * `POST /auth/csrf` **antes** de haver sessão, para poder chamar `/auth/refresh`.
 */
let bootstrapCsrfToken: string | null = null;

export function getCsrfToken(): string | null {
  return current?.csrfToken ?? bootstrapCsrfToken;
}

export function setCsrfToken(token: string | null): void {
  bootstrapCsrfToken = token;
  notify();
}

/** Substitui a sessão ativa e avisa quem estiver a observar. */
export function setSession(session: Session | null): void {
  current = session;
  if (session !== null) {
    bootstrapCsrfToken = session.csrfToken;
  }
  notify();
}

/** Limpa tudo o que identifica o utilizador. Usado no logout e no refresh falhado. */
export function clearSession(): void {
  current = null;
  bootstrapCsrfToken = null;
  notify();
}

/**
 * Observa mudanças da sessão. Compatível com `useSyncExternalStore`.
 *
 * @param listener Função chamada a cada mudança.
 * @returns Função que cancela a observação.
 */

export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function notify(): void {
  for (const listener of listeners) {
    listener();
  }
}

/** Home de cada papel. Um utilizador nunca vê um ecrã de erro por estar na área errada. */
export function homeRouteFor(role: AppRole): string {
  switch (role) {
    case 'superuser':
      return '/admin';
    case 'trainer':
      return '/trainer';
    case 'client':
      return '/portal';
  }
}
