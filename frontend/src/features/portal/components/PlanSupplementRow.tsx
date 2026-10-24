import { Check, CheckCircle2, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { useTogglePlanIntakeMutation } from '@/features/portal/api/portal';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { mealSupplementDose } from '@/features/portal/lib/nutrition';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';

type PlanItem = components['schemas']['MyPlanSupplementItemResponse'];

/**
 * Códigos de `MarkMyPlanSupplementIntakeHandler` e `UnmarkMyPlanSupplementIntakeHandler`:
 * chegam no `title` do ProblemDetails (os pedidos não têm corpo para validar).
 */
const PLAN_INTAKE_ERROR_MESSAGES: Record<string, string> = {
  portal_plan_supplement_not_found: 'Este suplemento já não faz parte do teu plano alimentar.',
  portal_plan_supplement_unavailable:
    'Este suplemento já não está disponível. Confirma com o teu personal trainer.',
  portal_nutrition_plan_not_selected: 'Escolhe primeiro o teu plano alimentar.',
};

/** Mensagem PT para a falha de marcar ou desmarcar uma toma do plano. */
function planIntakeFailureMessage(failure: unknown): string {
  const code = isApiProblem(failure) ? failure.code : null;

  return (
    (code !== null && Object.hasOwn(PLAN_INTAKE_ERROR_MESSAGES, code)
      ? PLAN_INTAKE_ERROR_MESSAGES[code]
      : undefined) ?? 'Não foi possível guardar a toma do suplemento. Tenta novamente.'
  );
}

/**
 * Um suplemento do plano alimentar com a toma de hoje.
 *
 * Mostra onde se toma (a refeição ou «Fora das refeições»), a dose e o botão de estado (44 px).
 * Um item cujo suplemento deixou de estar ativo aparece como «Indisponível» e não se marca;
 * a toma de hoje, se já estava marcada, ainda se desmarca.
 *
 * @param item Item da resposta de `GET /portal/my-plan-supplements/today`.
 */
export function PlanSupplementRow({ item }: { item: PlanItem }) {
  const toggle = useTogglePlanIntakeMutation();
  const taken = item.taken_at !== null;
  const blocked = item.is_unavailable && !taken;
  const where = item.placement === 'meal' ? (item.meal_type ?? 'Refeição') : 'Fora das refeições';

  return (
    <li>
      <PortalCard tone="supplements" className="gap-2 p-3">
        <div className="flex items-start gap-3">
          <div className="min-w-0 flex-1 space-y-0.5">
            <p className="flex flex-wrap items-center gap-2">
              <span className="min-w-0 font-medium wrap-break-word">{item.supplement_name}</span>
              {item.is_unavailable && <Badge variant="secondary">Indisponível</Badge>}
            </p>
            <p className="text-muted-foreground text-sm wrap-break-word">
              {mealSupplementDose(item)} · {where}
            </p>
            <p
              className={
                taken
                  ? 'text-section-supplements flex items-center gap-1 text-sm font-medium'
                  : 'text-muted-foreground flex items-center gap-1 text-sm'
              }
            >
              {taken ? <CheckCircle2 aria-hidden className="size-4" /> : null}
              {taken ? 'Tomado' : 'Por tomar'}
            </p>
          </div>
          <Button
            type="button"
            size="icon"
            variant={taken ? 'default' : 'outline'}
            className="size-11 shrink-0"
            aria-label={
              taken
                ? `Desmarcar toma de ${item.supplement_name} (${where})`
                : `Marcar toma de ${item.supplement_name} (${where})`
            }
            aria-pressed={taken}
            disabled={toggle.isPending || blocked}
            onClick={() =>
              toggle.mutate(
                { itemId: item.id, taken: !taken },
                { onError: (failure) => toast.error(planIntakeFailureMessage(failure)) }
              )
            }
          >
            {toggle.isPending ? (
              <Loader2 aria-hidden className="animate-spin" />
            ) : (
              <Check aria-hidden />
            )}
          </Button>
        </div>

        {item.is_unavailable && (
          <p className="text-muted-foreground text-sm">
            Este suplemento já não está disponível. Confirma com o teu personal trainer o que fazer.
          </p>
        )}
        {item.notes !== null && <p className="text-sm wrap-break-word">{item.notes}</p>}
      </PortalCard>
    </li>
  );
}
