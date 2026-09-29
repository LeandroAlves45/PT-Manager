import type { SessionStatus } from '@/features/sessions/lib/sessionStatus';

/** Filtros de uma listagem de sessões (`GET /sessions`). Instantes em ISO 8601 com offset. */
export interface SessionListFilters {
  readonly clientId: string | null;
  readonly status: SessionStatus | null;
  readonly startsFrom: string | null;
  readonly startsBefore: string | null;
  readonly page: number;
  readonly pageSize: number;
}

/**
 * Query keys da feature sessions.
 *
 * Todas as listagens (agenda do dia, lista, tab do cliente) vivem debaixo de `lists()`:
 * qualquer escrita numa sessão invalida-as de uma vez.
 */
export const sessionKeys = {
  all: ['sessions'] as const,
  lists: () => [...sessionKeys.all, 'list'] as const,
  list: (filters: SessionListFilters) => [...sessionKeys.lists(), filters] as const,
};
