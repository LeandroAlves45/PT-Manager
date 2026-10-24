import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

import {
  clientHandler,
  DAY_ID,
  details,
  detailsWithBlockedExercise,
  logPage,
  LOG_ID,
  PLAN_ID,
  setLog,
} from './trainingFixtures';

const VIEW = `/trainer/training-plans/${PLAN_ID}`;

function completions(items: unknown[]) {
  return http.get(API + '/workout-completions', () =>
    HttpResponse.json({ items, total_count: items.length, page_number: 1, page_size: 100 })
  );
}

describe('Training plan page', () => {
  it('groups the performed sets by day and exercise with the plan beside, plus the note', async () => {
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () => HttpResponse.json(details())),
      http.get(API + '/exercise-set-logs', () => HttpResponse.json(logPage([setLog()]))),
      completions([
        {
          id: '12121212-1212-1212-1212-121212121212',
          client_id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
          training_plan_id: PLAN_ID,
          training_plan_day_id: DAY_ID,
          local_date: '2026-09-14',
          notes: 'Custou muito na última série',
          completed_at: '2026-09-14T11:00:00Z',
        },
      ])
    );
    renderApp({ initialEntries: [VIEW] });

    expect(await screen.findByRole('heading', { name: 'Plano força' })).toBeInTheDocument();
    const section = within(screen.getByRole('region', { name: 'Séries realizadas' }));
    expect(
      await section.findByText('Série 1 · feito 8 × 80 kg · plano 12-10 × 80 kg')
    ).toBeInTheDocument();
    expect(section.getByRole('heading', { name: 'Agachamento' })).toBeInTheDocument();
    expect(section.getByText('Treino concluído a 14/09/2026')).toBeInTheDocument();
    expect(section.getByText(/Custou muito na última série/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Editar detalhes' })).toHaveAttribute(
      'href',
      `${VIEW}/edit`
    );
    expect(screen.getByRole('link', { name: 'Editar estrutura' })).toHaveAttribute(
      'href',
      `${VIEW}/edit/structure`
    );
  });

  it('marks a blocked exercise with the reason and note', async () => {
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () => HttpResponse.json(detailsWithBlockedExercise())),
      http.get(API + '/exercise-set-logs', () => HttpResponse.json(logPage([]))),
      completions([])
    );
    renderApp({ initialEntries: [VIEW] });

    const structure = within(await screen.findByRole('region', { name: 'Estrutura' }));
    expect(await structure.findByText('Bloqueado')).toBeInTheDocument();
    expect(
      structure.getByText('Motivo: Informação perigosa · Nota: Técnica sem segurança')
    ).toBeInTheDocument();
  });

  it('shows no mark on an exercise the moderation has not blocked', async () => {
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () => HttpResponse.json(details())),
      http.get(API + '/exercise-set-logs', () => HttpResponse.json(logPage([]))),
      completions([])
    );
    renderApp({ initialEntries: [VIEW] });

    const structure = within(await screen.findByRole('region', { name: 'Estrutura' }));
    expect(await structure.findByText('Agachamento')).toBeInTheDocument();
    expect(structure.queryByText('Bloqueado')).not.toBeInTheDocument();
  });

  it('has no correction or edit actions on an archived plan', async () => {
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () =>
        HttpResponse.json(details({ is_archived: true, is_active: false }))
      ),
      http.get(API + '/exercise-set-logs', () => HttpResponse.json(logPage([setLog()]))),
      completions([])
    );
    renderApp({ initialEntries: [VIEW] });

    expect(await screen.findByText(/feito 8 × 80 kg/)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Corrigir/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Editar detalhes' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Registar série/ })).not.toBeInTheDocument();
  });

  it('opens the correction page and patches the log', async () => {
    const patches: Array<Record<string, unknown>> = [];
    let log = setLog();
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () => HttpResponse.json(details())),
      http.get(API + '/exercise-set-logs', () => HttpResponse.json(logPage([log]))),
      completions([]),
      http.patch(API + '/exercise-set-logs/:id', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        patches.push(body);
        log = { ...log, ...body };
        return HttpResponse.json(log);
      })
    );
    const { router } = renderApp({ initialEntries: [VIEW] });
    await userEvent.click(
      await screen.findByRole('link', { name: 'Corrigir Agachamento, série 1' })
    );

    expect(await screen.findByRole('heading', { name: 'Corrigir série' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`${VIEW}/set-logs/${LOG_ID}`);
    await userEvent.clear(screen.getByLabelText('Repetições feitas'));
    await userEvent.type(screen.getByLabelText('Repetições feitas'), '10');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(patches).toHaveLength(1));
    expect(patches[0]).toMatchObject({ reps_done: 10, weight_kg: 80 });
    expect(patches[0]).not.toHaveProperty('training_plan_day_exercise_id');
    await waitFor(() => expect(router.state.location.pathname).toBe(VIEW));
  });

  it('refuses a correction without valid values', async () => {
    const patches: unknown[] = [];
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () => HttpResponse.json(details())),
      http.get(API + '/exercise-set-logs', () => HttpResponse.json(logPage([setLog()]))),
      http.patch(API + '/exercise-set-logs/:id', async ({ request }) => {
        patches.push(await request.json());
        return HttpResponse.json(setLog());
      })
    );
    renderApp({ initialEntries: [`${VIEW}/set-logs/${LOG_ID}`] });
    await userEvent.clear(await screen.findByLabelText('Peso (kg)'));
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText('Indica o peso (0 a 1000 kg).')).toBeInTheDocument();
    expect(patches).toHaveLength(0);
  });

  it('registers a set the client deleted, preselected from ?set=', async () => {
    const posts: Array<Record<string, unknown>> = [];
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () => HttpResponse.json(details())),
      http.post(API + '/exercise-set-logs', async ({ request }) => {
        posts.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json(setLog(), { status: 201 });
      })
    );
    renderApp({
      initialEntries: [`${VIEW}/set-logs/new?set=ffffffff-ffff-ffff-ffff-ffffffffffff`],
    });
    await userEvent.type(await screen.findByLabelText('Peso (kg)'), '80');
    await userEvent.type(screen.getByLabelText('Repetições feitas'), '9');
    await userEvent.click(screen.getByRole('button', { name: 'Registar' }));

    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0]).toMatchObject({
      training_plan_day_exercise_id: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
      set_number: 1,
      weight_kg: 80,
      reps_done: 9,
    });
  });
});
