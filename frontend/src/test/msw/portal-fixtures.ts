import type { components } from '@/shared/api/schema';

type Schemas = components['schemas'];

/**
 * Fixtures do portal do cliente (6F).
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
