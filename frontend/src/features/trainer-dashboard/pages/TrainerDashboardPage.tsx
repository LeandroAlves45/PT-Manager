import { format, parseISO } from 'date-fns';
import { pt } from 'date-fns/locale';
import { AlertTriangle, CalendarRange, ClipboardCheck, Plus, UserPlus, Users } from 'lucide-react';
import { Link } from 'react-router';

import { useTrainerDashboardQuery } from '@/features/trainer-dashboard/api/dashboard';
import { BentoCard } from '@/features/trainer-dashboard/components/BentoCard';
import { PackSalesBlock } from '@/features/trainer-dashboard/components/PackSalesBlock';
import { SessionsTodayBlock } from '@/features/trainer-dashboard/components/SessionsTodayBlock';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate, formatNumber } from '@/shared/lib/format';

/** Rota de criação de cliente: a página de clientes abre o formulário com '?new=true'. */
const NEW_CLIENT_ROUTE = '/trainer/clients?new=true';

/** "Terça, 16/09/2026" a partir do local do trainer devolvido pela API. */
function eyebrowDate(localToday: string): string {
  const text = format(parseISO(localToday), 'EEEE, dd/MM/yyyy', { locale: pt });
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** Larguras dos seis blocos no bento de 12 colunas, pela ordem de apresentação. */
const SKELETON_SPANS = [
  'lg:col-span-4',
  'lg:col-span-5',
  'lg:col-span-3',
  'lg:col-span-5',
  'lg:col-span-4',
  'lg:col-span-3',
] as const;

/** Classe dos links-botão das ações dos blocos. */
const ACTION_CLASS =
  'border-border hover:bg-accent focus-visible:outline-ring inline-flex min-h-11 w-full items-center justify-center rounded-md border px-3 text-sm font-medium transition-colors focus-visible:outline-2 md:min-h-9';

/**
 * Painel do personal trainer: bento de alertas, cada um com uma só ação.
 *
 * Orçamento de pedidos: um `GET /api/v1/dashboard` (ver `useTrainerDashboardQuery`). As
 * ações navegam para os ecrãs das fatias seguintes; só "Registar presença" escreve daqui.
 */
export function TrainerDashboardPage() {
  const query = useTrainerDashboardQuery();

  const header = (localToday?: string) => (
    <header className="flex flex-wrap items-end justify-between gap-4">
      <div className="space-y-1">
        {localToday !== undefined && (
          <p className="text-muted-foreground font-mono text-xs">{eyebrowDate(localToday)}</p>
        )}
        <h1 className="font-display text-3xl leading-none sm:text-4xl">Bom treino</h1>
      </div>
      <Button asChild>
        <Link to={NEW_CLIENT_ROUTE}>
          <Plus aria-hidden /> Novo cliente
        </Link>
      </Button>
    </header>
  );

  if (query.isPending)
    return (
      <section className="space-y-6">
        {header()}
        <div aria-label="A carregar painel..." className="grid gap-4 lg:grid-cols-12">
          {/* Classes completas: o Tailwind não gera `lg:col-span-${n}` construído em runtime. */}
          {SKELETON_SPANS.map((span, index) => (
            <Skeleton key={index} className={`h-48 ${span}`} />
          ))}
        </div>
      </section>
    );

  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;

  const data = query.data;

  if (data.active_client_count === 0)
    return (
      <section className="space-y-6">
        {header(data.local_today)}
        <EmptyState
          icon={UserPlus}
          title="Ainda não tens clientes"
          description="Cria a ficha do primeiro cliente. Depois podes convidá-lo para o portal."
          action={
            <Button asChild>
              <Link to={NEW_CLIENT_ROUTE}>Criar primeiro cliente</Link>
            </Button>
          }
        />
      </section>
    );

  const reviews = data.check_ins_pending_review;
  const oldest = [...reviews.items].sort((a, b) => a.responded_at.localeCompare(b.responded_at))[0];
  const plans = data.plans_expiring;

  return (
    <section className="space-y-6">
      {header(data.local_today)}
      <div className="grid gap-4 lg:grid-cols-12">
        <BentoCard
          title="Check-ins por rever"
          icon={ClipboardCheck}
          highlight
          className="lg:col-span-4"
          action={
            <Button asChild className="min-h-11 md:min-h-9">
              <Link to="/trainer/check-ins?status=unreviewed">Rever check-ins</Link>
            </Button>
          }
        >
          <div className="flex items-end gap-4">
            <p className="font-display text-6xl leading-none tabular-nums">
              {formatNumber(reviews.total_count)}
            </p>
            <div className="text-muted-foreground space-y-1 text-sm">
              {reviews.overdue_count > 0 && (
                <p className="flex items-center gap-1">
                  <AlertTriangle aria-hidden className="text-warning size-4" />
                  {formatNumber(reviews.overdue_count)} acima de 48 h de espera
                </p>
              )}
              {oldest !== undefined && (
                <p>
                  mais antigo: {oldest.client_name} ({formatDate(oldest.check_in_date)})
                </p>
              )}
              {reviews.total_count === 0 && <p>Tudo revisto.</p>}
            </div>
          </div>
        </BentoCard>

        <BentoCard
          title="Packs de sessões a terminar"
          icon={AlertTriangle}
          className="lg:col-span-5"
          action={
            <Link to="/trainer/sessions" className={ACTION_CLASS}>
              Renovar packs
            </Link>
          }
        >
          {data.packs_ending.items.length === 0 ? (
            <p className="text-muted-foreground text-sm">Nenhum pack perto do fim.</p>
          ) : (
            <ul className="divide-border divide-y">
              {data.packs_ending.items.map((pack) => (
                <li key={pack.pack_id} className="flex items-center justify-between gap-3 py-2">
                  <div className="min-w-0">
                    <Link
                      to={`/trainer/clients/${pack.client_id}`}
                      className="block truncate text-sm font-medium hover:underline"
                    >
                      {pack.client_name}
                    </Link>
                    <p className="text-muted-foreground truncate text-xs">
                      {pack.pack_name} · {pack.sessions_total} sessões
                      {pack.expected_end_date !== null &&
                        ` · ${formatDate(pack.expected_end_date)}`}
                    </p>
                  </div>
                  <span className="text-warning shrink-0 text-xs font-medium">
                    {pack.sessions_remaining}{' '}
                    {pack.sessions_remaining === 1 ? 'restante' : 'restantes'}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </BentoCard>

        <BentoCard
          title="Planos a expirar"
          icon={CalendarRange}
          className="lg:col-span-3"
          action={
            <Link to="/trainer/training-plans" className={ACTION_CLASS}>
              Prolongar
            </Link>
          }
        >
          <p className="font-display text-5xl leading-none tabular-nums">
            {formatNumber(plans.total_count)}
          </p>
          <p className="text-muted-foreground mt-2 text-sm">terminam nos próximos 7 dias</p>
          {plans.total_count > 0 && (
            <p className="text-muted-foreground text-xs">
              {plans.training_plan_count} de treino · {plans.meal_plan_count} alimentares
            </p>
          )}
        </BentoCard>

        <SessionsTodayBlock data={data.sessions_today} className="lg:col-span-5" />

        <BentoCard title="Clientes sem plano ativo" icon={Users} className="lg:col-span-4">
          {data.clients_without_training_plan.items.length === 0 ? (
            <p className="text-muted-foreground text-sm">Todos os clientes têm plano.</p>
          ) : (
            <ul className="divide-border divide-y">
              {data.clients_without_training_plan.items.map((client) => (
                <li key={client.client_id} className="flex items-center justify-between gap-3 py-2">
                  <div className="min-w-0">
                    <Link
                      to={`/trainer/clients/${client.client_id}`}
                      className="block truncate text-sm font-medium hover:underline"
                    >
                      {client.client_name}
                    </Link>
                    <p className="text-muted-foreground text-xs">
                      {client.has_had_plan
                        ? `sem plano há ${client.days_without_plan} dias`
                        : `nova · ${formatDate(client.without_plan_since)}`}
                    </p>
                  </div>
                  <Link
                    to={`/trainer/training-plans?client_id=${client.client_id}`}
                    className="text-primary shrink-0 text-xs font-medium hover:underline"
                    aria-label={`Atribuir plano a ${client.client_name}`}
                  >
                    Atribuir plano
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </BentoCard>

        <PackSalesBlock data={data.pack_sales} className="lg:col-span-3" />
      </div>
    </section>
  );
}
