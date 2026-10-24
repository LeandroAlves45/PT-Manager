import { describe, expect, it } from 'vitest';

import {
  formatGrams,
  formatKcal,
  goalLabel,
  macroRows,
  mealCountLabel,
  planPeriodLabel,
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

describe('macroRows', () => {
  const plan = {
    target_kcal: 2400,
    protein_target_grams: 180,
    carbs_target_grams: 270,
    fats_target_grams: 67,
    actual_totals: {
      kcal: 925.6,
      protein_grams: 79.25,
      carbs_grams: 99.36,
      fats_grams: 19.13,
      fiber_grams: 9.08,
    },
  };

  it('lists energy, protein, carbs, fat and fibre with text and the share of the target', () => {
    expect(
      macroRows(plan).map((row) => [row.key, row.actualText, row.targetText, row.percent])
    ).toEqual([
      ['kcal', '926 kcal', '2400 kcal', 39],
      ['protein', '79,3 g', '180 g', 44],
      ['carbs', '99,4 g', '270 g', 37],
      ['fats', '19,1 g', '67 g', 29],
      ['fiber', '9,1 g', '—', null],
    ]);
  });

  it('caps the bar at 100 when the plan goes over the target', () => {
    const rows = macroRows({ ...plan, actual_totals: { ...plan.actual_totals, kcal: 4800 } });

    expect(rows[0]!.percent).toBe(100);
    expect(rows[0]!.actualText).toBe('4800 kcal');
  });

  it('leaves the fibre out when the plan has none and draws no bar without a target', () => {
    const rows = macroRows({
      ...plan,
      protein_target_grams: 0,
      actual_totals: { ...plan.actual_totals, fiber_grams: 0 },
    });

    expect(rows.map((row) => row.key)).not.toContain('fiber');
    expect(rows[1]!.percent).toBeNull();
  });
});

describe('goalLabel', () => {
  it.each([
    ['maintenance', 'Manutenção'],
    ['deficit', 'Défice'],
    ['surplus', 'Superávit'],
    [null, null],
    ['unknown', null],
    ['constructor', null],
  ])('maps %s to %s', (goal, expected) => {
    expect(goalLabel(goal)).toBe(expected);
  });
});

describe('mealCountLabel', () => {
  it.each([
    [1, '1 refeição'],
    [3, '3 refeições'],
  ])('writes %s as "%s"', (count, expected) => {
    expect(mealCountLabel(count)).toBe(expected);
  });
});

describe('planPeriodLabel', () => {
  it('writes an open plan from its start and a closed one as a range', () => {
    expect(planPeriodLabel('2026-10-05', null)).toBe('Desde 05/10/2026');
    expect(planPeriodLabel('2026-10-05', '2026-11-01')).toBe('05/10/2026 a 01/11/2026');
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
