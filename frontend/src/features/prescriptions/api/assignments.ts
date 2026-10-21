import { useQuery } from '@tanstack/react-query';

import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

export type SupplementAssignment = components['schemas']['ClientSupplementAssignmentResponse'];

/**
 * Query keys das páginas de atribuições de suplementos. Ficam sob `prescriptionKeys.assignments`,
 * por isso uma escrita invalida listas e detalhe com um único `invalidateQueries`.
 */
export const assignmentKeys = {
  /** Uma atribuição, para a página de edição. */
  item: (id: string) => [...prescriptionKeys.assignments, 'item', id] as const,
};

/**
 * Uma atribuição para a página de edição.
 *
 * Sem cache entre visitas (`gcTime: 0`): o formulário arranca com os valores recebidos, e um
 * valor antigo da cache deixaria o trainer a editar dados desatualizados.
 */
export function useSupplementAssignmentQuery(assignmentId: string) {
  return useQuery({
    queryKey: assignmentKeys.item(assignmentId),
    gcTime: 0,
    refetchOnWindowFocus: false,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/supplement-assignments/{assignmentId}', {
          params: { path: { assignmentId } },
          signal,
        })
      ),
  });
}

/**
 * Procura a atribuição que causou o 409 `supplement_assignment_already_exists`, em páginas
 * de 100 (ativas e inativas) do cliente, até a encontrar ou esgotar o total.
 */
export async function findExistingAssignment(
  clientId: string,
  supplementId: string
): Promise<SupplementAssignment | null> {
  let page = 1;
  while (true) {
    const result = unwrap(
      await apiClient.GET('/api/v1/supplement-assignments', {
        params: {
          query: {
            client_id: clientId,
            activity: 'all',
            page_number: page,
            page_size: 100,
          },
        },
      })
    );
    const found = result.items.find((item) => item.supplement_id === supplementId);
    if (found !== undefined) return found;
    if (page * 100 >= result.total_count) return null;
    page++;
  }
}
