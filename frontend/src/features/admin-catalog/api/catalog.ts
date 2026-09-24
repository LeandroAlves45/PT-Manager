import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

export type CatalogKind = 'foods' | 'exercises' | 'supplements';
export type CatalogItem =
  | components['schemas']['GlobalFoodResponse']
  | components['schemas']['GlobalExerciseResponse']
  | components['schemas']['GlobalSupplementResponse'];

export interface CatalogFilters {
  activity: 'active' | 'archived' | 'all';
  search: string;
  page: number;
}

export const catalogKeys = {
  all: ['admin-catalog'] as const,
  list: (kind: CatalogKind, filters: CatalogFilters) =>
    [...catalogKeys.all, kind, filters] as const,
};

/** A API devolve itens e total na mesma resposta; nunca há pedido por linha. */
export function useCatalogQuery(kind: CatalogKind, filters: CatalogFilters) {
  return useQuery({
    queryKey: catalogKeys.list(kind, filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) => {
      const query = {
        activity: filters.activity,
        search: filters.search || undefined,
        page_number: filters.page,
        page_size: 25,
      };
      switch (kind) {
        case 'foods':
          return unwrap(await apiClient.GET('/api/v1/global-foods', { params: { query }, signal }));
        case 'exercises':
          return unwrap(
            await apiClient.GET('/api/v1/global-exercises', { params: { query }, signal })
          );
        case 'supplements':
          return unwrap(
            await apiClient.GET('/api/v1/global-supplements', { params: { query }, signal })
          );
      }
    },
  });
}

/** Invalida listas e totais da visão geral depois de uma ação confirmada. */
export function useCatalogAction(kind: CatalogKind) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      action,
    }: {
      id: string;
      action: 'archive' | 'reactivate' | 'delete';
    }) => {
      switch (kind) {
        case 'foods':
          if (action === 'archive')
            return unwrap(
              await apiClient.POST('/api/v1/global-foods/{foodId}/archive', {
                params: { path: { foodId: id } },
              })
            );
          if (action === 'reactivate')
            return unwrap(
              await apiClient.POST('/api/v1/global-foods/{foodId}/reactivate', {
                params: { path: { foodId: id } },
              })
            );
          return unwrap(
            await apiClient.DELETE('/api/v1/global-foods/{foodId}', {
              params: { path: { foodId: id } },
            })
          );

        case 'exercises':
          if (action === 'archive')
            return unwrap(
              await apiClient.POST('/api/v1/global-exercises/{exerciseId}/archive', {
                params: { path: { exerciseId: id } },
              })
            );
          if (action === 'reactivate')
            return unwrap(
              await apiClient.POST('/api/v1/global-exercises/{exerciseId}/reactivate', {
                params: { path: { exerciseId: id } },
              })
            );
          return unwrap(
            await apiClient.DELETE('/api/v1/global-exercises/{exerciseId}', {
              params: { path: { exerciseId: id } },
            })
          );

        case 'supplements':
          if (action === 'archive')
            return unwrap(
              await apiClient.POST('/api/v1/global-supplements/{supplementId}/archive', {
                params: { path: { supplementId: id } },
              })
            );
          if (action === 'reactivate')
            return unwrap(
              await apiClient.POST('/api/v1/global-supplements/{supplementId}/reactivate', {
                params: { path: { supplementId: id } },
              })
            );
          return unwrap(
            await apiClient.DELETE('/api/v1/global-supplements/{supplementId}', {
              params: { path: { supplementId: id } },
            })
          );
      }
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: catalogKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
      ]);
    },
  });
}
