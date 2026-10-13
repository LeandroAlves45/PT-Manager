import {
  foodDisplayName,
  formatGrams,
  formatKcal,
  macrosLabel,
  mealSupplementDose,
} from '@/features/portal/lib/nutrition';
import { Link } from 'react-router';

import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';

type Meal = components['schemas']['MyMealResponse'];

/**
 * Uma refeição do plano alimentar, só de leitura.
 *
 * Cada alimento mostra gramas e kcal; proteína, hidratos e gordura aparecem só
 * no total da refeição. `meal_type` é texto livre do personal trainer e mostra-se tal como
 * veio. Os suplementos da refeição são informação do plano: a toma regista-se no ecrã
 * Suplementos.
 */
export function MealCard({ meal }: { meal: Meal }) {
  return (
    <article
      aria-label={meal.meal_type}
      className="border-border bg-card space-y-3 rounded-lg border p-3"
    >
      <header className="space-y-0.5">
        <h2 className="font-display text-xl leading-tight wrap-break-word">{meal.meal_type}</h2>
        <p className="text-sm tabular-nums">{formatKcal(meal.totals.kcal)}</p>
        <p className="text-muted-foreground text-sm tabular-nums">{macrosLabel(meal.totals)}</p>
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
        <div className="space-y-1">
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
                    Este suplemento já não está disponível. Fala com o teu personal trainer.
                  </p>
                )}
                {supplement.notes !== null && (
                  <p className="text-muted-foreground">{supplement.notes}</p>
                )}
              </li>
            ))}
          </ul>
          <Link
            to="/portal/supplements"
            className="text-primary inline-flex min-h-11 items-center text-sm font-medium underline underline-offset-4"
          >
            Registar tomas
          </Link>
        </div>
      )}
    </article>
  );
}
