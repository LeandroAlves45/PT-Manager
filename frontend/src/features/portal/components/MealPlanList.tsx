import { Check, Loader2 } from 'lucide-react';

import { useSelectMealPlanMutation } from '@/features/portal/api/portal';
import { PortalCard } from '@/features/portal/components/PortalCard';
import {
  formatKcal,
  goalLabel,
  mealCountLabel,
  planPeriodLabel,
} from '@/features/portal/lib/nutrition';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';

type MealPlanSummary = components['schemas']['MyMealPlanSummaryResponse'];

/**
 * Mensagem para uma escolha que falhou. O plano pode ter sido arquivado ou desativado pelo
 * personal trainer entretanto; a lista volta a ser lida e deixa de o mostrar.
 */
function selectionFailureMessage(error: unknown): string {
  if (isApiProblem(error) && error.code === 'portal_meal_plan_not_found')
    return 'Este plano já não está disponível. A lista foi atualizada.';
  return 'Não foi possível guardar a escolha. Tenta novamente.';
}

/**
 * Lista dos planos alimentares ativos, para o cliente escolher qual seguir.
 *
 * Cada plano mostra nome, palavra do objetivo, kcal, número de refeições e datas; o que está a
 * ser seguido leva a marca "A seguir". A escolha fica gravada na conta. A app nunca escolhe
 * sozinha, nem quando só há um plano. Não mostra fórmula, idade, sexo nem ajuste.
 *
 * @param plans Planos ativos, com o escolhido em `is_selected`.
 * @param onSelected Chamado depois de a escolha ficar gravada (por exemplo, para navegar).
 */
export function MealPlanList({
  plans,
  onSelected,
}: {
  plans: MealPlanSummary[];
  onSelected?: () => void;
}) {
  const select = useSelectMealPlanMutation();

  return (
    <div className="space-y-3">
      {select.isError && (
        <p
          role="alert"
          className="border-border bg-card rounded-lg border p-3 text-sm wrap-break-word"
        >
          {selectionFailureMessage(select.error)}
        </p>
      )}
      <ul className="grid grid-cols-1 gap-4 md:grid-cols-2">
        {plans.map((plan) => {
          const goal = goalLabel(plan.goal_type);
          const saving = select.isPending && select.variables === plan.id;
          const titleId = `meal-plan-${plan.id}`;
          return (
            <li key={plan.id} className="flex min-w-0 flex-col">
              <PortalCard tone="nutrition" className="flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <h2
                    id={titleId}
                    className="font-display min-w-0 pe-1 text-xl leading-tight wrap-break-word"
                  >
                    {plan.name}
                  </h2>
                  {plan.is_selected && (
                    <Badge>
                      <Check aria-hidden />A seguir
                    </Badge>
                  )}
                  {goal !== null && <Badge variant="secondary">{goal}</Badge>}
                </div>
                <p className="text-lg font-semibold tabular-nums">{formatKcal(plan.target_kcal)}</p>
                <p className="text-muted-foreground text-sm">
                  {mealCountLabel(plan.meal_count)} ·{' '}
                  {planPeriodLabel(plan.starts_date, plan.ends_date)}
                </p>
                {!plan.is_selected && (
                  <Button
                    type="button"
                    aria-describedby={titleId}
                    disabled={select.isPending}
                    className="mt-2 min-h-11 w-full"
                    onClick={() =>
                      select.mutate(plan.id, {
                        onSuccess: () => onSelected?.(),
                      })
                    }
                  >
                    {saving ? (
                      <>
                        <Loader2 aria-hidden className="animate-spin" />A guardar…
                      </>
                    ) : (
                      'Escolher este plano'
                    )}
                  </Button>
                )}
              </PortalCard>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
