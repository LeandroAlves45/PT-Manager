import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { clientKeys } from '@/features/clients';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { apiClient, unwrap } from '@/shared/api/client';

/** Séries realizadas por pedido: o máximo que a vista agrupada precisa de uma só vez. */
export const SET_LOGS_PAGE_SIZE = 100;

/** Chaves próprias das páginas de treino que não existem em `prescriptionKeys`. */
export const trainingKeys = {
  /** Conclusões de treino de um plano (nota do cliente por dia). */
  completions: (clientId: string, planId: string) =>
    ['workout-completions', clientId, planId] as const,
};

/** Detalhe de um plano de treino; desativado (e por isso `isLoading`) quando `planId` é `null`. */
export function useTrainingPlanQuery(planId: string | null) {
  return useQuery({
    queryKey: prescriptionKeys.trainingDetail(planId ?? ''),
    enabled: planId !== null,
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/training-plans/{trainingPlanId}', {
          params: { path: { trainingPlanId: planId ?? '' } },
          signal,
        })
        .then(unwrap),
  });
}

/** Ficha do cliente, para mostrar o nome do cliente fixo (`?client_id=`) num plano novo. */
export function useFixedClientQuery(clientId: string | null) {
  return useQuery({
    queryKey: clientKeys.detail(clientId ?? ''),
    enabled: clientId !== null,
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/clients/{clientId}', {
          params: { path: { clientId: clientId ?? '' } },
          signal,
        })
        .then(unwrap),
  });
}

/** Página de séries realizadas de um plano (mais recentes primeiro). */
export function useSetLogsQuery(clientId: string | undefined, planId: string, page: number) {
  return useQuery({
    queryKey: prescriptionKeys.logs(clientId ?? '', planId, page),
    enabled: clientId !== undefined,
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/exercise-set-logs', {
          params: {
            query: {
              client_id: clientId,
              training_plan_id: planId,
              page_number: page,
              page_size: SET_LOGS_PAGE_SIZE,
            },
          },
          signal,
        })
      ),
  });
}

/** Conclusões de treino do plano, com a nota que o cliente escreveu (só leitura). */
export function useWorkoutCompletionsQuery(clientId: string | undefined, planId: string) {
  return useQuery({
    queryKey: trainingKeys.completions(clientId ?? '', planId),
    enabled: clientId !== undefined,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/workout-completions', {
          params: {
            query: { client_id: clientId, training_plan_id: planId, page_size: 100 },
          },
          signal,
        })
      ),
  });
}
