import { isApiProblem } from '@/shared/api/problem';

/**
 * Tiers, estados, mensagens e chaves de idempotência da subscrição — fonte única, usada pela
 * página Subscrição e pelo cartão da sidebar.
 *
 * Valores reais do contrato (`Domain/ValueObjects/SubscriptionTier.cs` e
 * `SubscriptionStatus.cs`): tiers `FREE` (5 clientes), `STARTER` (25) e `PRO` (sem limite);
 * estados `ACTIVE`, `INACTIVE`, `SUSPENDED` e `CANCELLED`, sempre em maiúsculas.
 */

/** Tier que se pode comprar por Checkout. */
export type PaidTier = 'STARTER' | 'PRO';

/** Planos pela ordem de apresentação. O preço vive no Stripe e não é exposto pela API. */
export const PLANS = [
  { tier: 'FREE', label: 'Free', clients: '5 clientes' },
  { tier: 'STARTER', label: 'Starter', clients: '25 clientes' },
  { tier: 'PRO', label: 'Pro', clients: 'Clientes ilimitados' },
] as const;

/** Planos que se compram por Checkout. */
export const PAID_PLANS = PLANS.filter(
  (plan): plan is Extract<(typeof PLANS)[number], { tier: PaidTier }> => plan.tier !== 'FREE'
);

/** Rótulo de um tier, com o valor bruto como alternativa para um tier futuro. */
export function tierLabel(tier: string): string {
  return PLANS.find((plan) => plan.tier === tier)?.label ?? tier;
}

/** Rótulos PT-PT dos estados. */
export const STATUS_LABELS: Readonly<Record<string, string>> = {
  ACTIVE: 'Ativa',
  INACTIVE: 'Inativa',
  SUSPENDED: 'Suspensa',
  CANCELLED: 'Cancelada',
};

/** Qualquer estado que não 'ACTIVE' exige atenção: o servidor recusa clientes novos. */
export function hasPaymentIssue(status: string): boolean {
  return status !== 'ACTIVE';
}

/**
 * Pode iniciar um Checkout: no plano gratuito, ou com uma subscrição paga que já terminou.
 * Com uma subscrição paga ativa, mudar de plano faz-se no portal do Stripe
 * (`billing_use_customer_portal`).
 */
export function canCheckout(subscription: { tier: string; status: string }): boolean {
  return (
    subscription.tier === 'FREE' ||
    subscription.status === 'INACTIVE' ||
    subscription.status === 'CANCELLED'
  );
}

/** Mensagens dos códigos estáveis de `BillingErrors`. */
const BILLING_ERRORS: Readonly<Record<string, string>> = {
  billing_provider_disabled: 'Os pagamentos estão temporariamente indisponíveis.',
  billing_provider_unavailable: 'O serviço de pagamentos não respondeu. Tenta mais tarde.',
  billing_provider_invalid_response: 'O serviço de pagamentos não respondeu. Tenta mais tarde.',
  billing_provider_configuration_mismatch: 'Os pagamentos estão temporariamente indisponíveis.',
  billing_active_checkout_exists:
    'Já tens um pagamento iniciado. Conclui-o ou aguarda 30 minutos para escolher outro plano.',
  billing_use_customer_portal: 'Já tens uma subscrição paga. Muda de plano em "Gerir subscrição".',
  billing_checkout_not_available: 'Esta conta não precisa de subscrição.',
  billing_customer_not_linked: 'Ainda não há uma subscrição paga para gerir.',
  billing_concurrency_conflict: 'O pedido cruzou-se com outro. Tenta novamente.',
  billing_checkout_lease_lost: 'O pedido cruzou-se com outro. Tenta novamente.',
  billing_operation_tier_conflict: 'O pedido cruzou-se com outro. Tenta novamente.',
  rate_limit_exceeded: 'Foram feitos demasiados pedidos. Aguarda e tenta novamente.',
};

/** Mensagem de uma falha de billing. */
export function billingErrorMessage(error: unknown): string {
  if (!isApiProblem(error)) return 'Não foi possível contactar o servidor. Tenta novamente.';

  return BILLING_ERRORS[error.code] ?? 'Não foi possível abrir o pagamento. Tenta novamente.';
}
