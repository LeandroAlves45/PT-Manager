import { describe, expect, it } from 'vitest';

import {
  formatGrams,
  formatKcal,
  goalLabel,
  dailyTargets,
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

describe('dailyTargets', () => {
  const plan = {
    target_kcal: 2400,
    show_macros: true,
    protein_target_grams: 180,
    carbs_target_grams: 270.5,
    fats_target_grams: 67,
  };

  it('shows the kcal target and the three macro targets when the plan shows macros', () => {
    const targets = dailyTargets(plan);

    expect(targets.kcalText).toBe('2400');
    expect(targets.macros.map((macro) => [macro.key, macro.label, macro.gramsText])).toEqual([
      ['protein', 'Proteína', '180 g'],
      ['carbs', 'Hidratos', '270,5 g'],
      ['fats', 'Gordura', '67 g'],
    ]);
  });

  it('shows only the kcal when the personal trainer hides the macros', () => {
    const targets = dailyTargets({
      ...plan,
      show_macros: false,
      protein_target_grams: null,
      carbs_target_grams: null,
      fats_target_grams: null,
    });

    expect(targets).toEqual({ kcalText: '2400', macros: [] });
  });

  it('follows the flag even when the grams are still there', () => {
    expect(dailyTargets({ ...plan, show_macros: false }).macros).toEqual([]);
  });

  it('never shows a macro without a value, even if the flag says to', () => {
    expect(dailyTargets({ ...plan, carbs_target_grams: null }).macros).toEqual([]);
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
