import { describe, expect, it } from 'vitest';

import {
  foodDisplayName,
  formatGrams,
  formatKcal,
  macrosLabel,
  mealSupplementDose,
} from '@/features/portal/lib/nutrition';

describe('formatGrams', () => {
  it.each([
    [80, '80 g'],
    [79.25, '79,3 g'],
    [99.36, '99,4 g'],
    [0.45, '0,5 g'],
    [0, '0 g'],
  ])('formats %s as "%s" (one decimal at most, PT comma)', (value, expected) => {
    expect(formatGrams(value)).toBe(expected);
  });
});

describe('formatKcal', () => {
  it.each([
    [925.6, '926 kcal'],
    [2400, '2400 kcal'],
    [136, '136 kcal'],
  ])('formats %s as "%s" (whole kcal)', (value, expected) => {
    expect(formatKcal(value)).toBe(expected);
  });
});

describe('macrosLabel', () => {
  it('lists protein, carbs and fat in that order and leaves kcal and fibre out', () => {
    expect(
      macrosLabel({
        protein_grams: 19.4,
        carbs_grams: 57.36,
        fats_grams: 12.2,
        kcal: 433.6,
        fiber_grams: 8.48,
      })
    ).toBe('Proteína 19,4 g · Hidratos 57,4 g · Gordura 12,2 g');
  });
});

describe('foodDisplayName', () => {
  it('keeps the name of an available food', () => {
    expect(foodDisplayName({ food_name: 'Aveia', is_unavailable: false })).toBe('Aveia');
  });

  it('translates the masked name of a blocked food', () => {
    expect(foodDisplayName({ food_name: 'Unavailable food', is_unavailable: true })).toBe(
      'Alimento indisponível'
    );
  });
});

describe('mealSupplementDose', () => {
  it.each([
    [5, 'g', '5 g'],
    [2.5, 'ml', '2,5 ml'],
    [1, 'cápsula', '1 cápsula'],
  ])('joins %s and "%s" as "%s"', (quantity, unit, expected) => {
    expect(mealSupplementDose({ quantity, unit_of_measure: unit })).toBe(expected);
  });
});
