import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { describe, expect, it, vi } from 'vitest';

import { portalKeys } from '@/features/portal';
import type { components } from '@/shared/api/schema';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import {
  CATALOG_EXERCISE_ID,
  loggedSet,
  portalHome,
  PRESCRIPTION_ID,
  TRAINING_DAY_ID,
  workoutExercise,
  workoutSet,
  workoutToday,
} from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

type WorkoutToday = components['schemas']['MyWorkoutTodayResponse'];

function useToday(body: WorkoutToday) {
  server.use(http.get(`${API}/portal/my-workout/today`, () => HttpResponse.json(body)));
}

/** Treino com a 1.ª série já registada hoje (62,5 kg × 8, RPE 8,5). */
function todayWithFirstSetLogged(overrides: Partial<WorkoutToday> = {}): WorkoutToday {
  return workoutToday({
    day: {
      id: TRAINING_DAY_ID,
      notes: 'Inferiores',
      exercises: [
        workoutExercise({
          sets: [
            workoutSet({ logged: loggedSet() }),
            workoutSet({ id: 'b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d02', set_number: 2 }),
          ],
        }),
      ],
    },
    progress: { planned_sets: 2, logged_sets: 1, planned_exercises: 1, completed_exercises: 0 },
    ...overrides,
  });
}

async function openToday() {
  server.use(...restorableSession({ role: 'client' }));
  const view = renderApp({ initialEntries: ['/portal/today'] });
  await screen.findByRole('heading', { name: 'Treino de hoje' });
  // A home em cache prova que cada escrita a invalida (o cartão resume o treino).
  view.queryClient.setQueryData(portalKeys.home(), portalHome());
  return view;
}

function weightOf(setNumber: number): HTMLInputElement {
  return screen.getByLabelText(`Peso da série ${setNumber} de Agachamento (kg)`);
}

describe('PortalTodayPage', () => {
  it('shows the workout of the day from a single request', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/portal/my-workout/today`, () => {
        requests += 1;
        return HttpResponse.json(workoutToday());
      })
    );

    await openToday();

    expect(screen.getByText('Terça-feira, 06/10/2026')).toBeInTheDocument();
    expect(screen.getByText('Força 3x')).toBeInTheDocument();
    expect(screen.getByText('Semana 1 · Terça')).toBeInTheDocument();
    expect(screen.getByText('Inferiores')).toBeInTheDocument();
    expect(screen.getByText('1 exercício · 2 séries')).toBeInTheDocument();
    expect(screen.getByText('0 de 2 séries · 0 %')).toBeInTheDocument();

    const card = screen.getByRole('region', { name: 'Agachamento' });
    expect(within(card).getByText('2 séries · 8 reps · 90–120 s')).toBeInTheDocument();
    // Pré-preenchido com o planeado: um toque regista o que o personal trainer prescreveu.
    expect(weightOf(1)).toHaveValue('60');
    expect(screen.getByLabelText('Repetições da série 1 de Agachamento')).toHaveValue('8');
    expect(screen.getByRole('link', { name: 'Ver plano completo' })).toHaveAttribute(
      'href',
      '/portal/plan'
    );
    // Sem `has_ready_video`, não há botão que levaria a um 404.
    expect(screen.queryByRole('button', { name: /Ver vídeo/ })).not.toBeInTheDocument();
    expect(requests).toBe(1);
  });

  it('records a new set with POST and refreshes the workout and the home card', async () => {
    let state = workoutToday();
    const bodies: unknown[] = [];
    server.use(
      http.get(`${API}/portal/my-workout/today`, () => HttpResponse.json(state)),
      http.post(`${API}/portal/exercise-set-logs`, async ({ request }) => {
        bodies.push(await request.json());
        state = todayWithFirstSetLogged();
        return HttpResponse.json({});
      })
    );
    const user = userEvent.setup();
    const { queryClient } = await openToday();

    await user.clear(weightOf(1));
    await user.type(weightOf(1), '62,5');
    await user.click(screen.getByRole('button', { name: 'Registar série 1 de Agachamento' }));

    expect(
      await screen.findByRole('button', { name: 'Desmarcar série 1 de Agachamento' })
    ).toHaveAttribute('aria-pressed', 'true');
    expect(bodies).toEqual([
      {
        training_plan_day_exercise_id: PRESCRIPTION_ID,
        set_number: 1,
        weight_kg: 62.5,
        reps_done: 8,
        rpe: null,
        notes: null,
      },
    ]);
    expect(screen.getByText('1 de 2 séries · 50 %')).toBeInTheDocument();
    expect(queryClient.getQueryState(portalKeys.home())?.isInvalidated).toBe(true);
  });

  it('corrects a logged set with PATCH, keeping its RPE, and never POSTs again', async () => {
    useToday(todayWithFirstSetLogged());
    const patches: unknown[] = [];
    const posts = vi.fn();
    server.use(
      http.patch(`${API}/portal/exercise-set-logs/:logId`, async ({ request, params }) => {
        patches.push({ id: params.logId, body: await request.json() });
        return HttpResponse.json({});
      }),
      http.post(`${API}/portal/exercise-set-logs`, () => {
        posts();
        return HttpResponse.json({});
      })
    );
    const user = userEvent.setup();
    await openToday();

    expect(weightOf(1)).toHaveValue('62,5');
    await user.clear(weightOf(1));
    await user.type(weightOf(1), '65');
    await user.click(screen.getByRole('button', { name: 'Guardar série 1 de Agachamento' }));

    await waitFor(() => expect(patches).toHaveLength(1));
    expect(patches[0]).toEqual({
      id: loggedSet().log_id,
      body: { weight_kg: 65, reps_done: 8, rpe: 8.5, notes: null },
    });
    expect(posts).not.toHaveBeenCalled();
  });

  it('unmarks a logged set with DELETE', async () => {
    let state = todayWithFirstSetLogged();
    const deleted: unknown[] = [];
    server.use(
      http.get(`${API}/portal/my-workout/today`, () => HttpResponse.json(state)),
      http.delete(`${API}/portal/exercise-set-logs/:logId`, ({ params }) => {
        deleted.push(params.logId);
        state = workoutToday();
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    await openToday();

    await user.click(screen.getByRole('button', { name: 'Desmarcar série 1 de Agachamento' }));

    expect(
      await screen.findByRole('button', { name: 'Registar série 1 de Agachamento' })
    ).toBeInTheDocument();
    expect(deleted).toEqual([loggedSet().log_id]);
  });

  it('sends a single POST when the set is tapped twice while saving', async () => {
    const posts = vi.fn();
    server.use(
      http.post(`${API}/portal/exercise-set-logs`, async () => {
        posts();
        await delay(50);
        return HttpResponse.json({});
      })
    );
    const user = userEvent.setup();
    await openToday();

    const button = screen.getByRole('button', { name: 'Registar série 1 de Agachamento' });
    await user.click(button);
    await user.click(button);

    await waitFor(() => expect(button).toBeEnabled());
    expect(posts).toHaveBeenCalledTimes(1);
  });

  it('keeps the typed values when saving fails, so the client can retry', async () => {
    server.use(
      http.post(`${API}/portal/exercise-set-logs`, () =>
        HttpResponse.json(problem('training_date_outside_plan'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    await openToday();

    await user.clear(weightOf(1));
    await user.type(weightOf(1), '62,5');
    await user.click(screen.getByRole('button', { name: 'Registar série 1 de Agachamento' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Hoje está fora do período do teu plano.'
    );
    expect(weightOf(1)).toHaveValue('62,5');
  });

  it('validates the values before sending anything', async () => {
    const posts = vi.fn();
    server.use(
      http.post(`${API}/portal/exercise-set-logs`, () => {
        posts();
        return HttpResponse.json({});
      })
    );
    const user = userEvent.setup();
    await openToday();

    await user.clear(weightOf(1));
    await user.type(weightOf(1), '1200');
    await user.click(screen.getByRole('button', { name: 'Registar série 1 de Agachamento' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Indica o peso (0 a 1000 kg');
    expect(weightOf(1)).toHaveAttribute('aria-invalid', 'true');
    expect(posts).not.toHaveBeenCalled();
  });

  it('completes a partial workout with notes after confirming', async () => {
    let state = todayWithFirstSetLogged();
    const bodies: unknown[] = [];
    toastMock.success.mockClear();
    server.use(
      http.get(`${API}/portal/my-workout/today`, () => HttpResponse.json(state)),
      http.post(`${API}/portal/workout-completions`, async ({ request }) => {
        bodies.push(await request.json());
        state = todayWithFirstSetLogged({ completed_at: '2026-10-06T10:00:00Z' });
        return HttpResponse.json({});
      })
    );
    const user = userEvent.setup();
    await openToday();

    await user.click(screen.getByRole('button', { name: 'Concluir treino' }));
    const dialog = await screen.findByRole('dialog', { name: 'Concluir treino' });
    expect(within(dialog).getByText(/Faltam 1 série por registar/)).toBeInTheDocument();
    await user.type(
      within(dialog).getByLabelText('Notas para o teu personal trainer (opcional)'),
      'Senti-me bem'
    );
    await user.click(within(dialog).getByRole('button', { name: 'Concluir treino' }));

    expect(
      await screen.findByText(/Treino concluído · 1 de 2 séries registadas/)
    ).toBeInTheDocument();
    expect(bodies).toEqual([{ training_plan_day_id: TRAINING_DAY_ID, notes: 'Senti-me bem' }]);
    expect(toastMock.success).toHaveBeenCalledWith('Treino concluído. Bom trabalho!');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Concluir treino' })).not.toBeInTheDocument();
  });

  it('blocks unmarking after completion but still allows corrections', async () => {
    useToday(todayWithFirstSetLogged({ completed_at: '2026-10-06T10:00:00Z' }));
    const user = userEvent.setup();
    await openToday();

    expect(screen.getByRole('button', { name: 'Desmarcar série 1 de Agachamento' })).toBeDisabled();

    await user.clear(weightOf(1));
    await user.type(weightOf(1), '65');
    expect(screen.getByRole('button', { name: 'Guardar série 1 de Agachamento' })).toBeEnabled();
  });

  it('shows the next workout on a rest day', async () => {
    useToday(
      workoutToday({
        status: 'rest',
        week_number: 1,
        day_of_week: 2,
        day: null,
        next_workout: { date: '2026-10-08', week_number: 1, day_of_week: 3 },
      })
    );

    await openToday();

    expect(screen.getByRole('heading', { name: 'Dia de descanso' })).toBeInTheDocument();
    expect(
      screen.getByText('Próximo treino: quinta-feira, 08/10 · Semana 1 · Quinta.')
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Concluir treino' })).not.toBeInTheDocument();
  });

  it('explains a day outside the plan period', async () => {
    useToday(
      workoutToday({
        status: 'outside_plan',
        week_number: null,
        day_of_week: null,
        day: null,
        next_workout: null,
      })
    );

    await openToday();

    expect(screen.getByRole('heading', { name: 'Fora do período do plano' })).toBeInTheDocument();
    expect(screen.getByText('Hoje está fora do período do teu plano.')).toBeInTheDocument();
  });

  it('shows an empty state, not an error, when there is no active plan', async () => {
    server.use(
      http.get(`${API}/portal/my-workout/today`, () =>
        HttpResponse.json(problem('portal_training_plan_not_available'), { status: 404 })
      ),
      ...restorableSession({ role: 'client' })
    );

    renderApp({ initialEntries: ['/portal/today'] });

    expect(await screen.findByRole('heading', { name: 'Sem plano de treino' })).toBeInTheDocument();
    expect(screen.getByText('O teu treinador ainda não atribuiu um plano.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
  });

  it('plays the video with the catalog id, not the prescription id', async () => {
    useToday(
      workoutToday({
        day: {
          id: TRAINING_DAY_ID,
          notes: null,
          exercises: [workoutExercise({ has_ready_video: true })],
        },
      })
    );
    const requested: unknown[] = [];
    server.use(
      http.get(`${API}/portal/my-plan/exercises/:exerciseId/video`, ({ params }) => {
        requested.push(params.exerciseId);
        return HttpResponse.json({
          video_id: '0a1b2c3d-4e5f-4a6b-8c7d-8e9f0a1b2c3d',
          exercise_id: CATALOG_EXERCISE_ID,
          content_type: 'video/mp4',
          duration_milliseconds: 42000,
          width: 1280,
          height: 720,
          playback_url: 'https://media.test/agachamento.mp4?signature=1',
          expires_at: '2026-10-06T10:30:00Z',
        });
      })
    );
    const user = userEvent.setup();
    await openToday();

    await user.click(screen.getByRole('button', { name: 'Ver vídeo de Agachamento' }));

    expect(await screen.findByLabelText('Vídeo de Agachamento')).toHaveAttribute(
      'src',
      'https://media.test/agachamento.mp4?signature=1'
    );
    expect(requested).toEqual([CATALOG_EXERCISE_ID]);
  });

  it('explains a video that stopped being available', async () => {
    useToday(
      workoutToday({
        day: {
          id: TRAINING_DAY_ID,
          notes: null,
          exercises: [workoutExercise({ has_ready_video: true })],
        },
      })
    );
    server.use(
      http.get(`${API}/portal/my-plan/exercises/:exerciseId/video`, () =>
        HttpResponse.json(problem('exercise_video_not_found'), { status: 404 })
      )
    );
    const user = userEvent.setup();
    await openToday();

    await user.click(screen.getByRole('button', { name: 'Ver vídeo de Agachamento' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'O vídeo deixou de estar disponível.'
    );
  });

  it('collapses completed exercises and shows a blocked one in Portuguese', async () => {
    useToday(
      workoutToday({
        day: {
          id: TRAINING_DAY_ID,
          notes: null,
          exercises: [
            workoutExercise({
              is_completed: true,
              sets: [workoutSet({ logged: loggedSet() })],
            }),
            workoutExercise({
              id: '6b7c8d9e-0f1a-4b2c-8d3e-4f5a6b7c8d9e',
              exercise_name: 'Unavailable exercise',
              is_unavailable: true,
              sets: [workoutSet({ id: 'b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d09' })],
            }),
          ],
        },
      })
    );
    const user = userEvent.setup();
    await openToday();

    const done = within(screen.getByRole('region', { name: 'Agachamento' })).getByRole('button', {
      expanded: false,
    });
    expect(screen.getByLabelText('Exercício completo')).toBeInTheDocument();
    await user.click(done);
    expect(done).toHaveAttribute('aria-expanded', 'true');

    expect(screen.getByRole('region', { name: 'Exercício indisponível' })).toBeInTheDocument();
    expect(screen.queryByText('Unavailable exercise')).not.toBeInTheDocument();
  });
});
