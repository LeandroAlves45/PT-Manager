import { useQuery } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';

/**
 * Query keys do portal do cliente.
 *
 * Exportadas pelo `index.ts`: as fases seguintes (treino, tomas, check-ins) invalidam a home
 * depois de escrever, porque os cartões resumem esses dados.
 */
export const portalKeys = {
  all: ['portal'] as const,
  branding: () => [...portalKeys.all, 'branding'] as const,
  home: () => [...portalKeys.all, 'home'] as const,
};

/**
 * Marca do personal trainer do cliente autenticado.
 *
 * Um pedido por sessão do portal: a marca só muda quando o personal trainer a edita, por isso fica
 * fresca durante 5 minutos e não volta a ser pedida ao trocar de ecrã.
 */
export function usePortalBrandingQuery() {
  return useQuery({
    queryKey: portalKeys.branding(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/branding', { signal })),
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Home agregada do portal: treino, nutrição, suplementos e próximo check-in num só pedido.
 *
 * Orçamento: um `GET /api/v1/portal/home` ao abrir o Início; nenhum pedido por cartão.
 */
export function usePortalHomeQuery() {
  return useQuery({
    queryKey: portalKeys.home(),
    queryFn: async ({ signal }) => unwrap(await apiClient.GET('/api/v1/portal/home', { signal })),
  });
}
