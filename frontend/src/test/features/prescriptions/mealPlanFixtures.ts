import { http, HttpResponse } from 'msw';

import { API, problem } from '@/test/msw/handlers';

export const CLIENT_ID = '11111111-1111-1111-1111-111111111111';
export const PLAN_ID = '33333333-3333-3333-3333-333333333333';
export const FOOD_ID = '44444444-4444-4444-4444-444444444444';
export const SUPPLEMENT_ID = '55555555-5555-5555-5555-555555555555';
/** Alimento do catálogo que substitui um item bloqueado nos testes. */
export const OTHER_FOOD_ID = '44444444-4444-4444-4444-444444444445';
/** Linha de alimento do plano gravado e linha de suplemento do plano, para os pedidos. */
export const MEAL_ITEM_ID = '77777777-7777-7777-7777-777777777777';
export const PLAN_SUPPLEMENT_ID = '99999999-9999-9999-9999-999999999999';

/** Bloqueio pela moderação com motivo e nota, como a API o devolve. */
export const blockedEnforcement = {
  reason: 'dangerous_information',
  note: 'Quantidades fora do seguro',
};

/** Cálculo gravado: 2000 kcal, 140 P, 200 H, 70 G. */
export const savedCalculation = {
  schema_version: 1,
  calculation_origin: 'manual_energy',
  calculated_at: '2026-09-01T10:00:00Z',
  energy_formula: null,
  weight_kg_used: 80,
  height_cm_used: null,
  age_used: null,
  sex_used: null,
  body_fat_percentage_used: null,
  activity_level: null,
  activity_factor: null,
  goal_type: null,
  goal_adjustment_kcal: null,
  resting_energy_expenditure_kcal: null,
  total_daily_energy_expenditure_kcal: null,
  target_kcal: 2000,
  macro_distribution_mode: 'manual_grams',
  protein_percentage_input: null,
  carbs_percentage_input: null,
  fats_percentage_input: null,
  protein_grams_per_kg_input: null,
  fats_grams_per_kg_input: null,
  protein_target_grams: 140,
  carbs_target_grams: 200,
  fats_target_grams: 70,
  protein_energy_percentage: 28,
  carbs_energy_percentage: 40,
  fats_energy_percentage: 32,
  calculated_macro_kcal: 1990,
  kcal_difference: -10,
};

/** Plano gravado com uma refeição: 150 g de arroz (130 kcal/100 g) e creatina. */
export function savedPlan(overrides: Record<string, unknown> = {}) {
  return {
    id: PLAN_ID,
    client_id: CLIENT_ID,
    name: 'Plano alimentar A',
    description: null,
    starts_date: '2026-09-01',
    ends_date: null,
    calculation: savedCalculation,
    actual_totals: {
      protein_grams: 4,
      carbs_grams: 42,
      fats_grams: 0.5,
      kcal: 195,
      fiber_grams: 0,
    },
    is_active: true,
    is_archived: false,
    needs_review: false,
    meals: [
      {
        id: '66666666-6666-6666-6666-666666666666',
        meal_type: 'Pequeno-almoço',
        order_number: 1,
        totals: { protein_grams: 4, carbs_grams: 42, fats_grams: 0.5, kcal: 195, fiber_grams: 0 },
        items: [
          {
            id: MEAL_ITEM_ID,
            food_id: FOOD_ID,
            food_name: 'Arroz cozido',
            quantity_in_grams: 150,
            order_number: 1,
            protein_per100_g: 2.7,
            carbs_per100_g: 28,
            fats_per100_g: 0.3,
            kcal_per100_g: 130,
            fiber_per100_g: null,
            contribution: {
              protein_grams: 4.05,
              carbs_grams: 42,
              fats_grams: 0.45,
              kcal: 195,
              fiber_grams: 0,
            },
            platform_enforcement: null,
          },
        ],
        supplements: [
          {
            id: '88888888-8888-8888-8888-888888888888',
            supplement_id: SUPPLEMENT_ID,
            supplement_name: 'Creatina',
            unit_of_measure: 'g',
            notes: 'todos os dias',
            quantity: 5,
            order_number: 1,
            platform_enforcement: null,
          },
        ],
      },
    ],
    plan_supplements: [
      {
        id: PLAN_SUPPLEMENT_ID,
        supplement_id: SUPPLEMENT_ID,
        supplement_name: 'Creatina',
        unit_of_measure: 'g',
        notes: null,
        quantity: 3,
        order_number: 1,
        platform_enforcement: null,
      },
    ],
    created_at: '2026-09-01T10:00:00Z',
    updated_at: '2026-09-01T10:00:00Z',
    ...overrides,
  };
}

/**
 * Plano gravado em que o arroz está bloqueado (com nota), como o servidor o envia: a linha
 * continua no plano mas os totais da refeição e do plano já não a contam.
 */
export function blockedFoodPlan() {
  const plan = savedPlan();
  const [meal] = plan.meals;
  return savedPlan({
    actual_totals: { protein_grams: 0, carbs_grams: 0, fats_grams: 0, kcal: 0, fiber_grams: 0 },
    meals: [
      {
        ...meal!,
        totals: { protein_grams: 0, carbs_grams: 0, fats_grams: 0, kcal: 0, fiber_grams: 0 },
        items: meal!.items.map((item) => ({ ...item, platform_enforcement: blockedEnforcement })),
      },
    ],
  });
}

/** Cliente adulto (nasceu em 1990) ou menor, conforme `birth_date`. */
export function clientHandlers(birthDate = '1990-07-01') {
  return [
    http.get(API + '/clients/:id', () =>
      HttpResponse.json({
        id: CLIENT_ID,
        name: 'Marta Figueiredo',
        birth_date: birthDate,
        sex: 'female',
      })
    ),
    http.get(API + '/clients/:id/initial-assessment', () =>
      HttpResponse.json({
        id: 'a1',
        client_id: CLIENT_ID,
        weight_kg: 62.5,
        height_cm: 168,
        body_fat_percentage: 24,
        activity_level: 'lightly_active',
      })
    ),
  ];
}

/** Catálogo: um alimento de 220 kcal/100 g e a creatina em gramas. */
export function catalogHandlers() {
  return [
    http.get(API + '/foods', () =>
      HttpResponse.json({
        items: [
          {
            id: FOOD_ID,
            scope: 'global',
            name: 'Frango assado',
            kcal: 220,
            default_serving_grams: null,
          },
        ],
        total_count: 1,
        page_number: 1,
        page_size: 25,
      })
    ),
    http.get(API + '/foods/:id', () =>
      HttpResponse.json({
        id: FOOD_ID,
        scope: 'global',
        name: 'Frango assado',
        protein: 20,
        carbs: 0,
        fats: 15,
        kcal: 220,
        fiber: null,
        default_serving_grams: null,
      })
    ),
    http.get(API + '/supplements', () =>
      HttpResponse.json({
        items: [
          {
            id: SUPPLEMENT_ID,
            scope: 'global',
            name: 'Creatina',
            serving_size: '5 g',
            timing: null,
          },
        ],
        total_count: 1,
        page_number: 1,
        page_size: 25,
      })
    ),
    http.get(API + '/supplements/:id', () =>
      HttpResponse.json({ id: SUPPLEMENT_ID, name: 'Creatina', unit_of_measure: 'g' })
    ),
    http.get(API + '/meal-plans', () =>
      HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
    ),
  ];
}

/** Pesquisa e ficha de um segundo alimento (Peito de peru, 110 kcal/100 g) que substitui o bloqueado. */
export function replacementFoodHandlers() {
  return [
    http.get(API + '/foods', () =>
      HttpResponse.json({
        items: [
          {
            id: OTHER_FOOD_ID,
            scope: 'global',
            name: 'Peito de peru',
            kcal: 110,
            default_serving_grams: null,
          },
        ],
        total_count: 1,
        page_number: 1,
        page_size: 25,
      })
    ),
    http.get(API + '/foods/:id', () =>
      HttpResponse.json({
        id: OTHER_FOOD_ID,
        scope: 'global',
        name: 'Peito de peru',
        protein: 22,
        carbs: 0,
        fats: 2,
        kcal: 110,
        fiber: null,
        default_serving_grams: null,
      })
    ),
  ];
}

export { problem };
