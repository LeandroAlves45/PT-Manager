import { describe, expect, it } from 'vitest';

import {
  EMPTY_CALCULATION,
  ageOnDate,
  calculationFromAssessment,
  calculationFromResult,
  foodContribution,
  normalizeCalculation,
  remaining,
  sumFoods,
  unitLabel,
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

describe('nutrition draft maths', () => {
  const chicken = { kcal: 220, protein: 20, carbs: 0, fats: 15, fiber: null };

  it('uses per 100 g values × grams / 100, rounded to 2 decimals', () => {
    expect(foodContribution(chicken, 1000)).toEqual({
      kcal: 2200,
      protein: 200,
      carbs: 0,
      fats: 150,
      fiber: 0,
    });
    expect(foodContribution({ ...chicken, kcal: 130.555 }, 100).kcal).toBe(130.56);
    expect(foodContribution(chicken, Number.NaN).kcal).toBe(0);
  });

  it('lets what is missing go negative, and counts foods not loaded yet as zero', () => {
    const total = sumFoods(
      [
        { food_id: 'a', quantity_in_grams: 1000 },
        { food_id: 'unknown', quantity_in_grams: 500 },
      ],
      { a: chicken }
    );
    expect(total.kcal).toBe(2200);
    expect(remaining(2000, total.kcal)).toBe(-200);
    expect(remaining(2000, 0)).toBe(2000);
  });

  it('computes ages and copies the assessment without touching origin or macros', () => {
    expect(ageOnDate('2000-06-15', new Date(2026, 5, 14))).toBe(25);
    expect(ageOnDate('2000-06-15', new Date(2026, 5, 15))).toBe(26);

    const copied = calculationFromAssessment(
      { ...EMPTY_CALCULATION, calculation_origin: 'manual_energy', macro_mode: 'percentage' },
      { sex: 'female', birth_date: '2000-06-15' },
      { weight_kg: 62.5, height_cm: 168, body_fat_percentage: null, activity_level: 'sedentary' },
      new Date(2026, 5, 15)
    );
    expect(copied).toMatchObject({
      calculation_origin: 'manual_energy',
      macro_mode: 'percentage',
      sex: 'female',
      age: 26,
      weight_kg: 62.5,
      height_cm: 168,
      body_fat_percentage: null,
      activity_level: 'sedentary',
    });
  });

  it('shows «gr» as «g» and nothing when the unit is unknown', () => {
    expect(unitLabel('GR')).toBe('g');
    expect(unitLabel('ml')).toBe('ml');
    expect(unitLabel(undefined)).toBeNull();
    expect(unitLabel('  ')).toBeNull();
  });
});
