import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { checkInKeys, type CheckInListFilter } from '@/features/check-ins/api/keys';
import { apiClient, unwrap } from '@/shared/api/client';

/** Tamanho de página das listas de check-ins. */
export const CHECK_IN_PAGE_SIZE = 25;

/**
 * Listagem de check-ins — página global e tab do cliente.
 *
 * Orçamento de pedidos: um `GET /check-ins` por combinação de filtros; o nome do cliente
 * vem na resposta (`client_name`), nunca um pedido por linha. A API ordena por
 * `check_in_date` decrescente e calcula o estado com o dia local do personal trainer.
 */
export function useCheckInListQuery(filters: CheckInListFilter) {
  return useQuery({
    queryKey: checkInKeys.list(filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/check-ins', {
          params: {
            query: {
              client_id: filters.clientId ?? undefined,
              status: filters.status ?? undefined,
              from_date: filters.fromDate ?? undefined,
              to_date: filters.toDate ?? undefined,
              page_number: filters.page,
              page_size: filters.pageSize,
            },
          },
          signal,
        })
      ),
  });
}
