import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';

import { clientKeys } from '@/features/clients';
import { packKeys } from '@/features/packs/api/keys';
import { trainerDashboardKeys } from '@/features/trainer-dashboard';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type PackTypeRequest = components['schemas']['CreatePackTypeRequest'];
type AssignRequest = components['schemas']['AssignClientSessionPackRequest'];

/**
 * Invalida tudo o que uma escrita num pack de cliente pode mudar: as listas e os `usable`
 * de packs, o painel ("packs a terminar", "vendas de packs") e o detalhe do cliente
 * (badge de packs e KPI "Sessões restantes" do resumo).
 */
async function invalidateClientPackData(queryClient: QueryClient, clientId: string) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: packKeys.clientPacks() }),
    queryClient.invalidateQueries({ queryKey: trainerDashboardKeys.all }),
    queryClient.invalidateQueries({ queryKey: clientKeys.detail(clientId) }),
  ]);
}

/**
 * Cria (`packTypeId === null`) ou atualiza um tipo de pack.
 *
 * O PATCH do backend substitui todos os campos, por isso o corpo é o mesmo nos dois casos.
 * Mudar um tipo não mexe nos packs já vendidos (guardam nome e preço da altura).
 */
export function useSavePackTypeMutation(packTypeId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: PackTypeRequest) =>
      packTypeId === null
        ? unwrap(await apiClient.POST('/api/v1/pack-types', { body }))
        : unwrap(
            await apiClient.PATCH('/api/v1/pack-types/{packTypeId}', {
              params: { path: { packTypeId } },
              body,
            })
          ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: packKeys.types() }),
  });
}

/** Arquiva ou reativa um tipo de pack (204 sem corpo) */
export function usePackTypeActivityMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      packTypeId,
      action,
    }: {
      packTypeId: string;
      action: 'archive' | 'reactivate';
    }) => {
      const params = { params: { path: { packTypeId } } };
      unwrap(
        action === 'archive'
          ? await apiClient.POST('/api/v1/pack-types/{packTypeId}/archive', params)
          : await apiClient.POST('/api/v1/pack-types/{packTypeId}/reactivate', params)
      );
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: packKeys.types() }),
  });
}

/** Vende (atribui) um pack a um cliente. */
export function useAssignPackMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: AssignRequest) =>
      unwrap(await apiClient.POST('/api/v1/client-session-packs', { body })),
    onSuccess: (pack) => invalidateClientPackData(queryClient, pack.client_id),
  });
}

/**  Muda (ou remove, com 'null') a data prevista de fim de um pack. */
export function useUpdatePackEndDateMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      packId,
      expectedEndDate,
    }: {
      packId: string;
      expectedEndDate: string | null;
    }) =>
      unwrap(
        await apiClient.PATCH(
          '/api/v1/client-session-packs/{clientSessionPackId}/expected-end-date',
          {
            params: { path: { clientSessionPackId: packId } },
            body: { expected_end_date: expectedEndDate },
          }
        )
      ),
    onSuccess: (pack) => invalidateClientPackData(queryClient, pack.client_id),
  });
}

/**
 * Cancela um pack vendido por engano (204). O backend só aceita packs nunca usados e sem
 * sessões associadas; quem chama traduz `client_session_pack_used`/`_referenced`.
 */
export function useCancelPackMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ packId }: { packId: string; clientId: string }) => {
      unwrap(
        await apiClient.POST('/api/v1/client-session-packs/{clientSessionPackId}/cancel', {
          params: { path: { clientSessionPackId: packId } },
        })
      );
    },
    onSuccess: (_, { clientId }) => invalidateClientPackData(queryClient, clientId),
  });
}
