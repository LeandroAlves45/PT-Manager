import { describe, expect, it } from 'vitest';

import {
  NO_BLOCKED_ITEMS,
  blockedExercisesFromDetails,
  blockedItemsFromDetails,
  enforcementFor,
  sumUnblockedFoods,
} from '@/features/prescriptions/lib/blocked-items';
import { sumFoods, type FoodNutrients } from '@/features/prescriptions/lib/nutrition';
import type { components } from '@/shared/api/schema';
import {
  FOOD_ID,
  MEAL_ITEM_ID,
  OTHER_FOOD_ID,
  PLAN_SUPPLEMENT_ID,
  SUPPLEMENT_ID,
  blockedEnforcement,
  blockedFoodPlan,
  savedPlan,
} from '@/test/features/prescriptions/mealPlanFixtures';

import {
  BLOCKED_ENFORCEMENT,
  DAY_EXERCISE_ID,
  details as trainingDetails,
  detailsWithBlockedExercise,
  EXERCISE_ID,
} from '@/test/features/prescriptions/trainingFixtures';

type TrainingPlan = Parameters<typeof blockedExercisesFromDetails>[0];
type Details = components['schemas']['MealPlanDetailsResponse'];

const MEAL_SUPPLEMENT_ID = '88888888-8888-8888-8888-888888888888';

const lookup: Record<string, FoodNutrients> = {
  [FOOD_ID]: { kcal: 130, protein: 2.7, carbs: 28, fats: 0.3, fiber: null },
  [OTHER_FOOD_ID]: { kcal: 110, protein: 22, carbs: 0, fats: 2, fiber: null },
};

describe('blockedItemsFromDetails', () => {
  it('is empty when nothing is blocked', () => {
    expect(blockedItemsFromDetails(savedPlan() as unknown as Details)).toEqual({});
  });

  it('indexes blocked foods, meal supplements and plan supplements by line id', () => {
    const plan = blockedFoodPlan();
    const [meal] = plan.meals;
    const details = savedPlan({
      meals: [
        {
          ...meal!,
          supplements: meal!.supplements.map((item) => ({
            ...item,
            platform_enforcement: { reason: 'prohibited_content', note: null },
          })),
        },
      ],
      plan_supplements: plan.plan_supplements.map((item) => ({
        ...item,
        platform_enforcement: blockedEnforcement,
      })),
    });

    expect(blockedItemsFromDetails(details as unknown as Details)).toEqual({
      [MEAL_ITEM_ID]: { catalogId: FOOD_ID, enforcement: blockedEnforcement },
      [MEAL_SUPPLEMENT_ID]: {
        catalogId: SUPPLEMENT_ID,
        enforcement: { reason: 'prohibited_content', note: null },
      },
      [PLAN_SUPPLEMENT_ID]: { catalogId: SUPPLEMENT_ID, enforcement: blockedEnforcement },
    });
  });
});

describe('enforcementFor', () => {
  const blocked = { [MEAL_ITEM_ID]: { catalogId: FOOD_ID, enforcement: blockedEnforcement } };

  it('returns the enforcement while the line still points at the blocked item', () => {
    expect(enforcementFor(blocked, MEAL_ITEM_ID, FOOD_ID)).toBe(blockedEnforcement);
  });

  it('returns null once the line points at another catalog item', () => {
    expect(enforcementFor(blocked, MEAL_ITEM_ID, OTHER_FOOD_ID)).toBeNull();
  });

  it('returns null for a new line, an unknown line or an inherited key', () => {
    expect(enforcementFor(blocked, null, FOOD_ID)).toBeNull();
    expect(enforcementFor(blocked, 'outra-linha', FOOD_ID)).toBeNull();
    expect(enforcementFor(blocked, 'constructor', FOOD_ID)).toBeNull();
  });
});

describe('sumUnblockedFoods', () => {
  const lines = [
    { id: MEAL_ITEM_ID, food_id: FOOD_ID, quantity_in_grams: 150 },
    { id: 'linha-2', food_id: OTHER_FOOD_ID, quantity_in_grams: 100 },
    { id: null, food_id: OTHER_FOOD_ID, quantity_in_grams: 50 },
  ];

  it('equals sumFoods when nothing is blocked', () => {
    expect(sumUnblockedFoods(lines, lookup, NO_BLOCKED_ITEMS)).toEqual(sumFoods(lines, lookup));
  });

  it('leaves out the foods that are still blocked', () => {
    const blocked = { [MEAL_ITEM_ID]: { catalogId: FOOD_ID, enforcement: blockedEnforcement } };

    // 100 g + 50 g de peito de peru (110 kcal/100 g): o arroz bloqueado não conta.
    expect(sumUnblockedFoods(lines, lookup, blocked)).toEqual(sumFoods(lines.slice(1), lookup));
    expect(sumUnblockedFoods(lines, lookup, blocked).kcal).toBe(165);
  });

  it('counts the line again after it was replaced by another food', () => {
    const blocked = { [MEAL_ITEM_ID]: { catalogId: FOOD_ID, enforcement: blockedEnforcement } };
    const replaced = [{ ...lines[0]!, food_id: OTHER_FOOD_ID }, ...lines.slice(1)];

    // 150 + 100 + 50 g × 110 kcal / 100 g.
    expect(sumUnblockedFoods(replaced, lookup, blocked).kcal).toBe(330);
  });
});

describe('blockedExercisesFromDetails', () => {
  it('is empty when no exercise is blocked', () => {
    expect(blockedExercisesFromDetails(trainingDetails() as unknown as TrainingPlan)).toEqual({});
  });

  it('indexes a blocked exercise by line id, with the catalog exercise that was blocked', () => {
    const blocked = blockedExercisesFromDetails(detailsWithBlockedExercise());

    expect(blocked).toEqual({
      [DAY_EXERCISE_ID]: { catalogId: EXERCISE_ID, enforcement: BLOCKED_ENFORCEMENT },
    });
    expect(enforcementFor(blocked, DAY_EXERCISE_ID, EXERCISE_ID)).toEqual(BLOCKED_ENFORCEMENT);
    expect(enforcementFor(blocked, DAY_EXERCISE_ID, 'outro-exercicio')).toBeNull();
  });
});
