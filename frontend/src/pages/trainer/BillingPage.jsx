/**
 * BillingPage.jsx — dashboard de subscrição do Personal Trainer.
 *
 * Mostra: estado actual, tier, contagem de clientes, trial countdown,
 * e botões para o Stripe Checkout e Billing Portal.
 */

import { useEffect, useState, useCallback } from 'react';
import { toast } from 'react-toastify';
import {
  CreditCard,
  ExternalLink,
  Users,
  Calendar,
  TrendingUp,
  Loader2,
} from 'lucide-react';
import {
  getSubscription,
  createCheckout,
  createBillingPortal,
} from '../../api/billingApi';
import { Button } from '../../components/ui/button';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '../../components/ui/card';
import { Badge } from '../../components/ui/badge';
import { Progress } from '../../components/ui/progress';

function getStatusLabel(status) {
  const labels = {
    trialing: 'Trial ativo',
    active: 'Ativo',
    past_due: 'Pagamento em atraso',
    cancelled: 'Cancelado',
    trial_expired: 'Trial expirado',
  };
  return labels[status] ?? status ?? '—';
}

function getStatusVariant(status) {
  if (status === 'active') return 'default';
  if (status === 'trialing') return 'secondary';
  return 'destructive';
}

// Calcula dias restantes até uma data ISO string
function daysUntil(dateStr) {
  if (!dateStr) return null;
  const diff = new Date(dateStr) - new Date();
  return Math.max(0, Math.ceil(diff / (1000 * 60 * 60 * 24)));
}

export default function BillingPage() {
  const [subscription, setSubscription] = useState(null);
  const [loading, setLoading] = useState(true);
  const [checkoutLoading, setCheckoutLoading] = useState(false);
  const [portalLoading, setPortalLoading] = useState(false);

  const fetchSubscription = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getSubscription();
      setSubscription(data);
    } catch {
      toast.error('Erro ao carregar estado da subscrição.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSubscription();
  }, [fetchSubscription]);

  async function handleCheckout() {
    setCheckoutLoading(true);
    try {
      const { checkout_url } = await createCheckout();
      // Redireciona para o Stripe Checkout hospedado
      window.location.href = checkout_url;
    } catch {
      toast.error('Erro ao iniciar checkout. Tente novamente.');
    } finally {
      setCheckoutLoading(false);
    }
  }

  async function handlePortal() {
    setPortalLoading(true);
    try {
      const { portal_url } = await createBillingPortal();
      // Redireciona para o Stripe Billing Portal hospedado
      window.location.href = portal_url;
    } catch {
      toast.error('Erro ao acessar o portal de billing. Tente novamente.');
    } finally {
      setPortalLoading(false);
    }
  }

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-64">
        <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
      </div>
    );
  }

  if (!subscription) return null;

  const trialDaysLeft = daysUntil(subscription.trial_end);
  const isTrialing = subscription.status === 'trialing';
  const isActive = subscription.status === 'active';
  const needsPayment = ['trial_expired', 'cancelled', 'past_due'].includes(
    subscription.status
  );

  // Trainer Isento: ativo no tier Pro sem custo mensal
  const isExempt =
    isActive && subscription.tier === 'pro' && subscription.monthly_eur === 0;

  // Progresso de clientes : quantos % do limite estão usados
  const clientProgress = subscription.max_clients
    ? Math.round(
        (subscription.active_clients_count / subscription.max_clients) * 100
      )
    : 0;

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Billing</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Gere a tua subscrição e método de pagamento
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Card: Estado da subscrição */}
        <Card className="border-border bg-card">
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">Subscrição</CardTitle>
              <Badge variant={getStatusVariant(subscription.status)}>
                {getStatusLabel(subscription.status)}
              </Badge>
            </div>
            <CardDescription>
              Plano {subscription.tier_label} — €{subscription.monthly_eur}/mês
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {/* Personal Trainer isento — mostra banner informativo em vez dos botões Stripe */}
            {isExempt && (
              <div className="rounded-lg bg-green-500/10 border border-green-500/20 p-3 flex items-start gap-3">
                <ShieldCheck className="h-5 w-5 text-green-500 mt-0.5 shrink-0" />
                <div>
                  <p className="text-sm font-medium text-green-500">
                    Acesso completo — conta isenta
                  </p>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Esta conta tem isenção permanente de faturação. Não é
                    necessário configurar método de pagamento.
                  </p>
                </div>
              </div>
            )}

            {/* Aviso de trial */}
            {isTrialing && trialDaysLeft !== null && (
              <div className="rounded-lg bg-primary/10 border border-primary/20 p-3">
                <p className="text-sm font-medium text-primary">
                  {trialDaysLeft > 0
                    ? `${trialDaysLeft} dia${trialDaysLeft !== 1 ? 's' : ''} restante${trialDaysLeft !== 1 ? 's' : ''} no trial`
                    : 'Trial termina hoje'}
                </p>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Adiciona um método de pagamento para não perderes o acesso.
                </p>
              </div>
            )}

            {/* Aviso de acesso bloqueado */}
            {needsPayment && (
              <div className="rounded-lg bg-destructive/10 border border-destructive/20 p-3">
                <p className="text-sm font-medium text-destructive">
                  Acesso limitado
                </p>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Renova a subscrição para recuperar o acesso completo.
                </p>
              </div>
            )}

            {/* Botões Stripe */}
            <div className="flex flex-col gap-2">
              {(isTrialing || needsPayment) && (
                <Button
                  onClick={handleCheckout}
                  disabled={checkoutLoading}
                  className="w-full"
                >
                  {checkoutLoading ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin mr-2" />A
                      redirecionar...
                    </>
                  ) : (
                    <>
                      <CreditCard className="h-4 w-4 mr-2" />
                      Adicionar método de pagamento
                    </>
                  )}
                </Button>
              )}

              {isActive && (
                <Button
                  variant="outline"
                  onClick={handlePortal}
                  disabled={portalLoading}
                  className="w-full"
                >
                  {portalLoading ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin mr-2" />A
                      redirecionar...
                    </>
                  ) : (
                    <>
                      <ExternalLink className="h-4 w-4 mr-2" />
                      Gerir subscrição no Stripe
                    </>
                  )}
                </Button>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Card: Utilização de clientes */}
        <Card className="border-border bg-card">
          <CardHeader>
            <CardTitle className="text-base">Clientes ativos</CardTitle>
            <CardDescription>
              {subscription.max_clients
                ? `Limite do plano ${subscription.tier_label}`
                : 'Sem limite (plano Pro)'}
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="flex items-end gap-2">
              <span className="text-3xl font-bold text-foreground">
                {subscription.active_clients_count}
              </span>
              {subscription.max_clients && (
                <span className="text-muted-foreground mb-1">
                  / {subscription.max_clients}
                </span>
              )}
            </div>

            {subscription.max_clients && (
              <Progress value={clientProgress} className="h-2" />
            )}

            {subscription.upgrade_message && (
              <p className="text-xs text-muted-foreground">
                {subscription.upgrade_message}
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Tabela de plano */}
      <Card className="border-border bg-card">
        <CardHeader>
          <CardTitle className="text-base">Planos disponíveis</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {[
              {
                name: 'Free',
                price: 0,
                clients: '5',
                current: subscription.tier === 'free',
              },
              {
                name: 'Starter',
                price: 20,
                clients: '49',
                current: subscription.tier === 'starter',
              },
              {
                name: 'Pro',
                price: 40,
                clients: 'Ilimitados',
                current: subscription.tier === 'pro',
              },
            ].map((plan) => (
              <div
                key={plan.name}
                className={`rounded-lg border p-4 ${plan.current ? 'border-primary bg-primary/5' : 'border-border'}`}
              >
                <div className="flex items-center justify-between mb-2">
                  <p className="text-2x1 font-normal text-muted-foreground">
                    {plan.name}
                  </p>
                  {plan.current && (
                    <Badge variant="default" className="text-xs">
                      Atual
                    </Badge>
                  )}
                </div>
                <p className="text-2xl font-bold text-foreground">
                  €{plan.price}
                  <span className="text-sm font-normal text-muted-foreground">
                    /mês
                  </span>
                </p>
                <p className="text-sm text-muted-foreground mt-1">
                  Até {plan.clients} clientes
                </p>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
