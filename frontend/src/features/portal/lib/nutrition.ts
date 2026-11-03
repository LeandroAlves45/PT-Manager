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

/** Macros que o personal trainer pode mostrar ao cliente. */
export type MacroKey = 'protein' | 'carbs' | 'fats';

/** Meta diária de uma macro, já formatada. */
export interface MacroTarget {
  readonly key: MacroKey;
  readonly label: string;
  readonly gramsText: string;
}

/** Metas do dia que o cliente vê: as kcal sempre, as macros só se o plano as mostrar. */
export interface DailyTargets {
  readonly kcalText: string;
  /** Vazio quando o personal trainer escondeu as macros neste plano. */
  readonly macros: readonly MacroTarget[];
}

type PlanTargets = Pick<
  components['schemas']['MyNutritionPlanResponse'],
  | 'target_kcal'
  | 'show_macros'
  | 'protein_target_grams'
  | 'carbs_target_grams'
  | 'fats_target_grams'
>;

/**
 * Metas do plano escolhido. Mostra só o objetivo, nunca quanto os alimentos já somam: essa
 * comparação é trabalho do personal trainer e confundia o cliente. Com `show_macros` a false o
 * backend já envia as macros a `null`; aqui também se ignoram, para o ecrã nunca mostrar "null g".
 */
export function dailyTargets(plan: PlanTargets): DailyTargets {
  const grams = [plan.protein_target_grams, plan.carbs_target_grams, plan.fats_target_grams];
  const visible = plan.show_macros && grams.every((value) => value !== null);
  const macros: MacroTarget[] = visible
    ? [
        { key: 'protein', label: 'Proteína', gramsText: formatGrams(plan.protein_target_grams!) },
        { key: 'carbs', label: 'Hidratos', gramsText: formatGrams(plan.carbs_target_grams!) },
        { key: 'fats', label: 'Gordura', gramsText: formatGrams(plan.fats_target_grams!) },
      ]
    : [];
  return { kcalText: formatNumber(plan.target_kcal), macros };
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
