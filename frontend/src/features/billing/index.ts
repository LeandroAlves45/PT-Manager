/**
 * API pública da feature billing.
 *
 * O cartão da sidebar (`app/layouts/components/SubscriptionCard.tsx`) usa a mesma query e os
 * mesmos rótulos da página. A página entra no router pelo caminho.
 */
export { useSubscriptionQuery } from '@/features/billing/api/billing';
export { hasPaymentIssue, tierLabel } from '@/features/billing/lib/subscription';
