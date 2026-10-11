import { formatDecimal } from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { formatNumber } from '@/shared/lib/format';

/**
 * Regras de apresentação do plano alimentar do cliente.
 *
 * Funções puras. O backend envia os totais com duas casas decimais (`decimal` arredondado em
 * `MyNutritionPlanQueries`); num telemóvel mostram-se as kcal inteiras e os gramas com uma
 * casa no máximo.
 */

type Totals = components['schemas']['MyNutritionTotalsResponse'];

const GRAMS = new Intl.NumberFormat('pt-PT', { maximumFractionDigits: 1 });

/** Gramas com uma casa decimal no máximo: 80 -> "80 g" e 79.25 -> "79,3 g". */
export function formatGrams(value: number): string {
  return `${GRAMS.format(value)} g`;
}

/** Energia em kcal inteiras: 925.6 -> "926 kcal". */
export function formatKcal(value: number): string {
  return `${formatNumber(value)} kcal`;
}

/** Macros de uma refeição numa linha: "Proteína 19,4 g · Hidratos 57,4 g · Gordura 12,2 g". */
export function macrosLabel(totals: Totals): string {
  return [
    `Proteína ${formatGrams(totals.protein_grams)}`,
    `Hidratos ${formatGrams(totals.carbs_grams)}`,
    `Gordura ${formatGrams(totals.fats_grams)}`,
  ].join(' · ');
}

/**
 * Nome a mostrar. Um alimento bloqueado pela plataforma chega mascarado em inglês
 * (`"Unavailable food"`, `PortalContentMasking`); o cliente vê a versão PT.
 */
export function foodDisplayName(item: { food_name: string; is_unavailable: boolean }): string {
  return item.is_unavailable ? 'Alimento indisponível' : item.food_name;
}

/** Dose de um suplemento associado a uma refeição: "5 g", "2,5 ml". */
export function mealSupplementDose(supplement: {
  quantity: number;
  unit_of_measure: string;
}): string {
  return `${formatDecimal(supplement.quantity)} ${supplement.unit_of_measure}`;
}
