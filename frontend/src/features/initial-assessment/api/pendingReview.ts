import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';

/** Tamanho de página da lista de avaliações iniciais por rever. */
export const PENDING_REVIEW_PAGE_SIZE = 25;

/**
 * Query keys das avaliações iniciais por rever. `all` serve para invalidar a lista e o
 * contador da navegação depois de uma revisão.
 */
export const initialAssessmentKeys = {
  all: ['initial-assessments'] as const,
  pendingReview: (page: number, pageSize: number) =>
    [...initialAssessmentKeys.all, 'pending-review', page, pageSize] as const,
};

/**
 * Página das avaliações iniciais gravadas pelos clientes e ainda por rever, das mais antigas
 * para as mais recentes.
 *
 * @param page Página (1-based).
 * @param pageSize Itens por página. O contador da navegação pede `1` e lê só `total_count`.
 */
export function usePendingInitialAssessmentsQuery(
  page: number,
  pageSize: number = PENDING_REVIEW_PAGE_SIZE
) {
  return useQuery({
    queryKey: initialAssessmentKeys.pendingReview(page, pageSize),
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: true,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/initial-assessments/pending-review', {
          params: { query: { page_number: page, page_size: pageSize } },
          signal,
        })
      ),
  });
}
