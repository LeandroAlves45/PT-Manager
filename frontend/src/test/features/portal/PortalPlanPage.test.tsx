import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { portalKeys } from '@/features/portal';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { CATALOG_EXERCISE_ID, trainingPlan } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

async function openPlan() {
  server.use(...restorableSession({ role: 'client' }));
  renderApp({ initialEntries: ['/portal/plan'] });
  await screen.findByRole('heading', { name: 'Força 3x' });
}

describe('PortalPlanPage', () => {
  it('lists week, day, exercise and planned sets, read-only', async () => {
    await openPlan();

    expect(screen.getByText('Desde 05/10/2026')).toBeInTheDocument();
    const week = screen.getByRole('region', { name: 'Semana 1' });
    const days = within(week).getAllByRole('article');
    expect(days).toHaveLength(2);

    const tuesday = days[0]!;
    expect(within(tuesday).getByRole('heading', { name: 'Terça' })).toBeInTheDocument();
    expect(within(tuesday).getByText('1 exercício · 2 séries')).toBeInTheDocument();
    expect(within(tuesday).getByText('Agachamento')).toBeInTheDocument();
    expect(within(tuesday).getByText('2 séries · 8 reps · 90–120 s')).toBeInTheDocument();
    expect(
      within(tuesday).getByText('Série 2 · 8 reps · 60 kg · RPE 8 · 90–120 s')
    ).toBeInTheDocument();

    const thursday = days[1]!;
    expect(within(thursday).getByRole('heading', { name: 'Quinta' })).toBeInTheDocument();
    expect(within(thursday).getByText('Controla a descida.')).toBeInTheDocument();

    // Só leitura: nada para escrever nem registar.
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Registar/ })).not.toBeInTheDocument();
  });

  it('offers the video only where the API says it is ready', async () => {
    const plan = trainingPlan();
    const [tuesday, thursday] = plan.days;
    server.use(
      http.get(`${API}/portal/my-plan`, () =>
        HttpResponse.json({
          ...plan,
          days: [
            {
              ...tuesday!,
              exercises: [{ ...tuesday!.exercises[0]!, has_ready_video: true }],
            },
            thursday!,
          ],
        })
      )
    );
    const requested: unknown[] = [];
    let storageDown = true;
    server.use(
      http.get(`${API}/portal/my-plan/exercises/:exerciseId/video`, ({ params }) => {
        requested.push(params.exerciseId);
        return storageDown
          ? HttpResponse.json(problem('exercise_video_storage_unavailable'), { status: 503 })
          : HttpResponse.json({
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
    await openPlan();

    expect(screen.queryByRole('button', { name: 'Ver vídeo de Supino' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver vídeo de Agachamento' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'O vídeo não está disponível de momento. Tenta novamente mais tarde.'
    );
    expect(requested).toEqual([CATALOG_EXERCISE_ID]);

    // Falha passageira: o cliente repete sem sair do ecrã.
    storageDown = false;
    await user.click(
      screen.getByRole('button', { name: 'Tentar novamente o vídeo de Agachamento' })
    );
    expect(await screen.findByLabelText('Vídeo de Agachamento')).toBeInTheDocument();
    expect(requested).toHaveLength(2);
  });

  it('groups the days by week and shows the period and decimal loads of the plan', async () => {
    const plan = trainingPlan();
    const [tuesday, thursday] = plan.days;
    const first = tuesday!.exercises[0]!;
    server.use(
      http.get(`${API}/portal/my-plan`, () =>
        HttpResponse.json({
          ...plan,
          end_date: '2026-11-01',
          days: [
            {
              ...tuesday!,
              exercises: [
                {
                  ...first,
                  sets: [{ ...first.sets[0]!, planned_weight_kg: 62.5, planned_rpe: 8.5 }],
                },
              ],
            },
            { ...thursday!, week_number: 2 },
          ],
        })
      )
    );

    await openPlan();

    expect(screen.getByText('05/10/2026 a 01/11/2026')).toBeInTheDocument();
    const firstWeek = screen.getByRole('region', { name: 'Semana 1' });
    expect(within(firstWeek).getAllByRole('article')).toHaveLength(1);
    expect(
      within(firstWeek).getByText('Série 1 · 8 reps · 62,5 kg · RPE 8,5 · 90–120 s')
    ).toBeInTheDocument();
    const secondWeek = screen.getByRole('region', { name: 'Semana 2' });
    expect(within(secondWeek).getByRole('heading', { name: 'Quinta' })).toBeInTheDocument();
  });

  it('says so when the plan has no training days yet', async () => {
    server.use(
      http.get(`${API}/portal/my-plan`, () => HttpResponse.json({ ...trainingPlan(), days: [] }))
    );

    await openPlan();

    expect(screen.getByText('O plano ainda não tem dias de treino.')).toBeInTheDocument();
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
  });

  it('shows an empty state when there is no active plan', async () => {
    server.use(
      http.get(`${API}/portal/my-plan`, () =>
        HttpResponse.json(problem('portal_training_plan_not_available'), { status: 404 })
      ),
      ...restorableSession({ role: 'client' })
    );

    renderApp({ initialEntries: ['/portal/plan'] });

    expect(await screen.findByRole('heading', { name: 'Sem plano de treino' })).toBeInTheDocument();
  });

  it('is reached from the workout of the day and links back to it', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/today'] });

    await user.click(await screen.findByRole('link', { name: 'Ver plano completo' }));
    expect(await screen.findByRole('heading', { name: 'Força 3x' })).toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: 'Treino de hoje' }));
    expect(await screen.findByRole('heading', { name: 'Treino de hoje' })).toBeInTheDocument();
  });

  it('keeps what was on screen and warns when a refresh fails, then recovers on retry', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const { queryClient } = renderApp({ initialEntries: ['/portal/plan'] });
    await screen.findByRole('heading', { name: 'Força 3x' });

    server.use(
      http.get(`${API}/portal/my-plan`, () =>
        HttpResponse.json(problem('Internal server error'), { status: 500 })
      )
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.plan() });

    expect(
      await screen.findByText(
        'Não foi possível atualizar. Estás a ver os últimos dados carregados.'
      )
    ).toBeInTheDocument();
    expect(screen.getByText('Desde 05/10/2026')).toBeInTheDocument();
    // É um aviso, não o ecrã de erro.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    server.use(http.get(`${API}/portal/my-plan`, () => HttpResponse.json(trainingPlan())));
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    await waitFor(() =>
      expect(
        screen.queryByText('Não foi possível atualizar. Estás a ver os últimos dados carregados.')
      ).not.toBeInTheDocument()
    );
    expect(screen.getByText('Desde 05/10/2026')).toBeInTheDocument();
  });

  it('drops the cached data when the refresh says the plan is gone', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const { queryClient } = renderApp({ initialEntries: ['/portal/plan'] });
    await screen.findByRole('heading', { name: 'Força 3x' });

    server.use(
      http.get(`${API}/portal/my-plan`, () =>
        HttpResponse.json(problem('portal_training_plan_not_available'), { status: 404 })
      )
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.plan() });

    expect(await screen.findByRole('heading', { name: 'Sem plano de treino' })).toBeInTheDocument();
    expect(screen.queryByText('Desde 05/10/2026')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
  });
});
