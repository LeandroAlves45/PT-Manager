import type { components } from '@/shared/api/schema';

/**
 * Respostas da API do trainer usadas pelos testes da 6E.
 *
 * Tipadas pelo `schema.d.ts`: se o contrato mudar, os testes deixam de compilar em vez de
 * testarem uma forma que o backend já não devolve.
 */

type Schemas = components['schemas'];

export const CLIENT_ID = '33333333-3333-3333-3333-333333333333';
export const SESSION_ID = '44444444-4444-4444-4444-444444444444';

/** Painel com todos os blocos preenchidos. */
export function dashboardResponse(
  overrides: Partial<Schemas['TrainerDashboardResponse']> = {}
): Schemas['TrainerDashboardResponse'] {
  return {
    local_today: '2026-09-16',
    active_client_count: 12,
    check_ins_pending_review: {
      total_count: 7,
      overdue_count: 3,
      items: [
        {
          check_in_id: '55555555-5555-5555-5555-555555555551',
          client_id: CLIENT_ID,
          client_name: 'João Carvalho',
          check_in_date: '2026-09-14',
          responded_at: '2026-09-14T09:00:00Z',
        },
        {
          check_in_id: '55555555-5555-5555-5555-555555555552',
          client_id: CLIENT_ID,
          client_name: 'Marta Figueiredo',
          check_in_date: '2026-09-11',
          responded_at: '2026-09-11T09:00:00Z',
        },
      ],
    },
    packs_ending: {
      total_count: 1,
      items: [
        {
          pack_id: '66666666-6666-6666-6666-666666666666',
          client_id: CLIENT_ID,
          client_name: 'João Carvalho',
          pack_name: 'Pack 10 sessões',
          sessions_total: 10,
          sessions_remaining: 1,
          expected_end_date: '2026-09-30',
        },
      ],
    },
    plans_expiring: {
      total_count: 4,
      training_plan_count: 3,
      meal_plan_count: 1,
      training_plans: [],
      meal_plans: [],
    },
    sessions_today: {
      total_count: 2,
      next_session_starts_at: '2026-09-16T11:00:00Z',
      items: [
        {
          session_id: SESSION_ID,
          client_id: CLIENT_ID,
          client_name: 'Marta Figueiredo',
          starts_at: '2026-09-16T11:00:00Z',
          duration_minutes: 60,
          session_type: 'PT individual',
          location: '',
          status: 'scheduled',
        },
        {
          session_id: '44444444-4444-4444-4444-444444444445',
          client_id: CLIENT_ID,
          client_name: 'Tiago Brito',
          starts_at: '2026-09-16T08:00:00Z',
          duration_minutes: 45,
          session_type: null,
          location: null,
          status: 'completed',
        },
      ],
    },
    clients_without_training_plan: {
      total_count: 1,
      items: [
        {
          client_id: CLIENT_ID,
          client_name: 'Rita Sá',
          has_had_plan: true,
          without_plan_since: '2026-09-07',
          days_without_plan: 9,
        },
      ],
    },
    pack_sales: {
      current_month: {
        year: 2026,
        month: 9,
        totals: [{ currency: 'EUR', amount_cents: 248000, pack_count: 8 }],
      },
      previous_month: {
        year: 2026,
        month: 8,
        totals: [{ currency: 'EUR', amount_cents: 200000, pack_count: 7 }],
      },
    },
    ...overrides,
  };
}

/** Linha da tabela de clientes. */
export function clientSummary(
  overrides: Partial<Schemas['ClientSummaryResponse']> = {}
): Schemas['ClientSummaryResponse'] {
  return {
    id: CLIENT_ID,
    name: 'Marta Figueiredo',
    contact_email: 'marta@example.com',
    phone: '+351912345680',
    birth_date: '1992-04-17',
    sex: 'female',
    objective: 'Recomposição',
    is_active: true,
    created_at: '2026-02-04T10:00:00Z',
    updated_at: '2026-02-04T10:00:00Z',
    ...overrides,
  };
}

/** Página de clientes no envelope `PagedResponse`. */
export function clientPage(
  items: Schemas['ClientSummaryResponse'][],
  total = items.length
): Schemas['PagedResponseOfClientSummaryResponse'] {
  return { items, total_count: total, page_number: 1, page_size: 25 };
}

/** Ficha completa do cliente, sem conta no portal e com um pack. */
export function clientDetails(
  overrides: Partial<Schemas['ClientDetailsResponse']> = {}
): Schemas['ClientDetailsResponse'] {
  return {
    id: CLIENT_ID,
    user_id: null,
    name: 'Marta Figueiredo',
    contact_email: 'marta@example.com',
    phone: '+351912345680',
    birth_date: '1992-04-17',
    sex: 'female',
    objective: 'recomposição',
    notes: null,
    emergency_contact_name: null,
    emergency_contact_phone: null,
    avatar_url: null,
    is_active: true,
    usable_packs: [
      {
        id: '66666666-6666-6666-6666-666666666666',
        pack_type_id: '77777777-7777-7777-7777-777777777777',
        name: 'Pack 10 sessões',
        sessions_total: 10,
        sessions_remaining: 3,
        price_cents: 30000,
        currency: 'EUR',
        purchase_date: '2026-09-01',
        expected_end_date: '2026-09-30',
        created_at: '2026-09-01T10:00:00Z',
      },
    ],
    created_at: '2026-02-04T10:00:00Z',
    updated_at: '2026-02-04T10:00:00Z',
    ...overrides,
  };
}

/** Resumo 6B completo. */
export function clientSummaryOverview(
  overrides: Partial<Schemas['ClientSummaryOverviewResponse']> = {}
): Schemas['ClientSummaryOverviewResponse'] {
  return {
    client_id: CLIENT_ID,
    local_today: '2026-09-16',
    weight: {
      current_kg: 64.8,
      measured_on: '2026-09-11',
      source: 'check_in',
      change_kg: -1.4,
      change_since: '2026-07-22',
    },
    height_cm: 168,
    training_plan: {
      id: '88888888-8888-8888-8888-888888888888',
      name: 'Força 4× / semana',
      start_date: '2026-09-01',
      end_date: '2026-09-30',
      days_per_week: 4,
    },
    adherence: {
      window_start: '2026-08-20',
      window_end: '2026-09-16',
      planned_sets: 112,
      logged_sets: 96,
      percentage: 86,
    },
    nutrition: {
      meal_plan_id: '99999999-9999-9999-9999-999999999999',
      name: 'Défice ligeiro',
      target_kcal: 2200,
      protein_target_grams: 140,
      carbs_target_grams: 180,
      fats_target_grams: 70,
    },
    packs: { usable_pack_count: 1, sessions_remaining: 3, next_expected_end_date: '2026-09-30' },
    ...overrides,
  };
}
