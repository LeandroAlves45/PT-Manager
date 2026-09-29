import type { components } from '@/shared/api/schema';

/**
 * Estados, rótulos e mensagens de erro das sessões — fonte única.
 */

/**
 * Estado de uma sessão, tal como a API o devolve e o aceita em `GET /sessions?status=`
 * (`SessionStatusFilter` do contrato; o binder de query aceita estes nomes snake_case
 * desde a 6E-2 — `Api/Http/QueryEnumModelBinderProvider.cs`).
 */
export type SessionStatus = NonNullable<components['schemas']['SessionStatusFilter']>;

/** Todos os estados, pela ordem do filtro. */
export const SESSION_STATUSES = [
  'scheduled',
  'completed',
  'cancelled_by_client',
  'cancelled_by_trainer',
  'no_show',
] as const satisfies readonly SessionStatus[];

/** Rótulos PT-PT dos estados de sessão. */
export const SESSION_STATUS_LABELS: Readonly<Record<string, string>> = {
  scheduled: 'Agendada',
  completed: 'Realizada',
  cancelled_by_client: 'Cancelada pelo cliente',
  cancelled_by_trainer: 'Cancelada por ti.',
  no_show: 'Faltou',
};

/** Ações sobre uma sessão, pelo sufixo da rota (`POST /sessions/{id}/<ação>`). */
export type SessionTransition =
  'complete' | 'no-show' | 'cancel-by-trainer' | 'cancel-by-client' | 'restore';

/** Mensagem de sucesso de cada ação, para o toast. */
export const TRANSITION_SUCCESS: Readonly<Record<SessionTransition, string>> = {
  complete: 'Presença registada.',
  'no-show': 'Falta registada.',
  'cancel-by-trainer': 'Sessão cancelada por ti.',
  'cancel-by-client': 'Sessão cancelada pelo cliente.',
  restore: 'Sessão reposta como agendada.',
};

/** Recusas de sessão que não pertencem a um campo (`SessionErrors`, `ClientErrors`) */
export const SESSION_ERRORS: Readonly<Record<string, string>> = {
  session_transition_too_early: 'Só podes registar a presença ou a falta depois da hora de início.',
  session_pack_balance_unavailable: 'O pack associado já não tem sessões disponíveis.',
  session_invalid_state: 'A sessão já mudou de estado. Atualiza a lista.',
  session_pack_not_available: 'Esse pack já não tem saldo ou não é deste cliente.',
  session_client_inactive: 'Este cliente está arquivado. Reativa-o antes de marcar sessões.',
  session_client_day_conflict: 'Este cliente já tem uma sessão marcada nesse dia.',
  session_schedule_conflict: 'Já tens outra sessão marcada a essa hora.',
  session_not_found: 'Esta sessão já não existe.',
  client_not_found: 'Este cliente já não existe.',
};
