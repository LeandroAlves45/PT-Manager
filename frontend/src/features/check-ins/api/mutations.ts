import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';

import { checkInKeys } from '@/features/check-ins/api/keys';
import { clientKeys } from '@/features/clients';
import { trainerDashboardKeys } from '@/features/trainer-dashboard';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type CreateRequest = components['schemas']['CreateCheckInRequest'];
type RescheduleRequest = components['schemas']['RescheduleCheckInRequest'];
type CorrectRequest = components['schemas']['CorrectCheckInRequest'];

/** Ação sem corpo sobre um check-in (`POST /check-ins/{id}/<ação>`). */
export type CheckInAction = 'cancel' | 'review';

/**
 * Invalida tudo o que uma escrita num check-in pode mudar: as listas de check-ins, o painel
 * (KPI "check-ins por rever") e o detalhe do cliente (o peso atual e a adesão do resumo
 * vêm dos check-ins respondidos).
 */
async function invalidateCheckInData(queryClient: QueryClient, clientId: string) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: checkInKeys.all }),
    queryClient.invalidateQueries({ queryKey: trainerDashboardKeys.all }),
    queryClient.invalidateQueries({ queryKey: clientKeys.detail(clientId) }),
  ]);
}

/** Agenda um check-in (201). */
export function useCreateCheckInMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: CreateRequest) =>
      unwrap(await apiClient.POST('/api/v1/check-ins', { body })),
    onSuccess: (checkIn) => invalidateCheckInData(queryClient, checkIn.client_id),
  });
}

/** Move um check-in agendado para outro dia (só antes do dia original). */
export function useRescheduleCheckInMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ checkInId, body }: { checkInId: string; body: RescheduleRequest }) =>
      unwrap(
        await apiClient.PATCH('/api/v1/check-ins/{checkInId}/reschedule', {
          params: { path: { checkInId } },
          body,
        })
      ),
    onSuccess: (checkIn) => invalidateCheckInData(queryClient, checkIn.client_id),
  });
}

/**
 * Cancela (só futuro e sem resposta) ou marca como revisto (só respondido). As duas são
 * idempotentes no servidor: repetir devolve o mesmo check-in.
 */
export function useCheckInActionMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ checkInId, action }: { checkInId: string; action: CheckInAction }) =>
      unwrap(
        action === 'cancel'
          ? await apiClient.POST('/api/v1/check-ins/{checkInId}/cancel', {
              params: { path: { checkInId } },
            })
          : await apiClient.POST('/api/v1/check-ins/{checkInId}/review', {
              params: { path: { checkInId } },
            })
      ),
    onSuccess: (checkIn) => invalidateCheckInData(queryClient, checkIn.client_id),
  });
}

/**
 * Corrige os valores de um check-in respondido (`PUT /check-ins/{id}/answer`). Não é a
 * resposta do cliente: essa vem do portal. O `PUT` substitui todos os valores.
 */
export function useCorrectCheckInMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ checkInId, body }: { checkInId: string; body: CorrectRequest }) =>
      unwrap(
        await apiClient.PUT('/api/v1/check-ins/{checkInId}/answer', {
          params: { path: { checkInId } },
          body,
        })
      ),
    onSuccess: (checkIn) => invalidateCheckInData(queryClient, checkIn.client_id),
  });
}
