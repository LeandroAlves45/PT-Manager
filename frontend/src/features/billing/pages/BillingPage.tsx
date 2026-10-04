import { CheckCircle2, Info } from 'lucide-react';
import { parseAsStringLiteral, useQueryState } from 'nuqs';
import { useRef, useState } from 'react';

import {
  useCheckoutMutation,
  usePortalMutation,
  useSubscriptionQuery,
} from '@/features/billing/api/billing';
import { redirectToProvider } from '@/features/billing/lib/redirect';
import {
  billingErrorMessage,
  canCheckout,
  hasPaymentIssue,
  PAID_PLANS,
  STATUS_LABELS,
  tierLabel,
  type PaidTier,
} from '@/features/billing/lib/subscription';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Progress } from '@/shared/components/ui/progress';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate, formatNumber } from '@/shared/lib/format';

/** Valor de `?checkout=` nos URLs de retorno configurados no Stripe. */
const CHECKOUT_RESULTS = ['success', 'cancelled'] as const;

/**
 * "Subscrição": plano atual, uso de clientes e ações de pagamento.
 *
 * - No plano gratuito (ou com a subscrição paga terminada): escolher Starter ou Pro abre o
 *   Checkout alojado do Stripe.
 * - Com plano pago: "Gerir subscrição" abre o portal do Stripe (mudar de plano, faturas,
 *   cancelar).
 *
 * No regresso do Stripe o URL traz `?checkout=success|cancelled` (URLs de retorno nos
 * secrets): a página mostra o aviso. O plano só muda quando o webhook chega, por isso no
 * sucesso a subscrição volta a ser lida a cada 3 s até ficar paga (máx. ~30 s).
 */
export function BillingPage() {
  const [result, setResult] = useQueryState('checkout', parseAsStringLiteral(CHECKOUT_RESULTS));

  // O regresso do Stripe é um carregamento novo da app: a primeira leitura já é fresca. Só o
  // sucesso volta a ler, porque o webhook que muda o plano pode chegar depois.
  const subscription = useSubscriptionQuery({ awaitingCheckout: result === 'success' });
  const checkout = useCheckoutMutation();
  const portal = usePortalMutation();
  const [error, setError] = useState<string | null>(null);

  // Uma chave por tier enquanto a página vive: repetir depois de uma falha é a mesma operação.
  const checkoutKeys = useRef(new Map<PaidTier, string>());

  async function startCheckout(tier: PaidTier) {
    setError(null);
    let idempotencyKey = checkoutKeys.current.get(tier);
    if (idempotencyKey === undefined) {
      idempotencyKey = crypto.randomUUID();
      checkoutKeys.current.set(tier, idempotencyKey);
    }
    try {
      redirectToProvider(await checkout.mutateAsync({ tier, idempotencyKey }));
    } catch (failure) {
      setError(billingErrorMessage(failure));
    }
  }

  async function openPortal() {
    setError(null);
    try {
      redirectToProvider(await portal.mutateAsync(crypto.randomUUID()));
    } catch (failure) {
      setError(billingErrorMessage(failure));
    }
  }

  return (
    <section className="space-y-6">
      <PageHeader title="Subscrição" description="O teu plano e o limite de clientes." />

      {result === 'success' && (
        <Notice icon={CheckCircle2} onDismiss={() => void setResult(null)}>
          Pagamento recebido. A confirmação do plano pode demorar alguns segundos.
        </Notice>
      )}
      {result === 'cancelled' && (
        <Notice icon={Info} onDismiss={() => void setResult(null)}>
          Pagamento cancelado. O teu plano não mudou.
        </Notice>
      )}
      {error !== null && (
        <p role="alert" className="text-destructive text-sm">
          {error}
        </p>
      )}

      {subscription.isPending ? (
        <Skeleton role="status" aria-label="A carregar subscrição…" className="h-48 w-full" />
      ) : subscription.isError ? (
        <ErrorState error={subscription.error} onRetry={() => void subscription.refetch()} />
      ) : (
        <>
          <CurrentPlan
            tier={subscription.data.tier}
            status={subscription.data.status}
            clientLimit={subscription.data.client_limit}
            clientCount={subscription.data.current_client_count}
            trialEndsAt={subscription.data.trial_ends_at}
            canManage={subscription.data.tier !== 'FREE'}
            portalPending={portal.isPending}
            onManage={() => void openPortal()}
          />
          {canCheckout(subscription.data) && (
            <section aria-labelledby="plans-title" className="space-y-3">
              <h2 id="plans-title" className="font-display text-xl">
                Mudar de plano
              </h2>
              <ul className="grid gap-4 sm:grid-cols-2">
                {PAID_PLANS.map((plan) => (
                  <li key={plan.tier} className="border-border space-y-3 rounded-xl border p-5">
                    <div>
                      <h3 className="font-display text-lg">{plan.label}</h3>
                      <p className="text-muted-foreground text-sm">{plan.clients}</p>
                    </div>
                    <Button
                      disabled={checkout.isPending}
                      onClick={() => void startCheckout(plan.tier)}
                    >
                      {checkout.isPending && checkout.variables.tier === plan.tier
                        ? 'A abrir pagamento…'
                        : `Escolher ${plan.label}`}
                    </Button>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </>
      )}
    </section>
  );
}

function CurrentPlan({
  tier,
  status,
  clientLimit,
  clientCount,
  trialEndsAt,
  canManage,
  portalPending,
  onManage,
}: {
  tier: string;
  status: string;
  clientLimit: number | null;
  clientCount: number;
  trialEndsAt: string | null;
  canManage: boolean;
  portalPending: boolean;
  onManage: () => void;
}) {
  const percentage =
    clientLimit === null
      ? 0
      : Math.min(100, Math.round((clientCount / Math.max(clientLimit, 1)) * 100));
  const trialActive = trialEndsAt !== null && new Date(trialEndsAt) > new Date();

  return (
    <section aria-labelledby="plan-title" className="border-border space-y-4 rounded-xl border p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-muted-foreground text-sm">Plano atual</p>
          <h2 id="plan-title" className="font-display text-2xl">
            {tierLabel(tier)}
          </h2>
        </div>
        <Badge variant={hasPaymentIssue(status) ? 'destructive' : 'secondary'}>
          {STATUS_LABELS[status] ?? status}
        </Badge>
      </div>
      <div className="space-y-2">
        <p className="tabular text-sm">
          {clientLimit === null
            ? `${formatNumber(clientCount)} clientes · ilimitado`
            : `${formatNumber(clientCount)} de ${formatNumber(clientLimit)} clientes`}
        </p>
        {clientLimit !== null && (
          <Progress value={percentage} aria-label="Clientes usados" className="h-2" />
        )}
      </div>
      {trialActive && (
        <p className="text-muted-foreground text-sm">
          Período experimental até {formatDate(trialEndsAt)}.
        </p>
      )}
      {hasPaymentIssue(status) && (
        <p className="text-warning text-sm font-medium">
          A subscrição não está ativa: não podes adicionar clientes novos.
        </p>
      )}
      {canManage && (
        <Button variant="outline" disabled={portalPending} onClick={onManage}>
          {portalPending ? 'A abrir portal…' : 'Gerir subscrição'}
        </Button>
      )}
    </section>
  );
}

function Notice({
  icon: Icon,
  onDismiss,
  children,
}: {
  icon: typeof Info;
  onDismiss: () => void;
  children: string;
}) {
  return (
    <div
      role="status"
      className="border-border bg-muted flex items-start gap-3 rounded-xl border p-4"
    >
      <Icon aria-hidden className="mt-0.5 size-4 shrink-0" />
      <p className="flex-1 text-sm">{children}</p>
      <Button size="sm" variant="ghost" onClick={onDismiss}>
        Fechar
      </Button>
    </div>
  );
}
