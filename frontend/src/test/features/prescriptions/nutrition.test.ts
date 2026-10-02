import { describe, expect, it } from 'vitest';

import {
  EMPTY_CALCULATION,
  calculationFromResult,
  normalizeCalculation,
} from '@/features/prescriptions/lib/nutrition';
import type { components } from '@/shared/api/schema';

type Result = components['schemas']['NutritionCalculationResponse'];

const result: Result = {
  schema_version: 1,
  calculation_origin: 'formula',
  calculated_at: '2026-09-30T10:00:00Z',
  energy_formula: 'mifflin_st_jeor',
  weight_kg_used: 80,
  height_cm_used: 180,
  age_used: 30,
  sex_used: 'male',
  body_fat_percentage_used: null,
  activity_level: 'moderately_active',
  activity_factor: 1.55,
  goal_type: 'maintenance',
  goal_adjustment_kcal: 0,
  resting_energy_expenditure_kcal: 1800,
  total_daily_energy_expenditure_kcal: 2790,
  target_kcal: 2790,
  macro_distribution_mode: 'grams_per_kg',
  protein_percentage_input: null,
  carbs_percentage_input: null,
  fats_percentage_input: null,
  protein_grams_per_kg_input: 1.75,
  fats_grams_per_kg_input: 0.85,
  protein_target_grams: 140,
  carbs_target_grams: 420,
  fats_target_grams: 68,
  protein_energy_percentage: 20,
  carbs_energy_percentage: 60,
  fats_energy_percentage: 20,
  calculated_macro_kcal: 2852,
  kcal_difference: 62,
};

describe('nutrition calculation mapping', () => {
  it('restores original grams per kg instead of deriving them from rounded targets', () => {
    const restored = calculationFromResult(result);

    expect(restored.protein_grams_per_kg).toBe(1.75);
    expect(restored.fats_grams_per_kg).toBe(0.85);
    expect(restored.protein_grams).toBeNull();
  });

  it('clears formula values for manual energy and percentage values for manual grams', () => {
    const request = normalizeCalculation({
      ...EMPTY_CALCULATION,
      calculation_origin: 'manual_energy',
      weight_kg: 80,
      manual_target_kcal: 2000,
      energy_formula: 'mifflin_st_jeor',
      height_cm: 180,
      age: 30,
      sex: 'male',
      macro_mode: 'manual_grams',
      protein_percentage: 30,
      protein_grams: 140,
      carbs_grams: 200,
      fats_grams: 70,
    });

    expect(request.energy_formula).toBeNull();
    expect(request.height_cm).toBeNull();
    expect(request.age).toBeNull();
    expect(request.sex).toBeNull();
    expect(request.protein_percentage).toBeNull();
    expect(request.protein_grams).toBe(140);
  });
});
