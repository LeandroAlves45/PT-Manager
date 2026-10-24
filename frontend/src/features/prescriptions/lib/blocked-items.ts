import {
  sumFoods,
  type FoodLine,
  type FoodNutrients,
  type Totals,
} from '@/features/prescriptions/lib/nutrition';
import type { TrainingDetails } from '@/features/prescriptions/lib/training-draft';
import type { components } from '@/shared/api/schema';

/** Bloqueio pela moderação, como a API o devolve em `platform_enforcement`. */
export type Enforcement = components['schemas']['PlatformEnforcementResponse'];

/**
 * Itens bloqueados do plano gravado, por ID da linha (`MealItemRequest.id` ou
 * `MealSupplementRequest.id`). Guarda também o item do catálogo que estava bloqueado: se o
 * personal trainer trocar a linha por outro item, o bloqueio já não se aplica.
 */
export type BlockedItems = Readonly<
  Record<string, { readonly catalogId: string; readonly enforcement: Enforcement }>
>;

/** Sem itens bloqueados: um plano novo, ou um plano sem bloqueios. */
export const NO_BLOCKED_ITEMS: BlockedItems = {};

/** Linha de alimento com o ID da linha (`null` numa linha nova, que nunca está bloqueada). */
export interface BlockableFoodLine extends FoodLine {
  readonly id: string | null;
}

/**
 * Recolhe do detalhe do plano os alimentos e suplementos (de refeição e do plano) que a
 * moderação bloqueou, indexados pelo ID da linha.
 */
export function blockedItemsFromDetails(
  details: components['schemas']['MealPlanDetailsResponse']
): BlockedItems {
  const blocked: Record<string, { catalogId: string; enforcement: Enforcement }> = {};
  for (const meal of details.meals) {
    for (const item of meal.items) {
      if (item.platform_enforcement !== null)
        blocked[item.id] = { catalogId: item.food_id, enforcement: item.platform_enforcement };
    }
    for (const item of meal.supplements) {
      if (item.platform_enforcement !== null)
        blocked[item.id] = {
          catalogId: item.supplement_id,
          enforcement: item.platform_enforcement,
        };
    }
  }
  for (const item of details.plan_supplements) {
    if (item.platform_enforcement !== null)
      blocked[item.id] = { catalogId: item.supplement_id, enforcement: item.platform_enforcement };
  }
  return blocked;
}

/**
 * Recolhe do detalhe do plano de treino os exercícios que a moderação bloqueou, indexados pelo
 * ID da linha (`DayExerciseRequest.id`), com o exercício do catálogo bloqueado.
 */
export function blockedExercisesFromDetails(details: TrainingDetails): BlockedItems {
  const blocked: Record<string, { catalogId: string; enforcement: Enforcement }> = {};
  for (const day of details.days) {
    for (const exercise of day.exercises) {
      if (exercise.platform_enforcement !== null)
        blocked[exercise.id] = {
          catalogId: exercise.exercise_id,
          enforcement: exercise.platform_enforcement,
        };
    }
  }
  return blocked;
}

/**
 * Bloqueio que ainda vale para uma linha: só quando a linha vem do plano gravado e continua a
 * apontar para o item bloqueado. Linha nova, ou trocada por outro item, devolve `null`.
 *
 * @param lineId ID da linha (`null` numa linha nova).
 * @param catalogId Item do catálogo que a linha tem agora.
 */
export function enforcementFor(
  blocked: BlockedItems,
  lineId: string | null,
  catalogId: string
): Enforcement | null {
  if (lineId === null || !Object.hasOwn(blocked, lineId)) return null;
  const entry = blocked[lineId]!;
  return entry.catalogId === catalogId ? entry.enforcement : null;
}

/**
 * Total de uma lista de alimentos sem os que continuam bloqueados (como o servidor, que os
 * exclui dos totais). Sem bloqueios devolve o mesmo que `sumFoods`.
 */
export function sumUnblockedFoods(
  lines: readonly BlockableFoodLine[],
  lookup: Readonly<Record<string, FoodNutrients>>,
  blocked: BlockedItems
): Totals {
  return sumFoods(
    lines.filter((line) => enforcementFor(blocked, line.id, line.food_id) === null),
    lookup
  );
}
