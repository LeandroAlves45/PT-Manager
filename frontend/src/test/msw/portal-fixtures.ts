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
    planned_reps_max: null,
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
    planned_reps_max: null,
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

/** Nutrição e suplementos. Os macros por alimento são valores reais por 100 g; as
 * contribuições e os totais seguem a conta do backend (`MyNutritionPlanQueries`): gramas a
 * dividir por 100, arredondado a duas casas.
 */

/** Atribuição da creatina, já tomada hoje nas fixtures. */
export const CREATINE_ASSIGNMENT_ID = 'a1b2c3d4-0001-4a5b-8c6d-7e8f9a0b1c2d';
/** Atribuição do ómega 3, por tomar. */
export const OMEGA_ASSIGNMENT_ID = 'a1b2c3d4-0002-4a5b-8c6d-7e8f9a0b1c2d';

/** Plano alimentar ativo com duas refeições; a primeira tem um suplemento associado. */
export function nutritionPlan(
  overrides: Partial<Schemas['MyNutritionPlanResponse']> = {}
): Schemas['MyNutritionPlanResponse'] {
  return {
    id: '6b4f8f5e-4d3c-4f1d-9d8e-0a1b2c3d4e5f',
    name: 'Manutenção - 2400 kcal',
    description: 'Bebe pelo menos 2 litros de água por dia.',
    starts_date: '2026-10-05',
    ends_date: null,
    target_kcal: 2400,
    protein_target_grams: 180,
    carbs_target_grams: 270,
    fats_target_grams: 67,
    actual_totals: {
      protein_grams: 79.25,
      carbs_grams: 99.36,
      fats_grams: 19.13,
      kcal: 925.6,
      fiber_grams: 9.08,
    },
    meals: [
      {
        meal_type: 'Pequeno-almoço',
        order_number: 1,
        totals: {
          protein_grams: 19.4,
          carbs_grams: 57.36,
          fats_grams: 12.2,
          kcal: 433.6,
          fiber_grams: 8.48,
        },
        items: [
          {
            order_number: 1,
            food_name: 'Aveia',
            is_unavailable: false,
            quantity_in_grams: 80,
            contribution: {
              protein_grams: 10.8,
              carbs_grams: 46.96,
              fats_grams: 5.6,
              kcal: 297.6,
              fiber_grams: 8.48,
            },
          },
          {
            order_number: 2,
            food_name: 'Iogurte natural',
            is_unavailable: false,
            quantity_in_grams: 200,
            contribution: {
              protein_grams: 8.6,
              carbs_grams: 10.4,
              fats_grams: 6.6,
              kcal: 136,
              fiber_grams: 0,
            },
          },
        ],
        supplements: [
          {
            order_number: 1,
            supplement_name: 'Creatina',
            is_unavailable: false,
            unit_of_measure: 'g',
            quantity: 5,
            notes: 'Com água.',
          },
        ],
      },
      {
        meal_type: 'Almoço',
        order_number: 2,
        totals: {
          protein_grams: 59.85,
          carbs_grams: 42,
          fats_grams: 6.93,
          kcal: 492,
          fiber_grams: 0.6,
        },
        items: [
          {
            order_number: 1,
            food_name: 'Arroz basmati cozido',
            is_unavailable: false,
            quantity_in_grams: 150,
            contribution: {
              protein_grams: 4.05,
              carbs_grams: 42,
              fats_grams: 0.45,
              kcal: 195,
              fiber_grams: 0.6,
            },
          },
          {
            order_number: 2,
            food_name: 'Peito de frango',
            is_unavailable: false,
            quantity_in_grams: 180,
            contribution: {
              protein_grams: 55.8,
              carbs_grams: 0,
              fats_grams: 6.48,
              kcal: 297,
              fiber_grams: 0,
            },
          },
        ],
        supplements: [],
      },
    ],
    updated_at: '2026-10-05T10:00:00Z',
    ...overrides,
  };
}

/** Tomas de terça, 6 de outubro de 2026: creatina tomada, ómega 3 por tomar (ordem por nome). */
export function supplementIntakesToday(
  overrides: Partial<Schemas['MyTodaySupplementIntakesResponse']> = {}
): Schemas['MyTodaySupplementIntakesResponse'] {
  return {
    local_date: '2026-10-06',
    taken_count: 1,
    total_count: 2,
    items: [
      {
        assignment_id: CREATINE_ASSIGNMENT_ID,
        supplement_name: 'Creatina',
        serving_size: '5 g',
        timing: 'Depois do treino',
        is_taken: true,
        taken_at: '2026-10-06T09:30:00Z',
      },
      {
        assignment_id: OMEGA_ASSIGNMENT_ID,
        supplement_name: 'Ómega 3',
        serving_size: '2 cápsulas',
        timing: 'Ao pequeno-almoço',
        is_taken: false,
        taken_at: null,
      },
    ],
    ...overrides,
  };
}

/** Atribuição ativa tal como a lista paginada a devolve; por omissão, a da creatina. */
export function supplementAssignment(
  overrides: Partial<Schemas['MySupplementAssignmentResponse']> = {}
): Schemas['MySupplementAssignmentResponse'] {
  return {
    id: CREATINE_ASSIGNMENT_ID,
    supplement_id: 'b2c3d4e5-0001-4b6c-9d7e-8f9a0b1c2d3e',
    supplement_name: 'Creatina',
    supplement_description: 'Creatina monohidratada.',
    unit_of_measure: 'g',
    serving_size: '5 g',
    timing: 'Depois do treino',
    trainer_notes: 'Toma todos os dias, também nos de descanso.',
    is_supplement_archived: false,
    updated_at: '2026-10-05T10:00:00Z',
    ...overrides,
  };
}

/** Página única com as duas atribuições das tomas de hoje. */
export function supplementAssignmentsPage(
  items: Schemas['MySupplementAssignmentResponse'][] = [
    supplementAssignment(),
    supplementAssignment({
      id: OMEGA_ASSIGNMENT_ID,
      supplement_id: 'b2c3d4e5-0002-4b6c-9d7e-8f9a0b1c2d3e',
      supplement_name: 'Ómega 3',
      supplement_description: null,
      unit_of_measure: 'cápsula',
      serving_size: '2 cápsulas',
      timing: 'Ao pequeno-almoço',
      trainer_notes: null,
    }),
  ]
): Schemas['PagedResponseOfMySupplementAssignmentResponse'] {
  return { items, total_count: items.length, page_number: 1, page_size: 100 };
}

// Check-ins e perfil. O João do seed: check-in por responder no domingo, 4 de outubro de
// 2026 (o mesmo dia da home), sem contacto de emergência e sem fotografia.

export const DUE_CHECK_IN_ID = '0f1e2d3c-4b5a-4968-8776-655443322110';

/** Check-in de hoje por responder (`GET /portal/my-check-ins/due`): todos os valores vazios. */
export function dueCheckIn(
  overrides: Partial<Schemas['MyCheckInResponse']> = {}
): Schemas['MyCheckInResponse'] {
  return {
    id: DUE_CHECK_IN_ID,
    check_in_date: '2026-10-04',
    target_date: null,
    weight_kg: null,
    body_fat_percentage: null,
    notes: null,
    body_measurements: {
      waist_cm: null,
      hip_cm: null,
      chest_cm: null,
      right_arm_cm: null,
      left_arm_cm: null,
      right_thigh_cm: null,
      left_thigh_cm: null,
      right_calf_cm: null,
      left_calf_cm: null,
    },
    feedback: {
      appetite: null,
      digestion: null,
      training_load: null,
      recovery_sleep: null,
      energy_levels: null,
      body_response: null,
    },
    training_adherence_score: null,
    nutrition_adherence_score: null,
    status: 'scheduled',
    responded_at: null,
    updated_at: '2026-10-01T09:00:00Z',
    ...overrides,
  };
}

/** Próximo check-in agendado (`GET /portal/my-check-ins/next`), duas semanas depois. */
export function nextCheckIn(
  overrides: Partial<Schemas['MyNextCheckInResponse']> = {}
): Schemas['MyNextCheckInResponse'] {
  return {
    id: '1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d',
    check_in_date: '2026-10-18',
    is_today: false,
    ...overrides,
  };
}

/** Perfil do João tal como o seed o cria. */
export function portalProfile(
  overrides: Partial<Schemas['MyProfileResponse']> = {}
): Schemas['MyProfileResponse'] {
  return {
    name: 'João Pereira',
    contact_email: 'joao@ptmanager.local',
    phone: '+351912345678',
    birth_date: '1992-04-17',
    sex: 'male',
    emergency_contact_name: null,
    emergency_contact_phone: null,
    avatar_url: null,
    updated_at: '2026-10-01T09:00:00Z',
    ...overrides,
  };
}
