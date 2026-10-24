import { formatDecimal } from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { formatDate, formatNumber } from '@/shared/lib/format';

/**
 * Regras de apresentação do plano alimentar do cliente.
 *
 * Funções puras. O backend envia os totais com duas casas decimais (`decimal` arredondado em
 * `MyNutritionPlanQueries`); num telemóvel mostram-se as kcal inteiras e os gramas com uma
 * casa no máximo.
 */

const GRAMS = new Intl.NumberFormat('pt-PT', { maximumFractionDigits: 1 });

/** Gramas com uma casa decimal no máximo: 80 -> "80 g" e 79.25 -> "79,3 g". */
export function formatGrams(value: number): string {
  return `${GRAMS.format(value)} g`;
}

/** Energia em kcal inteiras: 925.6 -> "926 kcal". */
export function formatKcal(value: number): string {
  return `${formatNumber(value)} kcal`;
}

/** Chaves das métricas mostradas nas barras e nos chips. */
export type MacroKey = 'kcal' | 'protein' | 'carbs' | 'fats' | 'fiber';

/** Uma métrica do plano: o que o plano dá, o objetivo e a percentagem da barra. */
export interface MacroRow {
  readonly key: MacroKey;
  readonly label: string;
  readonly actualText: string;
  /** "—" quando não há objetivo (a fibra). */
  readonly targetText: string;
  /** 0 a 100, com o excesso preso a 100; `null` quando não há objetivo, logo não há barra. */
  readonly percent: number | null;
}

type PlanTotals = Pick<
  components['schemas']['MyNutritionPlanResponse'],
  | 'target_kcal'
  | 'protein_target_grams'
  | 'carbs_target_grams'
  | 'fats_target_grams'
  | 'actual_totals'
>;

/** Percentagem da barra: o excesso enche-a a 100 e nunca passa daí. */
function barPercent(actual: number, target: number): number | null {
  if (target <= 0) return null;
  return Math.min(100, Math.max(0, Math.round((actual / target) * 100)));
}

/**
 * Linhas das barras de macros: energia, proteína, hidratos, gordura e, só quando o plano a tem,
 * fibra. `actual_totals` é a soma dos alimentos prescritos, não o que o cliente comeu. Passar a
 * meta não gera aviso: a barra fica cheia e o número diz o resto.
 */
export function macroRows(plan: PlanTotals): MacroRow[] {
  const actual = plan.actual_totals;
  const rows: MacroRow[] = [
    {
      key: 'kcal',
      label: 'Energia',
      actualText: formatKcal(actual.kcal),
      targetText: formatKcal(plan.target_kcal),
      percent: barPercent(actual.kcal, plan.target_kcal),
    },
    {
      key: 'protein',
      label: 'Proteína',
      actualText: formatGrams(actual.protein_grams),
      targetText: formatGrams(plan.protein_target_grams),
      percent: barPercent(actual.protein_grams, plan.protein_target_grams),
    },
    {
      key: 'carbs',
      label: 'Hidratos',
      actualText: formatGrams(actual.carbs_grams),
      targetText: formatGrams(plan.carbs_target_grams),
      percent: barPercent(actual.carbs_grams, plan.carbs_target_grams),
    },
    {
      key: 'fats',
      label: 'Gordura',
      actualText: formatGrams(actual.fats_grams),
      targetText: formatGrams(plan.fats_target_grams),
      percent: barPercent(actual.fats_grams, plan.fats_target_grams),
    },
  ];
  if (actual.fiber_grams > 0)
    rows.push({
      key: 'fiber',
      label: 'Fibra',
      actualText: formatGrams(actual.fiber_grams),
      targetText: '—',
      percent: null,
    });
  return rows;
}

const GOAL_LABELS: ReadonlyMap<string, string> = new Map([
  ['maintenance', 'Manutenção'],
  ['deficit', 'Défice'],
  ['surplus', 'Superávit'],
]);

/**
 * Palavra do objetivo do plano. Um valor que o frontend não conhece (ou `null`) não mostra
 * nada: melhor sem etiqueta do que com o código cru.
 */
export function goalLabel(goal: string | null): string | null {
  return goal === null ? null : (GOAL_LABELS.get(goal) ?? null);
}

/** Número de refeições por extenso: "1 refeição", "3 refeições". */
export function mealCountLabel(count: number): string {
  return count === 1 ? '1 refeição' : `${count} refeições`;
}

/** Período do plano: "Desde 05/10/2026" ou "05/10/2026 a 01/11/2026". */
export function planPeriodLabel(startsDate: string, endsDate: string | null): string {
  return endsDate === null
    ? `Desde ${formatDate(startsDate)}`
    : `${formatDate(startsDate)} a ${formatDate(endsDate)}`;
}

/** Dose de um suplemento associado a uma refeição: "5 g", "2,5 ml". */
export function mealSupplementDose(supplement: {
  quantity: number;
  unit_of_measure: string;
}): string {
  return `${formatDecimal(supplement.quantity)} ${supplement.unit_of_measure}`;
}
