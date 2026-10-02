import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const PLAN_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const CLIENT_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const DAY_ID = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const EXERCISE_ID = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
const LOG_ID = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';

// O editor do plano também tem "Peso (kg)" (peso planeado); o registo vive no seu formulário.
const logForm = () =>
  within(screen.getByRole('heading', { name: 'Registar série' }).closest('form') as HTMLElement);

const summary = {
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
};
const details = {
  ...summary,
  notes: null,
  has_history: false,
  days: [
    {
      id: DAY_ID,
      day_of_week: 0,
      week_number: 1,
      notes: null,
      exercises: [
        {
          id: EXERCISE_ID,
          exercise_id: 'ffffffff-ffff-ffff-ffff-ffffffffffff',
          exercise_name: 'Agachamento',
          order_number: 1,
          exercise_group_id: null,
          group_position: null,
          notes: null,
          sets: [
            {
              id: '99999999-9999-9999-9999-999999999999',
              set_number: 1,
              planned_reps: 8,
              planned_weight_kg: 60,
              rest_seconds_min: 90,
              rest_seconds_max: 120,
              planned_rpe: 8,
            },
          ],
        },
      ],
    },
  ],
};

describe('Set logs', () => {
  it('records and corrects a set without changing its plan exercise identity', async () => {
    const created: unknown[] = [];
    const corrected: unknown[] = [];
    let log: Record<string, unknown> | null = null;
    server.use(
      ...restorableSession(),
      http.get(API + '/training-plans', () =>
        HttpResponse.json({ items: [summary], total_count: 1, page_number: 1, page_size: 25 })
      ),
      http.get(API + '/training-plans/:id', () =>
        HttpResponse.json({ ...details, has_history: log !== null })
      ),
      http.get(API + '/exercise-set-logs', () =>
        HttpResponse.json({
          items: log === null ? [] : [log],
          total_count: log === null ? 0 : 1,
          page_number: 1,
          page_size: 25,
        })
      ),
      http.post(API + '/exercise-set-logs', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        created.push(body);
        log = { ...body, id: LOG_ID, exercise_name: 'Agachamento' };
        return HttpResponse.json(log, { status: 201 });
      }),
      http.patch(API + '/exercise-set-logs/:id', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        corrected.push(body);
        log = { ...log, ...body };
        return HttpResponse.json(log);
      })
    );

    renderApp({ initialEntries: ['/trainer/training-plans'] });
    await userEvent.click(await screen.findByRole('button', { name: 'Ver plano' }));
    await screen.findByText('Nenhuma série registada neste plano.');
    await userEvent.selectOptions(screen.getByLabelText('Exercício'), EXERCISE_ID);
    await userEvent.type(logForm().getByLabelText('Peso (kg)'), '60');
    await userEvent.clear(screen.getByLabelText('Repetições feitas'));
    await userEvent.type(screen.getByLabelText('Repetições feitas'), '8');
    await userEvent.click(screen.getByRole('button', { name: 'Registar' }));
    await waitFor(() => expect(created).toHaveLength(1));
    expect((created[0] as Record<string, unknown>).training_plan_day_exercise_id).toBe(EXERCISE_ID);

    await userEvent.click(await screen.findByRole('button', { name: 'Corrigir' }));
    await userEvent.clear(screen.getByLabelText('Repetições feitas'));
    await userEvent.type(screen.getByLabelText('Repetições feitas'), '10');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar correção' }));
    await waitFor(() => expect(corrected).toHaveLength(1));
    expect((corrected[0] as Record<string, unknown>).reps_done).toBe(10);
    expect(corrected[0]).not.toHaveProperty('training_plan_day_exercise_id');
  });

  it('starts weight and reps empty and refuses to record without them', async () => {
    const created: unknown[] = [];
    server.use(
      ...restorableSession(),
      http.get(API + '/training-plans', () =>
        HttpResponse.json({ items: [summary], total_count: 1, page_number: 1, page_size: 25 })
      ),
      http.get(API + '/training-plans/:id', () => HttpResponse.json(details)),
      http.get(API + '/exercise-set-logs', () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      ),
      http.post(API + '/exercise-set-logs', async ({ request }) => {
        created.push(await request.json());
        return HttpResponse.json({}, { status: 201 });
      })
    );

    renderApp({ initialEntries: ['/trainer/training-plans'] });
    await userEvent.click(await screen.findByRole('button', { name: 'Ver plano' }));
    await screen.findByText('Nenhuma série registada neste plano.');
    expect(logForm().getByLabelText('Peso (kg)')).toHaveValue(null);
    expect(screen.getByLabelText('Repetições feitas')).toHaveValue(null);
    await userEvent.selectOptions(screen.getByLabelText('Exercício'), EXERCISE_ID);
    await userEvent.click(screen.getByRole('button', { name: 'Registar' }));
    expect(created).toHaveLength(0);
  });
});
