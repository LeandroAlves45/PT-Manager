import { MacroChips } from '@/features/portal/components/MacroChips';
import { MealSupplementList } from '@/features/portal/components/MealSupplementList';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { formatGrams, formatKcal } from '@/features/portal/lib/nutrition';
import type { components } from '@/shared/api/schema';

type Meal = components['schemas']['MyMealResponse'];

/**
 * Uma refeição do plano alimentar, só de leitura.
 *
 * Cada alimento mostra gramas e kcal; proteína, hidratos e gordura aparecem em chips só no
 * total da refeição. `meal_type` é texto livre do personal trainer e mostra-se tal como veio.
 * Os suplementos da refeição são informação do plano; as tomas registam-se na página de suplementos.
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
                <span className="min-w-0 wrap-break-word">{item.food_name}</span>
                <span className="text-muted-foreground shrink-0 tabular-nums">
                  {formatGrams(item.quantity_in_grams)} · {formatKcal(item.contribution.kcal)}
                </span>
              </li>
            ))}
          </ul>
        )}

        <MealSupplementList title="Suplementos" supplements={meal.supplements} />
      </PortalCard>
    </article>
  );
}
