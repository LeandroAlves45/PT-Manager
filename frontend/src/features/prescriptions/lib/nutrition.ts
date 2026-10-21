import type { components } from '@/shared/api/schema';

/** Pedido de cálculo, igual no preview, na criação e no `PUT`. */
export type Calculation = components['schemas']['NutritionCalculationRequest'];
/** Snapshot do cálculo devolvido pelo servidor, com os valores de entrada usados. */
export type CalculationResult = components['schemas']['NutritionCalculationResponse'];

/** Cálculo vazio de um plano novo: nada vem pré-preenchido (só «Usar avaliação inicial» copia valores). */
export const EMPTY_CALCULATION: Calculation = {
  calculation_origin: '',
  energy_formula: null,
  weight_kg: 0,
  height_cm: null,
  age: null,
  sex: null,
  body_fat_percentage: null,
  activity_level: null,
  goal_type: null,
  goal_adjustment_kcal: null,
  manual_target_kcal: null,
  macro_mode: '',
  protein_percentage: null,
  carbs_percentage: null,
  fats_percentage: null,
  protein_grams_per_kg: null,
  fats_grams_per_kg: null,
  protein_grams: null,
  carbs_grams: null,
  fats_grams: null,
};

/**
 * O snapshot conserva os inputs originais. Os alvos derivados nunca substituem
 * percentagens ou gramas por kg, porque isso perderia precisão ao reabrir.
 */
export function calculationFromResult(result: CalculationResult): Calculation {
  return {
    calculation_origin: result.calculation_origin,
    energy_formula: result.energy_formula,
    weight_kg: result.weight_kg_used,
    height_cm: result.height_cm_used,
    age: result.age_used,
    sex: result.sex_used,
    body_fat_percentage: result.body_fat_percentage_used,
    activity_level: result.activity_level,
    goal_type: result.goal_type,
    goal_adjustment_kcal: result.goal_adjustment_kcal,
    manual_target_kcal: result.calculation_origin === 'manual_energy' ? result.target_kcal : null,
    macro_mode: result.macro_distribution_mode,
    protein_percentage: result.protein_percentage_input,
    carbs_percentage: result.carbs_percentage_input,
    fats_percentage: result.fats_percentage_input,
    protein_grams_per_kg: result.protein_grams_per_kg_input,
    fats_grams_per_kg: result.fats_grams_per_kg_input,
    protein_grams:
      result.macro_distribution_mode === 'manual_grams' ? result.protein_target_grams : null,
    carbs_grams:
      result.macro_distribution_mode === 'manual_grams' ? result.carbs_target_grams : null,
    fats_grams: result.macro_distribution_mode === 'manual_grams' ? result.fats_target_grams : null,
  };
}

/** Remove valores proibidos pelo modo antes do preview e da escrita. */
export function normalizeCalculation(input: Calculation): Calculation {
  return {
    ...input,
    energy_formula: input.calculation_origin === 'formula' ? input.energy_formula : null,
    height_cm: input.calculation_origin === 'formula' ? input.height_cm : null,
    age: input.calculation_origin === 'formula' ? input.age : null,
    sex: input.calculation_origin === 'formula' ? input.sex : null,
    body_fat_percentage: input.calculation_origin === 'formula' ? input.body_fat_percentage : null,
    activity_level: input.calculation_origin === 'formula' ? input.activity_level : null,
    goal_type: input.calculation_origin === 'formula' ? input.goal_type : null,
    goal_adjustment_kcal:
      input.calculation_origin === 'formula' ? input.goal_adjustment_kcal : null,
    manual_target_kcal:
      input.calculation_origin === 'manual_energy' ? input.manual_target_kcal : null,
    protein_percentage: input.macro_mode === 'percentage' ? input.protein_percentage : null,
    carbs_percentage: input.macro_mode === 'percentage' ? input.carbs_percentage : null,
    fats_percentage: input.macro_mode === 'percentage' ? input.fats_percentage : null,
    protein_grams_per_kg: input.macro_mode === 'grams_per_kg' ? input.protein_grams_per_kg : null,
    fats_grams_per_kg: input.macro_mode === 'grams_per_kg' ? input.fats_grams_per_kg : null,
    protein_grams: input.macro_mode === 'manual_grams' ? input.protein_grams : null,
    carbs_grams: input.macro_mode === 'manual_grams' ? input.carbs_grams : null,
    fats_grams: input.macro_mode === 'manual_grams' ? input.fats_grams : null,
  };
}

/** Totais de energia e macros, com as mesmas casas decimais que o servidor devolve. */
export interface Totals {
  readonly kcal: number;
  readonly protein: number;
  readonly carbs: number;
  readonly fats: number;
  readonly fiber: number;
}

/** Valores nutricionais de um alimento por 100 g, como estão no catálogo. */
export interface FoodNutrients {
  readonly kcal: number;
  readonly protein: number;
  readonly carbs: number;
  readonly fats: number;
  /** `null` quando o catálogo não tem fibra para o alimento. */
  readonly fiber: number | null;
}

/** Totais de um plano ou refeição sem alimentos. */
export const ZERO_TOTALS: Totals = { kcal: 0, protein: 0, carbs: 0, fats: 0, fiber: 0 };

/** Arredonda a 2 casas, meio para longe de zero (igual a `MidpointRounding.AwayFromZero`). */
export function roundTwo(value: number): number {
  const rounded = Math.round(Math.abs(value) * 100 + 1e-9) / 100;
  return value < 0 ? -rounded : rounded;
}

/** Quantidade em gramas utilizável: números inválidos ou negativos contam como zero. */
function safeGrams(grams: number): number {
  return Number.isFinite(grams) && grams > 0 ? grams : 0;
}

/** Contribuição de um alimento: valor por 100 g × gramas / 100 (`CalculateContribution` do servidor). */
export function foodContribution(nutrients: FoodNutrients, grams: number): Totals {
  const factor = safeGrams(grams) / 100;
  return {
    kcal: roundTwo(nutrients.kcal * factor),
    protein: roundTwo(nutrients.protein * factor),
    carbs: roundTwo(nutrients.carbs * factor),
    fats: roundTwo(nutrients.fats * factor),
    fiber: roundTwo((nutrients.fiber ?? 0) * factor),
  };
}

/** Linha de alimento mínima para somar: id do catálogo e gramas. */
export interface FoodLine {
  readonly food_id: string;
  readonly quantity_in_grams: number;
}

/**
 * Total de uma lista de alimentos. Soma sem arredondar e arredonda no fim, como o servidor
 * (`CalculateTotals`); alimentos ainda sem valores carregados contam como zero.
 */
export function sumFoods(
  lines: readonly FoodLine[],
  lookup: Readonly<Record<string, FoodNutrients>>
): Totals {
  let kcal = 0;
  let protein = 0;
  let carbs = 0;
  let fats = 0;
  let fiber = 0;
  for (const line of lines) {
    const nutrients = lookup[line.food_id];
    if (nutrients === undefined) continue;
    const factor = safeGrams(line.quantity_in_grams) / 100;
    kcal += nutrients.kcal * factor;
    protein += nutrients.protein * factor;
    carbs += nutrients.carbs * factor;
    fats += nutrients.fats * factor;
    fiber += (nutrients.fiber ?? 0) * factor;
  }
  return {
    kcal: roundTwo(kcal),
    protein: roundTwo(protein),
    carbs: roundTwo(carbs),
    fats: roundTwo(fats),
    fiber: roundTwo(fiber),
  };
}

/** Metas de um plano, vindas do cálculo (a fibra não tem meta). */
export interface Targets {
  readonly kcal: number;
  readonly protein: number;
  readonly carbs: number;
  readonly fats: number;
}

/** O que falta para a meta; negativo quando os alimentos já a excedem. */
export function remaining(target: number, total: number): number {
  return roundTwo(target - total);
}

/** Metas de um cálculo (preview ou snapshot). */
export function targetsFromResult(result: CalculationResult): Targets {
  return {
    kcal: result.target_kcal,
    protein: result.protein_target_grams,
    carbs: result.carbs_target_grams,
    fats: result.fats_target_grams,
  };
}

/** Idade em anos completos à data `today`, a partir de `yyyy-MM-dd`. */
export function ageOnDate(birthDate: string, today: Date): number {
  const [year = 0, month = 1, day = 1] = birthDate.split('-').map(Number);
  let age = today.getFullYear() - year;

  if (today.getMonth() + 1 < month || (today.getMonth() + 1 === month && today.getDate() < day))
    age--;
  return age;
}

/** Idade mínima das fórmulas de energia (o servidor recusa fórmula abaixo disto). */
export const FORMULA_MIN_AGE = 18;

/** Dados da ficha do cliente que o cálculo aproveita. */
export interface ClientProfile {
  readonly sex: string;
  readonly birth_date: string;
}

/** Dados da avaliação inicial que o cálculo aproveita. */
export interface AssessmentMeasures {
  readonly weight_kg: number;
  readonly height_cm: number;
  readonly body_fat_percentage: number | null;
  readonly activity_level: string;
}

/**
 * Copia para o cálculo os valores da ficha e da avaliação inicial («Usar avaliação inicial»).
 * Só substitui o que existe: sem avaliação ficam o sexo e a idade da ficha, e a gordura
 * corporal só entra quando a avaliação a tem. Não mexe na origem, no objetivo nem nas macros.
 */
export function calculationFromAssessment(
  current: Calculation,
  client: ClientProfile,
  assessment: AssessmentMeasures | null,
  today: Date
): Calculation {
  return {
    ...current,
    sex: client.sex,
    age: ageOnDate(client.birth_date, today),
    weight_kg: assessment?.weight_kg ?? current.weight_kg,
    height_cm: assessment?.height_cm ?? current.height_cm,
    activity_level: assessment?.activity_level ?? current.activity_level,
    body_fat_percentage: assessment?.body_fat_percentage ?? current.body_fat_percentage,
  };
}

/** Totais devolvidos pelo servidor (`NutritionTotalsResponse`) no formato do ecrã. */
export function totalsFromResponse(
  totals: components['schemas']['NutritionTotalsResponse']
): Totals {
  return {
    kcal: totals.kcal,
    protein: totals.protein_grams,
    carbs: totals.carbs_grams,
    fats: totals.fats_grams,
    fiber: totals.fiber_grams,
  };
}

/** Unidade de um suplemento para mostrar: «gr» conta como «g» (U60); vazia ou ausente → `null`. */
export function unitLabel(unit: string | undefined): string | null {
  const trimmed = unit?.trim() ?? '';
  if (trimmed === '') return null;
  return trimmed.toLowerCase() === 'gr' ? 'g' : trimmed;
}
