import { MacroChips } from '@/features/portal/components/MacroChips';
import { PortalCard } from '@/features/portal/components/PortalCard';
import {
  foodDisplayName,
  formatGrams,
  formatKcal,
  mealSupplementDose,
} from '@/features/portal/lib/nutrition';
import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';

type Meal = components['schemas']['MyMealResponse'];

/**
 * Uma refeição do plano alimentar, só de leitura.
 *
 * Cada alimento mostra gramas e kcal; proteína, hidratos e gordura aparecem em chips só no
 * total da refeição. `meal_type` é texto livre do personal trainer e mostra-se tal como veio.
 * Os suplementos da refeição são informação do plano, para ler.
 */
export function MealCard({ meal }: { meal: Meal }) {
  return (
    <article aria-label={meal.meal_type} className="flex min-w-0 flex-col">
      <PortalCard tone="nutrition" className="flex-1">
        <header className="space-y-2">
          <div className="flex items-baseline justify-between gap-3">
            <h2 className="font-display min-w-0 pe-1 text-xl leading-tight wrap-break-word">
              {meal.meal_type}
            </h2>
            <p className="shrink-0 text-lg font-semibold tabular-nums">
              {formatKcal(meal.totals.kcal)}
            </p>
          </div>
          <MacroChips totals={meal.totals} />
        </header>

        {meal.items.length === 0 ? (
          <p className="text-muted-foreground text-sm">Esta refeição ainda não tem alimentos.</p>
        ) : (
          <ul className="divide-border divide-y">
            {meal.items.map((item, index) => (
              <li
                key={`${item.order_number}-${index}`}
                className="flex justify-between gap-3 py-2 text-sm"
              >
                <span className="min-w-0 wrap-break-word">{foodDisplayName(item)}</span>
                <span className="text-muted-foreground shrink-0 tabular-nums">
                  {formatGrams(item.quantity_in_grams)} · {formatKcal(item.contribution.kcal)}
                </span>
              </li>
            ))}
          </ul>
        )}

        {meal.supplements.length > 0 && (
          <div className="space-y-1 pt-2">
            <h3 className="text-sm font-medium">Suplementos</h3>
            <ul className="space-y-1 text-sm">
              {meal.supplements.map((supplement, index) => (
                <li key={`${supplement.order_number}-${index}`} className="space-y-0.5">
                  <p className="flex flex-wrap items-center gap-2">
                    <span className="min-w-0 wrap-break-word">
                      {supplement.supplement_name} · {mealSupplementDose(supplement)}
                    </span>
                    {supplement.is_unavailable && <Badge variant="secondary">Indisponível</Badge>}
                  </p>
                  {supplement.is_unavailable && (
                    <p className="text-muted-foreground">
                      Este suplemento já não está disponível. Confirma com o teu personal trainer o
                      que fazer.
                    </p>
                  )}
                  {supplement.notes !== null && (
                    <p className="text-muted-foreground">{supplement.notes}</p>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}
      </PortalCard>
    </article>
  );
}
