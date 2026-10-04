import { useMutation, useQuery } from '@tanstack/react-query';

import type { PaidTier } from '@/features/billing/lib/subscription';
import { apiClient, unwrap } from '@/shared/api/client';

/** Query keys da subscrição. A sidebar e a página partilham a mesma entrada. */
export const billingKeys = {
  all: ['billing'] as const,
  subscription: () => [...billingKeys.all, 'subscription'] as const,
};

/** Intervalo entre leituras enquanto se espera pelo webhook do Checkout. */
export const CHECKOUT_POLL_MS = 3000;

/** Leituras no máximo (~30 s): só depois disso o personal trainer atualiza a página se precisar. */
export const CHECKOUT_POLL_LIMIT = 10;

/**
 * Estado da subscrição do personal trainer (`GET /billing/subscription`).
 *
 * @param awaitingCheckout Verdadeiro no regresso de um Checkout pago: o plano só muda quando
 * o webhook do Stripe chega, por isso a query volta a ler a cada 3 s até a subscrição
 * ficar paga e ativa, ou até ao limite de leituras.
 */
export function useSubscriptionQuery({ awaitingCheckout = false } = {}) {
  return useQuery({
    queryKey: billingKeys.subscription(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/billing/subscription', { signal })),
    refetchInterval: (query) => {
      if (!awaitingCheckout) return false;

      const data = query.state.data;
      const paid = data !== undefined && data.tier !== 'FREE' && data.status === 'ACTIVE';

      return paid || query.state.dataUpdateCount >= CHECKOUT_POLL_LIMIT ? false : CHECKOUT_POLL_MS;
    },
  });
}

/**
 * Abre o Checkout alojado do Stripe para um tier e devolve o URL.
 *
 * O cabeçalho `Idempotency-Key` é obrigatório (UUID canónico) e o corpo é fechado: só
 * `tier`. Quem chama reutiliza a mesma chave nas repetições do mesmo tier; depois de voltar
 * do Stripe a chave é outra, e o servidor retoma o Checkout aberto do mesmo tier
 * (`BillingCheckoutStore.ReserveAsync`, 6E-5) em vez de o recusar.
 */
export function useCheckoutMutation() {
  return useMutation({
    mutationFn: async ({ tier, idempotencyKey }: { tier: PaidTier; idempotencyKey: string }) =>
      unwrap(
        await apiClient.POST('/api/v1/billing/checkout', {
          body: { tier },
          headers: { 'Idempotency-Key': idempotencyKey },
        })
      ).checkout_url,
  });
}

/**
 * Abre o portal de cliente do Stripe e devolve o URL. Cada clique é uma operação nova; a
 * chave é gerada na chamada e as repetições automáticas do React Query não a mudam.
 */
export function usePortalMutation() {
  return useMutation({
    mutationFn: async (idempotencyKey: string) =>
      unwrap(
        await apiClient.POST('/api/v1/billing/customer-portal', {
          headers: { 'Idempotency-Key': idempotencyKey },
        })
      ).portal_url,
  });
}
