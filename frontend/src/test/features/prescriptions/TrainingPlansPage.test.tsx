import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const PLAN_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const CLIENT_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

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
  created_at: '2026-09-01T10:00:00Z',
  updated_at: '2026-09-01T10:00:00Z',
};
const details = {
  ...summary,
  notes: null,
  has_history: true,
  days: [
    {
      id: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      day_of_week: 0,
      week_number: 1,
      notes: null,
      exercises: [
        {
          id: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
          exercise_id: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
          exercise_name: 'Agachamento',
          order_number: 1,
          exercise_group_id: null,
          group_position: null,
          notes: null,
          sets: [
            {
              id: 'ffffffff-ffff-ffff-ffff-ffffffffffff',
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

describe('Training plans', () => {
  it('shows client name from the list and protects a plan with history', async () => {
    const requests: string[] = [];
    server.use(
      ...restorableSession(),
      http.get(API + '/training-plans', ({ request }) => {
        requests.push(new URL(request.url).pathname);
        return HttpResponse.json({
          items: [summary],
          total_count: 1,
          page_number: 1,
          page_size: 25,
        });
      }),
      http.get(API + '/training-plans/:id', () => HttpResponse.json(details)),
      http.get(API + '/exercise-set-logs', () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      )
    );

    renderApp({ initialEntries: ['/trainer/training-plans'] });
    expect(await screen.findByText('Marta Figueiredo')).toBeInTheDocument();
    expect(requests).toHaveLength(1);

    await userEvent.click(screen.getByRole('button', { name: 'Ver plano' }));
    expect(await screen.findAllByText(/A estrutura e as datas/)).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Adicionar dia' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Data de início')).toBeDisabled();
    expect(screen.getByLabelText('Nome')).toBeEnabled();
  });

  it('keeps existing IDs, sends null for a new set, and locks after history conflict', async () => {
    const saves: unknown[] = [];
    const patches: Array<Record<string, unknown>> = [];
    let detailRequests = 0;
    server.use(
      ...restorableSession(),
      http.get(API + '/training-plans', () =>
        HttpResponse.json({ items: [summary], total_count: 1, page_number: 1, page_size: 25 })
      ),
      http.get(API + '/training-plans/:id', () => {
        detailRequests++;
        // Depois do 409 o servidor já reporta histórico: o refetch traz dados novos.
        return HttpResponse.json({ ...details, has_history: saves.length > 0 });
      }),
      http.get(API + '/exercise-set-logs', () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      ),
      http.put(API + '/training-plans/:id', async ({ request }) => {
        saves.push(await request.json());
        return HttpResponse.json(problem('training_structure_has_history'), { status: 409 });
      }),
      http.patch(API + '/training-plans/:id', async ({ request }) => {
        patches.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json({ ...details, name: 'Plano editado' });
      })
    );

    renderApp({ initialEntries: ['/trainer/training-plans'] });
    await userEvent.click(await screen.findByRole('button', { name: 'Ver plano' }));
    await userEvent.click(await screen.findByRole('button', { name: /Adicionar s/ }));
    await userEvent.clear(screen.getByLabelText('Nome'));
    await userEvent.type(screen.getByLabelText('Nome'), 'Plano editado');
    await userEvent.clear(screen.getByLabelText('Data de início'));
    await userEvent.type(screen.getByLabelText('Data de início'), '2026-09-05');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar plano de treino' }));
    await waitFor(() => expect(saves).toHaveLength(1));

    const saved = saves[0] as {
      structure: {
        days: Array<{
          id: string;
          exercises: Array<{ id: string; sets: Array<{ id: string | null }> }>;
        }>;
      };
    };
    expect(saved.structure.days[0]?.id).toBe(details.days[0]?.id);
    expect(saved.structure.days[0]?.exercises[0]?.id).toBe(details.days[0]?.exercises[0]?.id);
    expect(saved.structure.days[0]?.exercises[0]?.sets.map((item) => item.id)).toEqual([
      details.days[0]?.exercises[0]?.sets[0]?.id,
      null,
    ]);
    expect(await screen.findAllByText(/A estrutura e as datas/)).toHaveLength(2);
    expect(screen.queryByRole('button', { name: /Adicionar s/ })).not.toBeInTheDocument();
    // O refetch depois do 409 não repõe o rascunho: o nome escrito sobrevive.
    await waitFor(() => expect(detailRequests).toBe(2));
    expect(screen.getByLabelText('Nome')).toHaveValue('Plano editado');
    // Estrutura e datas ficaram só de consulta: o ecrã volta a mostrar as do servidor,
    // senão o PATCH seguinte reenviava a data alterada e recebia outro 409.
    await waitFor(() => expect(screen.getByLabelText('Data de início')).toHaveValue('2026-09-01'));
    expect(screen.getAllByLabelText('Série')).toHaveLength(1);

    await userEvent.click(screen.getByRole('button', { name: 'Guardar plano de treino' }));
    await waitFor(() => expect(patches).toHaveLength(1));
    expect(patches[0]).toMatchObject({ name: 'Plano editado', start_date: '2026-09-01' });
    expect(patches[0]).not.toHaveProperty('structure');
  });

  it.each(['/trainer/training-plans', '/trainer/meal-plans', '/trainer/supplement-assignments'])(
    'redirects a client away from %s',
    async (path) => {
      server.use(...restorableSession({ role: 'client' }));
      const { router } = renderApp({ initialEntries: [path] });
      await screen.findByText(/Treino de hoje/);
      expect(router.state.location.pathname).toBe('/portal/today');
    }
  );
});
