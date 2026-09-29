import { keepPreviousData, useQuery } from '@tanstack/react-query';

import {
  packKeys,
  type ClientPackListFilters,
  type PackTypeListFilters,
} from '@/features/packs/api/keys';
import { apiClient, unwrap } from '@/shared/api/client';

/** Tamanho de página das tabelas de tipos de pack e de packs dos clientes. */
export const PACK_PAGE_SIZE = 25;

/**
 * Máximo que a API aceita por página (`PaginationValidationRules`). Chega para a lista de
 * tipos ativos do formulário "Atribuir pack": um personal trainer não vende 100 tipos diferentes.
 */
const MAX_PAGE_SIZE = 100;

/**
 * Página da tabela de tipos de pack.
 *
 * Orçamento de pedidos: um `GET /pack-types` por combinação de filtros; `keepPreviousData`
 * mantém a tabela visível ao mudar de página ou filtro.
 */
export function usePackTypeListQuery(filters: PackTypeListFilters) {
  return useQuery({
    queryKey: packKeys.typeList(filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/pack-types', {
          params: {
            query: {
              activity: filters.activity,
              search: filters.search || undefined,
              page_number: filters.page,
              page_size: PACK_PAGE_SIZE,
            },
          },
          signal,
        })
      ),
  });
}

/**
 * Tipos de pack ativos, para o `<select>` de "Atribuir pack".
 *
 * Só corre quando o formulário está aberto (`enabled`), para não pagar um pedido em cada
 * visita à tab.
 */
export function useActivePackTypesQuery(enabled: boolean) {
  return useQuery({
    queryKey: packKeys.activeTypes(),
    enabled,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/pack-types', {
          params: { query: { activity: 'active', page_number: 1, page_size: MAX_PAGE_SIZE } },
          signal,
        })
      ).items,
  });
}

/** Página da tabela de packs dos clientes (todos ou só os de um cliente.) */
export function useClientPackListQuery(filters: ClientPackListFilters) {
  return useQuery({
    queryKey: packKeys.clientPackList(filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/client-session-packs', {
          params: {
            query: {
              client_id: filters.clientId ?? undefined,
              activity: filters.activity,
              page_number: filters.page,
              page_size: PACK_PAGE_SIZE,
            },
          },
          signal,
        })
      ),
  });
}

/**
 * Packs com saldo de um cliente, pela ordem do servidor (fim previsto mais cedo primeiro,
 * sem data no fim). O primeiro é o que "Marcar sessão" pré-escolhe.
 *
 * @param clientId Cliente escolhido, ou `null` enquanto não há cliente (sem pedido).
 */
export function useUsablePacksQuery(clientId: string | null) {
  return useQuery({
    queryKey: packKeys.usable(clientId ?? ''),
    enabled: clientId !== null,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/client-session-packs/usable', {
          params: { query: { client_id: clientId ?? '' } },
          signal,
        })
      ),
  });
}
