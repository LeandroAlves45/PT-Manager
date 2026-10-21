import type { components } from '@/shared/api/schema';

/**
 * Estados, filtros, rótulos e mensagens de erro das avaliações periódicas (antes «check-ins»;
 * o nome de código e o endereço mantêm-se) -> fonte única.
 */

type CheckIn = components['schemas']['CheckInResponse'];

/**
 * Filtro de estado aceite em `GET /check-ins?status=` (`CheckInStatusFilter`). O binder de
 * query aceita estes nomes em minúsculas (`Api/Http/QueryEnumModelBinderProvider.cs`); é o
 * que o painel usa em "Rever check-ins" (`?status=unreviewed`).
 */
export type CheckInStatusFilter = NonNullable<components['schemas']['CheckInStatusFilter']>;

/** Filtros pela ordem do selector. 'unreviewed' primeiro; é o trabalho por fazer. */
export const CHECK_IN_FILTERS = [
  'unreviewed',
  'scheduled',
  'answered',
  'missed',
  'cancelled',
] as const satisfies readonly CheckInStatusFilter[];

/** Rótulos PT-PT dos filtros. */
export const CHECK_IN_FILTER_LABELS: Readonly<Record<CheckInStatusFilter, string>> = {
  unreviewed: 'Por rever',
  scheduled: 'Agendadas',
  answered: 'Respondidas',
  missed: 'Em falta',
  cancelled: 'Canceladas',
};

/**
 * Rótulos do 'status' da resposta. O servidor só devolve quatro estados; "por rever" é um
 * respondido sem 'reviewed_at', assinalado á parte com 'isAwaitingReview'.
 */
export const CHECK_IN_STATUS_LABELS: Readonly<Record<string, string>> = {
  scheduled: 'Agendada',
  answered: 'Respondida',
  missed: 'Em falta',
  cancelled: 'Cancelada',
};

/** Respondida e ainda não revista: é o que conta no KPI do painel. */
export function isAwaitingReview(checkIn: CheckIn): boolean {
  return checkIn.status === 'answered' && checkIn.reviewed_at === null;
}

/**
 * Pode ser reagendado ou cancelado: agendado e depois de hoje. O servidor decide com o dia
 * local do personal trainer; aqui só se esconde a ação quando seria certamente recusada.
 *
 * @param todayKey Hoje em `yyyy-MM-dd`.
 */
export function isOpenFuture(checkIn: CheckIn, todayKey: string): boolean {
  return checkIn.status === 'scheduled' && checkIn.check_in_date > todayKey;
}

/** Mensagens dos códigos estáveis de `AssessmentErrors` e da validação dos check-ins. */
export const CHECK_IN_ERRORS: Readonly<Record<string, string>> = {
  check_in_not_found: 'Esta avaliação já não existe.',
  check_in_date_conflict: 'O cliente já tem uma avaliação nesse dia.',
  check_in_date_not_allowed: 'Escolhe hoje ou um dia futuro.',
  check_in_cannot_be_rescheduled: 'Só é possível reagendar antes do dia da avaliação.',
  check_in_cannot_be_cancelled: 'Só é possível cancelar antes do dia da avaliação.',
  check_in_not_answered: 'Esta avaliação ainda não foi respondida.',
  check_in_cancelled: 'Esta avaliação foi cancelada.',
  assessment_client_inactive: 'Reativa o cliente antes de agendar avaliações.',
  client_not_found: 'Este cliente já não existe.',
  target_date_before_check_in: 'A data-alvo não pode ser anterior à avaliação.',
  rate_limit_exceeded: 'Foram feitos demasiados pedidos. Aguarda e tenta novamente.',
};

/** Mensagem de erro de check-in, com a alternativa genérica dada por quem chama. */
export function checkInErrorMessage(code: string, fallback: string): string {
  return CHECK_IN_ERRORS[code] ?? fallback;
}
