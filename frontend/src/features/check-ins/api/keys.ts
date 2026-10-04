import type { CheckInStatusFilter } from '@/features/check-ins/lib/checkInStatus';

/** Filtros de uma listagem de check-ins. Datas em 'yyyy-MM-dd'. */
export interface CheckInListFilter {
  readonly clientId: string | null;
  readonly status: CheckInStatusFilter | null;
  readonly fromDate: string | null;
  readonly toDate: string | null;
  readonly page: number;
  readonly pageSize: number;
}

/**
 * Query keys da feature check-ins.
 *
 * A página global e a tab do cliente vivem debaixo de `lists()`: qualquer escrita num
 * check-in invalida-as de uma vez, sem saber que filtros estavam ativos.
 */
export const checkInKeys = {
  all: ['check-ins'] as const,
  lists: () => [...checkInKeys.all, 'lists'] as const,
  list: (filter: CheckInListFilter) => [...checkInKeys.lists(), filter] as const,
};
