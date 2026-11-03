import { Target } from 'lucide-react';

import { PortalCard } from '@/features/portal/components/PortalCard';
import { dailyTargets, type MacroKey } from '@/features/portal/lib/nutrition';
import type { components } from '@/shared/api/schema';
import { cn } from '@/shared/lib/utils';

type NutritionPlan = components['schemas']['MyNutritionPlanResponse'];

/** Cor de cada macro: tokens fixos com contraste AA nos dois temas, iguais aos chips. */
const MACRO_TEXT: Record<MacroKey, string> = {
  protein: 'text-macro-protein',
  carbs: 'text-macro-carbs',
  fats: 'text-macro-fats',
};

/**
 * Metas do dia do plano alimentar: as kcal e, se o personal trainer as mostrar neste plano, a
 * proteína, os hidratos e a gordura.
 *
 * Só mostra o objetivo. Não há barras nem "quanto o plano já dá": essa conta é trabalho do
 * personal trainer. O nome de cada macro está sempre em texto, por isso a cor é só reforço.
 *
 * @param plan Plano alimentar escolhido pelo cliente.
 */
export function DailyTargets({ plan }: { plan: NutritionPlan }) {
  const targets = dailyTargets(plan);

  return (
    <PortalCard tone="nutrition" title="Metas do dia" icon={Target}>
      <div className="space-y-4">
        <p className="flex items-baseline gap-2">
          <span className="text-3xl font-semibold tabular-nums">{targets.kcalText}</span>
          <span className="text-muted-foreground text-sm">kcal por dia</span>
        </p>
        {targets.macros.length > 0 && (
          <dl className="grid grid-cols-3 gap-2">
            {targets.macros.map((macro) => (
              <div key={macro.key} className="bg-muted/40 min-w-0 rounded-lg border px-3 py-2">
                <dt className={cn('truncate text-xs font-medium', MACRO_TEXT[macro.key])}>
                  {macro.label}
                </dt>
                <dd className="text-base font-semibold tabular-nums">{macro.gramsText}</dd>
              </div>
            ))}
          </dl>
        )}
      </div>
    </PortalCard>
  );
}
