import { ArrowLeft, UtensilsCrossed } from 'lucide-react';
import { Link, useParams, useSearchParams } from 'react-router';

import { useMealPlanDetailQuery } from '@/features/prescriptions/api/meal-plans';
import {
  MealTotalsLine,
  NutritionBalance,
  TargetsSummary,
} from '@/features/prescriptions/components/NutritionBalance';
import { mealPlanBackPath } from '@/features/prescriptions/lib/meal-plan-draft';
import {
  targetsFromResult,
  totalsFromResponse,
  unitLabel,
} from '@/features/prescriptions/lib/nutrition';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate } from '@/shared/lib/format';

/** Gramas ou quantidade com até 2 casas, em PT-PT. */
function formatQuantity(value: number): string {
  return new Intl.NumberFormat('pt-PT', { maximumFractionDigits: 2 }).format(value);
}

/**
 * Página de um plano alimentar (`meal-plans/:planId`): metas, o que as refeições somam, o
 * que falta ou excede, e cada refeição com os seus totais. «Editar metas» abre o passo 1 do
 * editor e «Editar refeições» o passo 2. `?client_id=` (vindo da ficha do cliente) mantém-se
 * nos links e define o regresso.
 */
export function MealPlanViewPage() {
  const { planId = '' } = useParams();
  const [search] = useSearchParams();
  const clientId = search.get('client_id');
  const suffix = clientId === null ? '' : `?client_id=${encodeURIComponent(clientId)}`;
  const plan = useMealPlanDetailQuery(planId);

  const back = (
    <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
      <Link to={mealPlanBackPath(clientId)}>
        <ArrowLeft aria-hidden />{' '}
        {clientId === null ? 'Voltar a planos alimentares' : 'Voltar à ficha do cliente'}
      </Link>
    </Button>
  );

  if (plan.isPending) {
    return (
      <section className="space-y-6">
        {back}
        <Skeleton role="status" aria-label="A carregar plano alimentar…" className="h-80 w-full" />
      </section>
    );
  }
  if (plan.isError) {
    return (
      <section className="space-y-6">
        {back}
        {isApiProblem(plan.error) && plan.error.status === 404 ? (
          <EmptyState
            icon={UtensilsCrossed}
            title="Plano alimentar não encontrado"
            description="Este plano já não existe."
          />
        ) : (
          <ErrorState error={plan.error} onRetry={() => void plan.refetch()} />
        )}
      </section>
    );
  }

  const data = plan.data;
  const targets = targetsFromResult(data.calculation);
  const period =
    data.ends_date === null
      ? `Desde ${formatDate(data.starts_date)}`
      : `${formatDate(data.starts_date)} a ${formatDate(data.ends_date)}`;

  return (
    <section className="space-y-6">
      {back}
      <PageHeader
        title={data.name}
        description={data.description ?? period}
        action={
          data.is_archived ? undefined : (
            <div className="flex flex-wrap gap-2">
              <Button asChild variant="outline" className="min-h-11 md:min-h-8">
                <Link to={`/trainer/meal-plans/${data.id}/edit${suffix}`}>Editar metas</Link>
              </Button>
              <Button asChild className="min-h-11 md:min-h-8">
                <Link to={`/trainer/meal-plans/${data.id}/edit/meals${suffix}`}>
                  Editar refeições
                </Link>
              </Button>
            </div>
          )
        }
      />
      <p className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
        {data.description !== null && <span>{period}</span>}
        {data.is_archived && <Badge variant="secondary">Arquivado</Badge>}
      </p>

      <div className="grid gap-4 lg:grid-cols-2">
        <TargetsSummary targets={targets} />
        <NutritionBalance targets={targets} totals={totalsFromResponse(data.actual_totals)} />
      </div>

      <section className="space-y-4">
        <h2 className="font-display text-xl">Refeições</h2>
        {data.meals.length === 0 ? (
          <p className="text-muted-foreground border-border rounded-xl border border-dashed p-6 text-center text-sm">
            Este plano ainda não tem refeições.
          </p>
        ) : (
          data.meals.map((meal) => (
            <article
              key={meal.id}
              aria-label={meal.meal_type}
              className="border-border bg-card space-y-3 rounded-xl border p-4"
            >
              <div className="space-y-1">
                <h3 className="font-medium">{meal.meal_type}</h3>
                <MealTotalsLine totals={totalsFromResponse(meal.totals)} />
              </div>
              {meal.items.length > 0 && (
                <ul className="divide-border divide-y text-sm">
                  {meal.items.map((item) => (
                    <li key={item.id} className="flex flex-wrap justify-between gap-x-4 py-1.5">
                      <span>
                        {item.food_name}{' '}
                        <span className="text-muted-foreground">
                          {formatQuantity(item.quantity_in_grams)} g
                        </span>
                      </span>
                      <span className="text-primary tabular-nums">
                        {formatQuantity(item.contribution.kcal)} kcal
                      </span>
                    </li>
                  ))}
                </ul>
              )}
              {meal.supplements.length > 0 && (
                <div className="space-y-1">
                  <h4 className="text-muted-foreground text-xs">Suplementos nesta refeição</h4>
                  <ul className="divide-border divide-y text-sm">
                    {meal.supplements.map((item) => (
                      <li key={item.id} className="py-1.5">
                        {item.supplement_name}{' '}
                        <span className="text-muted-foreground">
                          {formatQuantity(item.quantity)}
                          {unitLabel(item.unit_of_measure) === null
                            ? ''
                            : ` ${unitLabel(item.unit_of_measure)}`}
                          {item.notes !== null && item.notes !== '' ? ` · ${item.notes}` : ''}
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </article>
          ))
        )}
      </section>
    </section>
  );
}
