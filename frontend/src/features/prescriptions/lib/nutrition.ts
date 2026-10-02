import type { components } from '@/shared/api/schema';

/** Pedido de cálculo, igual no preview, na criação e no `PUT`. */
export type Calculation = components['schemas']['NutritionCalculationRequest'];
/** Snapshot do cálculo devolvido pelo servidor, com os valores de entrada usados. */
export type CalculationResult = components['schemas']['NutritionCalculationResponse'];

/** Cálculo vazio de um plano novo, antes das sugestões da ficha e da avaliação. */
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
