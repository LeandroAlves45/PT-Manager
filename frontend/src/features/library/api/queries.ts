import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { libraryKeys, type LibraryFilters } from '@/features/library/api/keys';
import { apiClient, unwrap } from '@/shared/api/client';

/** Tamanho de página das três tabelas da biblioteca. */
export const LIBRARY_PAGE_SIZE = 25;

/**
 * Query string comum: a API devolve globais ativos + privados do personal trainer, ordenadas por nome.
 */
function listQuery(filters: LibraryFilters) {
  return {
    activity: filters.activity,
    search: filters.search || undefined,
    page_number: filters.page,
    page_size: LIBRARY_PAGE_SIZE,
  };
}

/**
 * Página de exercícios (globais + privados).
 *
 * Orçamento: um `GET /exercises` por combinação de filtros; o estado do vídeo vem na própria
 * linha (`has_ready_video`), nunca um pedido por exercício. `keepPreviousData` mantém a tabela
 * visível ao mudar de página ou filtro.
 */
export function useExerciseListQuery(filters: LibraryFilters) {
  return useQuery({
    queryKey: libraryKeys.list('exercises', filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/exercises', { params: { query: listQuery(filters) }, signal })
      ),
  });
}

/** Página de alimentos (globais + privados); macros por 100 g e `kcal` calculado no servidor. */
export function useFoodListQuery(filters: LibraryFilters) {
  return useQuery({
    queryKey: libraryKeys.list('foods', filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/foods', { params: { query: listQuery(filters) }, signal })
      ),
  });
}

/** Página de suplementos (globais + privados). */
export function useSupplementListQuery(filters: LibraryFilters) {
  return useQuery({
    queryKey: libraryKeys.list('supplements', filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/supplements', {
          params: { query: listQuery(filters) },
          signal,
        })
      ),
  });
}
