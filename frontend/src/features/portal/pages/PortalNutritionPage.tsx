import { UtensilsCrossed } from 'lucide-react';

import { usePortalNutritionQuery } from '@/features/portal/api/portal';
import { MealCard } from '@/features/portal/components/MealCard';
import { formatGrams, formatKcal } from '@/features/portal/lib/nutrition';
import type { components } from '@/shared/api/schema';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate } from '@/shared/lib/format';

type NutritionPlan = components['schemas']['MyNutritionPlanResponse'];

/**
 * Plano alimentar do cliente (`/portal/nutrition`), só de leitura.
 *
 * Um `GET /portal/my-nutrition` alimenta o ecrã todo. O 404
 * `portal_nutrition_plan_not_available` é um estado vazio, não um erro: o backend responde o
 * mesmo sem plano ativo e com a ficha arquivada, por isso a mensagem não distingue os dois.
 */
export function PortalNutritionPage() {
  const query = usePortalNutritionQuery();

  if (query.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <div role="status" aria-label="A carregar o plano alimentar…" className="space-y-3">
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      </section>
    );

  if (query.isError) {
    if (isApiProblem(query.error) && query.error.code === 'portal_nutrition_plan_not_available')
      return (
        <EmptyState
          icon={UtensilsCrossed}
          title="Sem plano alimentar"
          description="O teu personal trainer ainda não te atribuiu um plano alimentar."
        />
      );

    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  }

  const plan = query.data;

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">
          {plan.ends_date === null
            ? `Desde ${formatDate(plan.starts_date)}`
            : `${formatDate(plan.starts_date)} a ${formatDate(plan.ends_date)}`}
        </p>
        <h1 className="font-display text-3xl leading-none">Nutrição</h1>
        <p className="text-muted-foreground text-sm wrap-break-word">{plan.name}</p>
        {plan.description !== null && <p className="text-sm wrap-break-word">{plan.description}</p>}
      </header>

      <TargetsTable plan={plan} />

      {plan.meals.length === 0 ? (
        <p className="text-sm">O plano ainda não tem refeições.</p>
      ) : (
        <div className="space-y-3">
          {plan.meals.map((meal) => (
            <MealCard key={meal.order_number} meal={meal} />
          ))}
        </div>
      )}
    </section>
  );
}

/**
 * Objetivo diário ao lado do que o plano dá
 *
 * `actual_totals` é a soma dos alimentos prescritos, não o que o cliente comeu: o backend não
 * regista refeições. Por isso a coluna chama-se "Este plano dá". A fibra não tem objetivo e só
 * aparece quando o plano a tem.
 */
function TargetsTable({ plan }: { plan: NutritionPlan }) {
  const actual = plan.actual_totals;
  const rows = [
    {
      label: 'Energia',
      target: formatKcal(plan.target_kcal),
      actual: formatKcal(actual.kcal),
    },
    {
      label: 'Proteína',
      target: formatGrams(plan.protein_target_grams),
      actual: formatGrams(actual.protein_grams),
    },
    {
      label: 'Hidratos',
      target: formatGrams(plan.carbs_target_grams),
      actual: formatGrams(actual.carbs_grams),
    },
    {
      label: 'Gordura',
      target: formatGrams(plan.fats_target_grams),
      actual: formatGrams(actual.fats_grams),
    },
    ...(actual.fiber_grams > 0
      ? [{ label: 'Fibra', target: '—', actual: formatGrams(actual.fiber_grams) }]
      : []),
  ];

  return (
    <div className="border-border bg-card rounded-lg border p-3">
      <table className="w-full text-sm tabular-nums">
        <caption className="sr-only">Objetivo diário e o que este plano dá</caption>
        <thead>
          <tr className="text-muted-foreground font-mono text-xs">
            <th scope="col" className="pb-2 text-left font-normal">
              <span className="sr-only">Nutriente</span>
            </th>
            <th scope="col" className="pb-2 text-right font-normal">
              Objetivo
            </th>
            <th scope="col" className="pb-2 text-right font-normal">
              Este plano dá
            </th>
          </tr>
        </thead>
        <tbody className="divide-border divide-y">
          {rows.map((row) => (
            <tr key={row.label}>
              <th scope="row" className="py-2 text-left font-medium">
                {row.label}
              </th>
              <td className="py-2 text-right">{row.target}</td>
              <td className="py-2 text-right">{row.actual}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
