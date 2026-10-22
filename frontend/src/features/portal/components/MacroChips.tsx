import { formatGrams } from '@/features/portal/lib/nutrition';
import type { components } from '@/shared/api/schema';
import { cn } from '@/shared/lib/utils';

type Totals = components['schemas']['MyNutritionTotalsResponse'];

const CHIPS = [
  { letter: 'P', label: 'Proteína', color: 'border-macro-protein/50 text-macro-protein' },
  { letter: 'H', label: 'Hidratos', color: 'border-macro-carbs/50 text-macro-carbs' },
  { letter: 'G', label: 'Gordura', color: 'border-macro-fats/50 text-macro-fats' },
] as const;

/**
 * Proteína, hidratos e gordura de uma refeição em três chips com a cor de cada macro.
 *
 * A letra e as gramas estão em texto; o nome por extenso fica só para leitores de ecrã, por isso
 * a cor é um reforço e nunca a única pista.
 *
 * @param totals Totais da refeição.
 */
export function MacroChips({ totals }: { totals: Totals }) {
  const grams = [totals.protein_grams, totals.carbs_grams, totals.fats_grams];

  return (
    <ul className="flex flex-wrap gap-1.5">
      {CHIPS.map((chip, index) => (
        <li
          key={chip.letter}
          className={cn(
            'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium tabular-nums',
            chip.color
          )}
        >
          <span aria-hidden>{chip.letter}</span>
          <span className="sr-only">{chip.label}</span>
          <span>{formatGrams(grams[index]!)}</span>
        </li>
      ))}
    </ul>
  );
}
