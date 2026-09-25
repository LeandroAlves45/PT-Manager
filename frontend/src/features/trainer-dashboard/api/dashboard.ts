import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';

/**
 * Query keys do painel do personal trainer.
 *
 * Exportadas pelo `index.ts` porque outras features (clientes, e nas fatias seguintes
 * sessões, packs, planos e check-ins) invalidam o painel depois de escrever.
 */
export const trainerDashboardKeys = {
  all: ['trainer-dashboard'] as const,
};

/**
 * Painel agregado do personal trainer.
 *
 * Orçamento de pedidos: um único `GET /api/v1/dashboard` ao montar; todos os blocos saem
 * desta resposta. Invalidado por qualquer escrita
 * de clientes e pela ação "Registar presença".
 */
export function useTrainerDashboardQuery() {
  return useQuery({
    queryKey: trainerDashboardKeys.all,
    queryFn: ({ signal }) => apiClient.GET('/api/v1/dashboard', { signal }).then(unwrap),
  });
}

/**
 * "Registar presença": marca a sessão de hoje como realizada.
 *
 * O backend consome o pack associado e recusa a transição antes da hora de início
 * (`session_transition_too_early`); quem chama traduz esse código.
 */
export function useCompleteSessionMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (sessionId: string) =>
      unwrap(
        await apiClient.POST('/api/v1/sessions/{sessionId}/complete', {
          params: { path: { sessionId } },
        })
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: trainerDashboardKeys.all }),
  });
}
