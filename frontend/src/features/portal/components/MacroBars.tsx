import { Target } from 'lucide-react';

import { PortalCard } from '@/features/portal/components/PortalCard';
import { macroRows, type MacroKey } from '@/features/portal/lib/nutrition';
import type { components } from '@/shared/api/schema';
import { cn } from '@/shared/lib/utils';

type NutritionPlan = components['schemas']['MyNutritionPlanResponse'];

/**
 * Cores por métrica. A energia fica em tom neutro para não se confundir com os hidratos quando a
 * marca é laranja ou âmbar; as macros usam os tokens fixos, com contraste AA nos dois temas.
 */
const STYLES: Record<MacroKey, { readonly text: string; readonly bar: string }> = {
  kcal: { text: 'text-foreground', bar: 'bg-foreground' },
  protein: { text: 'text-macro-protein', bar: 'bg-macro-protein' },
  carbs: { text: 'text-macro-carbs', bar: 'bg-macro-carbs' },
  fats: { text: 'text-macro-fats', bar: 'bg-macro-fats' },
  fiber: { text: 'text-macro-fiber', bar: 'bg-macro-fiber' },
};

/**
 * Metas do dia ao lado do que o plano dá, uma barra por métrica.
 *
 * `actual_totals` é a soma dos alimentos prescritos, não o que o cliente comeu: o backend não
 * regista refeições. Os números estão sempre em texto, por isso a cor nunca é a única pista.
 * Quando a soma passa a meta a barra enche a 100 % e o número diz o resto, sem aviso nem
 * vermelho. A fibra não tem objetivo ("—") e, sem meta, não tem barra.
 *
 * @param plan Plano alimentar escolhido pelo cliente.
 */
export function MacroBars({ plan }: { plan: NutritionPlan }) {
  return (
    <PortalCard tone="nutrition" title="Metas do dia" icon={Target}>
      <ul className="space-y-3">
        {macroRows(plan).map((row) => {
          const style = STYLES[row.key];
          return (
            <li key={row.key} className="space-y-1.5">
              <div className="flex flex-wrap items-baseline justify-between gap-x-3 text-sm">
                <span className={cn('font-medium', style.text)}>{row.label}</span>
                <span className="tabular-nums">
                  <span className="text-muted-foreground">Este plano dá </span>
                  <span className="font-semibold">{row.actualText}</span>
                  <span className="text-muted-foreground">
                    {' · Objetivo '}
                    <span>{row.targetText}</span>
                  </span>
                </span>
              </div>
              {row.percent !== null && (
                <div
                  role="progressbar"
                  aria-label={`${row.label}: o que este plano dá face ao objetivo`}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-valuenow={row.percent}
                  aria-valuetext={`${row.actualText} de ${row.targetText}`}
                  className="bg-muted h-2 overflow-hidden rounded-full"
                >
                  <div
                    className={cn('h-full rounded-full', style.bar)}
                    style={{ width: `${row.percent}%` }}
                  />
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </PortalCard>
  );
}
