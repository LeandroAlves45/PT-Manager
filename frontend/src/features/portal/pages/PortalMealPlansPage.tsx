import { UtensilsCrossed } from 'lucide-react';
import { Link, useNavigate } from 'react-router';

import { usePortalMealPlansQuery } from '@/features/portal/api/portal';
import { MealPlanList } from '@/features/portal/components/MealPlanList';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Planos alimentares ativos (`/portal/nutrition/planos`): o cliente vê todos e escolhe qual
 * seguir. Escolher grava na conta e volta ao plano em `/portal/nutrition`.
 */
export function PortalMealPlansPage() {
  const query = usePortalMealPlansQuery();
  const navigate = useNavigate();

  if (query.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-56" />
        <div
          role="status"
          aria-label="A carregar os planos alimentares…"
          className="grid grid-cols-1 gap-4 md:grid-cols-2"
        >
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      </section>
    );

  if (query.data === undefined)
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;

  const plans = query.data.items;

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        <h1 className="font-display pe-1 text-3xl leading-none">Planos alimentares</h1>
        <p className="text-muted-foreground text-sm">
          Escolhe o plano que queres seguir. Podes trocar quando quiseres.
        </p>
      </header>

      {plans.length === 0 ? (
        <EmptyState
          icon={UtensilsCrossed}
          title="Sem plano alimentar"
          description="O teu personal trainer ainda não te atribuiu um plano alimentar."
        />
      ) : (
        <MealPlanList plans={plans} onSelected={() => void navigate('/portal/nutrition')} />
      )}

      <Link
        to="/portal/nutrition"
        className="text-primary inline-flex min-h-11 items-center text-sm font-medium underline underline-offset-4"
      >
        Voltar à nutrição
      </Link>
    </section>
  );
}
