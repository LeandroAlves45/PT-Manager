import { http, HttpResponse } from 'msw';

import { API } from '@/test/msw/handlers';

export const PLAN_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
export const CLIENT_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
export const DAY_ID = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
export const DAY_EXERCISE_ID = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
export const EXERCISE_ID = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';
export const SET_ID = 'ffffffff-ffff-ffff-ffff-ffffffffffff';
export const LOG_ID = '99999999-9999-9999-9999-999999999999';

export const summary = {
  id: PLAN_ID,
  client_id: CLIENT_ID,
  client_name: 'Marta Figueiredo',
  name: 'Plano força',
  description: null,
  training_modality: null,
  start_date: '2026-09-01',
  end_date: null,
  is_active: true,
  is_archived: false,
  needs_review: false,
  created_at: '2026-09-01T10:00:00Z',
  updated_at: '2026-09-01T10:00:00Z',
};

/** Detalhe de um plano: uma série de 10 a 12 repetições (lê-se «12-10») a 80 kg. */
export function details(overrides: Record<string, unknown> = {}) {
  return {
    id: PLAN_ID,
    client_id: CLIENT_ID,
    name: 'Plano força',
    description: null,
    training_modality: null,
    notes: null,
    start_date: '2026-09-01',
    end_date: null,
    is_active: true,
    is_archived: false,
    needs_review: false,
    has_history: false,
    created_at: '2026-09-01T10:00:00Z',
    updated_at: '2026-09-01T10:00:00Z',
    days: [
      {
        id: DAY_ID,
        day_of_week: 0,
        week_number: 1,
        notes: null,
        exercises: [
          {
            id: DAY_EXERCISE_ID,
            exercise_id: EXERCISE_ID,
            exercise_name: 'Agachamento',
            order_number: 1,
            exercise_group_id: null,
            group_position: null,
            notes: null,
            sets: [
              {
                id: SET_ID,
                set_number: 1,
                planned_reps: 10,
                planned_reps_max: 12,
                planned_weight_kg: 80,
                rest_seconds_min: 90,
                rest_seconds_max: 120,
                planned_rpe: 8,
              },
            ],
          },
        ],
      },
    ],
    ...overrides,
  };
}

/** Ficha mínima do cliente: só `id` e `name` interessam às páginas de treino. */
export const clientHandler = http.get(API + '/clients/:id', () =>
  HttpResponse.json({ id: CLIENT_ID, name: 'Marta Figueiredo' })
);

export function logPage(items: unknown[]) {
  return { items, total_count: items.length, page_number: 1, page_size: 100 };
}

export function setLog(overrides: Record<string, unknown> = {}) {
  return {
    id: LOG_ID,
    client_id: CLIENT_ID,
    training_plan_id: PLAN_ID,
    training_plan_day_id: DAY_ID,
    training_plan_day_exercise_id: DAY_EXERCISE_ID,
    exercise_id: EXERCISE_ID,
    exercise_name: 'Agachamento',
    set_number: 1,
    weight_kg: 80,
    reps_done: 8,
    rpe: null,
    notes: null,
    performed_at: '2026-09-14T10:00:00Z',
    created_at: '2026-09-14T10:00:00Z',
    updated_at: '2026-09-14T10:00:00Z',
    ...overrides,
  };
}
