import { addDays, format } from 'date-fns';

import type { components } from '@/shared/api/schema';
import { CLIENT_ID } from '@/test/msw/trainer-fixtures';

/**
 * Respostas da API para os testes da 6E-5: check-ins, definições e subscrição.
 *
 * Tipadas pelo `schema.d.ts`, como `trainer-fixtures.ts`. As datas dos check-ins são
 * relativas a hoje: o estado (`scheduled`/`missed`) e as ações dependem do dia.
 */

type Schemas = components['schemas'];

export const CHECK_IN_ID = '99999999-9999-9999-9999-999999999991';

/** Dia relativo a hoje, em `yyyy-MM-dd`. */
export function dayFromToday(days: number): string {
  return format(addDays(new Date(), days), 'yyyy-MM-dd');
}

const EMPTY_MEASUREMENTS: Schemas['BodyMeasurementsPayload'] = {
  waist_cm: null,
  hip_cm: null,
  chest_cm: null,
  right_arm_cm: null,
  left_arm_cm: null,
  right_thigh_cm: null,
  left_thigh_cm: null,
  right_calf_cm: null,
  left_calf_cm: null,
};

const EMPTY_FEEDBACK: Schemas['CheckInFeedbackPayload'] = {
  appetite: null,
  digestion: null,
  training_load: null,
  recovery_sleep: null,
  energy_levels: null,
  body_response: null,
};

/** Check-in agendado daqui a 7 dias, sem resposta. */
export function checkIn(
  overrides: Partial<Schemas['CheckInResponse']> = {}
): Schemas['CheckInResponse'] {
  return {
    id: CHECK_IN_ID,
    client_id: CLIENT_ID,
    client_name: 'Marta Figueiredo',
    check_in_date: dayFromToday(7),
    target_date: null,
    weight_kg: null,
    body_fat_percentage: null,
    notes: null,
    body_measurements: EMPTY_MEASUREMENTS,
    feedback: EMPTY_FEEDBACK,
    training_adherence_score: null,
    nutrition_adherence_score: null,
    status: 'scheduled',
    responded_at: null,
    cancelled_at: null,
    reviewed_at: null,
    created_at: '2026-09-20T10:00:00Z',
    updated_at: '2026-09-20T10:00:00Z',
    ...overrides,
  };
}

/** Check-in respondido ontem e ainda por rever. */
export function answeredCheckIn(
  overrides: Partial<Schemas['CheckInResponse']> = {}
): Schemas['CheckInResponse'] {
  return checkIn({
    check_in_date: dayFromToday(-1),
    status: 'answered',
    weight_kg: 64.8,
    body_fat_percentage: 22.5,
    notes: 'Semana boa.',
    body_measurements: { ...EMPTY_MEASUREMENTS, waist_cm: 70 },
    feedback: { ...EMPTY_FEEDBACK, appetite: 'Normal', energy_levels: 'Alta' },
    training_adherence_score: 90,
    nutrition_adherence_score: 80,
    responded_at: `${dayFromToday(-1)}T09:00:00Z`,
    ...overrides,
  });
}

/** Página de check-ins no envelope `PagedResponse`. */
export function checkInPage(
  items: Schemas['CheckInResponse'][],
  total = items.length,
  pageNumber = 1
): Schemas['PagedResponseOfCheckInResponse'] {
  return { items, total_count: total, page_number: pageNumber, page_size: 25 };
}

/** Definições sem logo, sem cores e sem contactos. */
export function trainerSettings(
  overrides: Partial<Schemas['TrainerSettingsResponse']> = {}
): Schemas['TrainerSettingsResponse'] {
  return {
    app_name: 'PT Marta',
    logo_url: null,
    primary_color: null,
    body_color: null,
    phone: null,
    address: null,
    city: null,
    timezone: 'Europe/Lisbon',
    created_at: '2026-02-04T10:00:00Z',
    updated_at: '2026-02-04T10:00:00Z',
    ...overrides,
  };
}

/** Subscrição gratuita com período experimental a decorrer. */
export function subscription(
  overrides: Partial<Schemas['SubscriptionResponse']> = {}
): Schemas['SubscriptionResponse'] {
  return {
    status: 'ACTIVE',
    tier: 'FREE',
    client_limit: 5,
    current_client_count: 3,
    trial_ends_at: `${dayFromToday(10)}T00:00:00Z`,
    ...overrides,
  };
}
