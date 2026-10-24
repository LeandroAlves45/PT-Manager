import {
  Activity,
  Apple,
  Ban,
  Banknote,
  LockKeyhole,
  Dumbbell,
  Pill,
  ShieldCheck,
  ShieldOff,
  Syringe,
  Users,
} from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';

import { apiClient, unwrap } from '@/shared/api/client';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatCount, formatCurrency, formatNumber } from '@/shared/lib/format';
import { TrainerStatCard } from '@/features/admin-overview/components/TrainerStatCard';

/** Uma resposta agregada fornece todos os cartões, sem consultas aos catálogos. */
export function AdminOverviewPage() {
  const query = useQuery({
    queryKey: ['admin-overview'],
    queryFn: ({ signal }) => apiClient.GET('/api/v1/admin/overview', { signal }).then(unwrap),
  });

  if (query.isPending)
    return (
      <section className="space-y-5">
        <PageHeader title="Visão geral" />
        <div
          aria-label="A carregar visão geral..."
          className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3"
        >
          {Array.from({ length: 11 }, (_, index) => (
            <Skeleton key={index} className="h-28" />
          ))}
        </div>
      </section>
    );
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;

  const data = query.data;
  const cards = [
    {
      label: 'Alimentos globais',
      count: data.global_foods.total_count,
      detail: formatCount(data.global_foods.active_count, 'ativo', 'ativos'),
      href: '/admin/catalog/foods',
      icon: Apple,
    },
    {
      label: 'Exercícios globais',
      count: data.global_exercises.total_count,
      detail: formatCount(data.global_exercises.active_count, 'ativo', 'ativos'),
      href: '/admin/catalog/exercises',
      icon: Dumbbell,
    },
    {
      label: 'Suplementos globais',
      count: data.global_supplements.total_count,
      detail: formatCount(data.global_supplements.active_count, 'ativo', 'ativos'),
      href: '/admin/catalog/supplements',
      icon: Pill,
    },
    {
      label: 'Alimentos privados',
      count: data.private_foods.total_count,
      detail: formatCount(data.private_foods.blocked_count, 'bloqueado', 'bloqueados'),
      href: '/admin/moderation?kind=foods',
      icon: ShieldCheck,
    },
    {
      label: 'Exercícios privados',
      count: data.private_exercises.total_count,
      detail: formatCount(data.private_exercises.blocked_count, 'bloqueado', 'bloqueados'),
      href: '/admin/moderation?kind=exercises',
      icon: Activity,
    },
    {
      label: 'Suplementos privados',
      count: data.private_supplements.total_count,
      detail: formatCount(data.private_supplements.blocked_count, 'bloqueado', 'bloqueados'),
      href: '/admin/moderation?kind=supplements',
      icon: Syringe,
    },
  ];

  const trainers = data.trainers;

  return (
    <section className="space-y-8">
      <PageHeader
        title="Visão geral"
        description="Treinadores, faturação, catálogos e moderação da plataforma"
      />
      <section aria-labelledby="overview-trainers" className="space-y-3">
        <h2 id="overview-trainers" className="font-display text-xl">
          Treinadores e faturação
        </h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          <TrainerStatCard
            label="Subscrições ativas"
            icon={Users}
            value={formatCount(trainers.active_subscription_count, 'treinador', 'treinadores')}
            helper="Inclui o período experimental. Não inclui isentos."
          />
          <TrainerStatCard
            label="Suspensas por pagamento"
            icon={Ban}
            value={formatCount(trainers.suspended_subscription_count, 'treinador', 'treinadores')}
            attention={trainers.suspended_subscription_count > 0}
          />
          <TrainerStatCard
            label="Contas bloqueadas"
            icon={LockKeyhole}
            value={formatCount(
              trainers.blocked_account_count,
              'conta bloqueada',
              'contas bloqueadas'
            )}
          />
          <TrainerStatCard
            label="Isentos"
            icon={ShieldOff}
            value={formatCount(trainers.exempt_count, 'treinador isento', 'treinadores isentos')}
          />
          <TrainerStatCard
            label="Recebido este mês"
            icon={Banknote}
            value={formatCurrency(trainers.revenue_this_month_cents, 'EUR')}
            helper="Líquido, sem a comissão do Stripe. Mês de calendário em Lisboa."
          />
        </div>
      </section>
      <section aria-labelledby="overview-catalogs" className="space-y-3">
        <h2 id="overview-catalogs" className="font-display text-xl">
          Catálogos e moderação
        </h2>
        {cards.every((card) => card.count === 0) ? (
          <EmptyState
            icon={ShieldCheck}
            title="Plataforma sem conteúdo"
            description="Os catálogos e a fila de moderação ainda estão vazios."
          />
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {cards.map(({ label, count, detail, href, icon: Icon }) => (
              <Link
                key={label}
                to={href}
                className="border-border bg-card hover:bg-accent focus-visible:outline-ring rounded-xl border p-5 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                <div className="text-muted-foreground flex items-center gap-2 text-sm">
                  <Icon aria-hidden className="size-4" />
                  {label}
                </div>
                <p className="font-display mt-3 text-4xl tabular-nums">{formatNumber(count)}</p>
                <p className="text-muted-foreground mt-1 text-sm">{detail}</p>
              </Link>
            ))}
          </div>
        )}
      </section>
    </section>
  );
}
