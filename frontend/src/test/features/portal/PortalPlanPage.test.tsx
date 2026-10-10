import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

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
    server.use(
      http.get(`${API}/portal/my-plan/exercises/:exerciseId/video`, ({ params }) => {
        requested.push(params.exerciseId);
        return HttpResponse.json(problem('exercise_video_storage_unavailable'), { status: 503 });
      })
    );
    const user = userEvent.setup();
    await openPlan();

    expect(screen.queryByRole('button', { name: 'Ver vídeo de Supino' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver vídeo de Agachamento' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'O vídeo não está disponível de momento. Tenta mais tarde.'
    );
    expect(requested).toEqual([CATALOG_EXERCISE_ID]);
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
});
