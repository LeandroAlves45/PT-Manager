import { useQuery } from '@tanstack/react-query';

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
 * desta resposta. Invalidado por qualquer escrita de clientes, sessões e packs.
 */
export function useTrainerDashboardQuery() {
  return useQuery({
    queryKey: trainerDashboardKeys.all,
    queryFn: ({ signal }) => apiClient.GET('/api/v1/dashboard', { signal }).then(unwrap),
  });
}
