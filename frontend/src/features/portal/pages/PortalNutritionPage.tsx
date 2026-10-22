import { UtensilsCrossed } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';

import { usePortalMealPlansQuery, usePortalNutritionQuery } from '@/features/portal/api/portal';
import { MacroBars } from '@/features/portal/components/MacroBars';
import { MealCard } from '@/features/portal/components/MealCard';
import { MealPlanList } from '@/features/portal/components/MealPlanList';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { goalLabel, planPeriodLabel } from '@/features/portal/lib/nutrition';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Plano alimentar que o cliente escolheu seguir (`/portal/nutrition`), só de leitura.
 *
 * Um `GET /portal/my-nutrition` alimenta o plano. Três estados vêm do código do 404:
 * - `portal_nutrition_plan_not_selected`: há planos ativos mas nenhum escolhido; a lista para
 *   escolher aparece nesta página e a app nunca escolhe sozinha, nem com um só plano;
 * - `portal_nutrition_plan_not_available`: não há nenhum plano ativo, estado vazio e não erro
 *   (o backend responde o mesmo sem plano ativo e com a ficha arquivada);
 * - sem erro: o plano, com o link "Trocar de plano" quando há mais de um plano ativo.
 */
export function PortalNutritionPage() {
  const query = usePortalNutritionQuery();
  const plansQuery = usePortalMealPlansQuery();
  // Escolher na lista desta página troca a lista pelo plano: o foco vai para o título do plano,
  // para o leitor de ecrã e o teclado não ficarem num botão que deixou de existir.
  const [justChose, setJustChose] = useState(false);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const planId = query.data?.id;

  useEffect(() => {
    if (justChose && planId !== undefined) titleRef.current?.focus();
  }, [justChose, planId]);

  if (query.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <div
          role="status"
          aria-label="A carregar o plano alimentar…"
          className="grid grid-cols-1 gap-4 md:grid-cols-2"
        >
          <Skeleton className="h-40 md:col-span-2" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      </section>
    );

  // Vale mesmo com dados em cache: o plano deixou de estar ativo.
  if (isApiProblem(query.error) && query.error.code === 'portal_nutrition_plan_not_available')
    return (
      <section className="space-y-4">
        <h1 className="font-display pe-1 text-3xl leading-none">Nutrição</h1>
        <EmptyState
          icon={UtensilsCrossed}
          title="Sem plano alimentar"
          description="O teu personal trainer ainda não te atribuiu um plano alimentar."
        />
      </section>
    );

  // Também vale com dados em cache: o plano escolhido foi arquivado e a escolha caiu.
  if (isApiProblem(query.error) && query.error.code === 'portal_nutrition_plan_not_selected')
    return (
      <section className="space-y-4">
        <header className="space-y-1">
          <h1 className="font-display pe-1 text-3xl leading-none">Nutrição</h1>
          <p className="text-muted-foreground text-sm">
            Escolhe o plano alimentar que queres seguir. Podes trocar quando quiseres.
          </p>
        </header>
        <PlanChoice onSelected={() => setJustChose(true)} />
      </section>
    );

  // Sem dados nenhuns, a falha ocupa o ecrã; com dados em cache, fica só um aviso mais abaixo.
  if (query.data === undefined)
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;

  const plan = query.data;
  const goal = goalLabel(plan.goal_type);
  const canSwitch = (plansQuery.data?.items.length ?? 0) >= 2;

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">
          {planPeriodLabel(plan.starts_date, plan.ends_date)}
        </p>
        <h1
          ref={titleRef}
          tabIndex={-1}
          className="font-display pe-1 text-3xl leading-none outline-none"
        >
          Nutrição
        </h1>
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-muted-foreground min-w-0 text-sm wrap-break-word">{plan.name}</p>
          {goal !== null && <Badge variant="secondary">{goal}</Badge>}
        </div>
        {plan.description !== null && <p className="text-sm wrap-break-word">{plan.description}</p>}
        {canSwitch && (
          <Button asChild variant="outline" className="mt-2 min-h-11">
            <Link to="/portal/nutrition/planos">Trocar de plano</Link>
          </Button>
        )}
      </header>

      {query.isError && (
        <StaleDataNotice onRetry={() => void query.refetch()} retrying={query.isFetching} />
      )}

      <MacroBars plan={plan} />

      {plan.meals.length === 0 ? (
        <p className="text-sm">O plano ainda não tem refeições.</p>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {plan.meals.map((meal) => (
            <MealCard key={meal.order_number} meal={meal} />
          ))}
        </div>
      )}
    </section>
  );
}

/**
 * Lista para escolher o plano quando o cliente ainda não tem nenhum escolhido. Escolher grava
 * e o `GET /portal/my-nutrition`, invalidado pela gravação, passa a mostrar o plano.
 */
function PlanChoice({ onSelected }: { onSelected: () => void }) {
  const plansQuery = usePortalMealPlansQuery();

  if (plansQuery.isPending)
    return (
      <div
        role="status"
        aria-label="A carregar os planos alimentares…"
        className="grid grid-cols-1 gap-4 md:grid-cols-2"
      >
        <Skeleton className="h-40" />
        <Skeleton className="h-40" />
      </div>
    );

  if (plansQuery.data === undefined)
    return <ErrorState error={plansQuery.error} onRetry={() => void plansQuery.refetch()} />;

  if (plansQuery.data.items.length === 0)
    return (
      <EmptyState
        icon={UtensilsCrossed}
        title="Sem plano alimentar"
        description="O teu personal trainer ainda não te atribuiu um plano alimentar."
      />
    );

  return <MealPlanList plans={plansQuery.data.items} onSelected={onSelected} />;
}
