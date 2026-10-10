import type { components } from '@/shared/api/schema';

type Schemas = components['schemas'];

/**
 * Fixtures do portal do cliente.
 *
 * Os valores seguem o domínio real: `app_name` por omissão é "PT Manager" e as cores são
 * `null` (`TrainerSettings.cs`), `day_of_week` vai de 0 = segunda a 6 = domingo, e a marca
 * do seed do trainer 1 é "Salgado Performance" com `#E8642A`/`#1F1A17`.
 */

/** Marca sem configuração: o que o backend devolve a um trainer que nunca a editou. */
export function portalBranding(
  overrides: Partial<Schemas['PortalBrandingResponse']> = {}
): Schemas['PortalBrandingResponse'] {
  return {
    app_name: 'PT Manager',
    logo_url: null,
    primary_color: null,
    body_color: null,
    ...overrides,
  };
}

/** Marca do trainer 1 do seed de desenvolvimento. */
export const seededBranding = portalBranding({
  app_name: 'Salgado Performance',
  primary_color: '#E8642A',
  body_color: '#1F1A17',
});

/** Cartão de treino num dia com treino, a meio. */
export function homeWorkout(
  overrides: Partial<Schemas['MyHomeWorkoutResponse']> = {}
): Schemas['MyHomeWorkoutResponse'] {
  return {
    status: 'workout',
    week_number: 1,
    day_of_week: 1,
    day_notes: 'Inferiores',
    exercise_count: 2,
    planned_sets: 7,
    logged_sets: 3,
    is_completed: false,
    next_workout: null,
    ...overrides,
  };
}

/** Home completa de um domingo, 4 de outubro de 2026. */
export function portalHome(
  overrides: Partial<Schemas['MyPortalHomeResponse']> = {}
): Schemas['MyPortalHomeResponse'] {
  return {
    local_date: '2026-10-04',
    workout: homeWorkout(),
    nutrition: {
      meal_plan_id: '6b4f8f5e-4d3c-4f1d-9d8e-0a1b2c3d4e5f',
      name: 'Manutenção - 2400 kcal',
      target_kcal: 2400,
      meal_count: 1,
    },
    supplements: { taken_count: 1, total_count: 1 },
    next_check_in: {
      id: '0f1e2d3c-4b5a-4968-8776-655443322110',
      check_in_date: '2026-10-04',
      is_today: true,
    },
    ...overrides,
  };
}

// Treino de hoje e plano. Terça, 6 de outubro de 2026 (dia de treino do João no
// seed: terças e quintas, semana 1, ciclo de 1 semana).

/** Id da prescrição (`training_plan_day_exercise_id`), usado para registar séries. */
export const PRESCRIPTION_ID = '5a0c1f6e-7b2d-4c8e-9f10-2a3b4c5d6e7f';
/** Id do exercício no catálogo, usado para pedir o vídeo; nunca igual ao da prescrição. */
export const CATALOG_EXERCISE_ID = '8d9e0f1a-2b3c-4d5e-8f60-718293a4b5c6';
/** Id do dia do plano (`training_plan_day_id`), usado ao concluir. */
export const TRAINING_DAY_ID = '3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f';

/** Série planeada para hoje, sem registo. */
export function workoutSet(
  overrides: Partial<Schemas['MyWorkoutSetResponse']> = {}
): Schemas['MyWorkoutSetResponse'] {
  return {
    id: 'b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d01',
    set_number: 1,
    planned_reps: 8,
    planned_weight_kg: 60,
    rest_seconds_min: 90,
    rest_seconds_max: 120,
    planned_rpe: 8,
    logged: null,
    ...overrides,
  };
}

/** Registo de hoje de uma série. */
export function loggedSet(
  overrides: Partial<Schemas['MyLoggedSetResponse']> = {}
): Schemas['MyLoggedSetResponse'] {
  return {
    log_id: 'c0ffee00-1111-4222-8333-444455556666',
    weight_kg: 62.5,
    reps_done: 8,
    rpe: 8.5,
    performed_at: '2026-10-06T09:15:00Z',
    ...overrides,
  };
}

/** Exercício do treino de hoje: agachamento com duas séries, sem vídeo. */
export function workoutExercise(
  overrides: Partial<Schemas['MyWorkoutExerciseResponse']> = {}
): Schemas['MyWorkoutExerciseResponse'] {
  return {
    id: PRESCRIPTION_ID,
    exercise_id: CATALOG_EXERCISE_ID,
    order_number: 1,
    exercise_name: 'Agachamento',
    is_unavailable: false,
    has_ready_video: false,
    exercise_group_id: null,
    group_position: null,
    notes: null,
    is_completed: false,
    sets: [workoutSet(), workoutSet({ id: 'b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d02', set_number: 2 })],
    ...overrides,
  };
}

/** Treino de hoje num dia com treino, ainda sem séries registadas. */
export function workoutToday(
  overrides: Partial<Schemas['MyWorkoutTodayResponse']> = {}
): Schemas['MyWorkoutTodayResponse'] {
  return {
    local_date: '2026-10-06',
    status: 'workout',
    plan_id: '7e8f9a0b-1c2d-4e3f-8a4b-5c6d7e8f9a0b',
    plan_name: 'Força 3x',
    week_number: 1,
    day_of_week: 1,
    day: { id: TRAINING_DAY_ID, notes: 'Inferiores', exercises: [workoutExercise()] },
    progress: { planned_sets: 2, logged_sets: 0, planned_exercises: 1, completed_exercises: 0 },
    completed_at: null,
    next_workout: null,
    ...overrides,
  };
}

/** Plano ativo: terças e quintas da semana 1, como o do João no seed. */
export function trainingPlan(
  overrides: Partial<Schemas['MyTrainingPlanResponse']> = {}
): Schemas['MyTrainingPlanResponse'] {
  const set = (id: string, setNumber: number): Schemas['MyExerciseSetResponse'] => ({
    id,
    set_number: setNumber,
    planned_reps: 8,
    planned_weight_kg: 60,
    rest_seconds_min: 90,
    rest_seconds_max: 120,
    planned_rpe: 8,
  });

  return {
    id: '7e8f9a0b-1c2d-4e3f-8a4b-5c6d7e8f9a0b',
    name: 'Força 3x',
    description: null,
    training_modality: null,
    notes: null,
    start_date: '2026-10-05',
    end_date: null,
    updated_at: '2026-10-05T10:00:00Z',
    days: [
      {
        id: TRAINING_DAY_ID,
        day_of_week: 1,
        week_number: 1,
        notes: 'Inferiores',
        exercises: [
          {
            id: PRESCRIPTION_ID,
            exercise_id: CATALOG_EXERCISE_ID,
            order_number: 1,
            exercise_name: 'Agachamento',
            is_unavailable: false,
            has_ready_video: false,
            exercise_group_id: null,
            group_position: null,
            notes: null,
            sets: [
              set('d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e501', 1),
              set('d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e502', 2),
            ],
          },
        ],
      },
      {
        id: '4d5e6f7a-8b9c-4d0e-9f1a-2b3c4d5e6f70',
        day_of_week: 3,
        week_number: 1,
        notes: null,
        exercises: [
          {
            id: 'e5f6a7b8-c9d0-4e1f-8a2b-3c4d5e6f7a80',
            exercise_id: '9f0a1b2c-3d4e-4f5a-8b6c-7d8e9f0a1b2c',
            order_number: 1,
            exercise_name: 'Supino',
            is_unavailable: false,
            has_ready_video: false,
            exercise_group_id: null,
            group_position: null,
            notes: 'Controla a descida.',
            sets: [set('f6a7b8c9-d0e1-4f2a-9b3c-4d5e6f7a8b01', 1)],
          },
        ],
      },
    ],
    ...overrides,
  };
}
