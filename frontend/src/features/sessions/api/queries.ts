import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { sessionKeys, type SessionListFilters } from '@/features/sessions/api/keys';
import { apiClient, unwrap } from '@/shared/api/client';

/** Tamanho de página da lista de sessões. */
export const SESSION_PAGE_SIZE = 25;

/**
 * Página do dia na agenda: o máximo que a API aceita. Um personal trainer não tem 100 sessões num
 * dia (a agenda não deixa sobrepor), por isso o dia cabe sempre numa página.
 */
export const DAY_PAGE_SIZE = 100;

/**
 * Listagem de sessões — agenda do dia, lista com filtros e tab do cliente.
 *
 * Orçamento de pedidos: um `GET /sessions` por combinação de filtros; o nome do cliente vem
 * na própria resposta (`client_name`), nunca um pedido por linha. A API ordena por
 * `starts_at` crescente.
 */
export function useSessionListQuery(filters: SessionListFilters) {
  return useQuery({
    queryKey: sessionKeys.list(filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/sessions', {
          params: {
            query: {
              client_id: filters.clientId ?? undefined,
              status: filters.status ?? undefined,
              starts_from: filters.startsFrom ?? undefined,
              starts_before: filters.startsBefore ?? undefined,
              page_number: filters.page,
              page_size: filters.pageSize,
            },
          },
          signal,
        })
      ),
  });
}
