import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { pastWorkoutDay, workoutDays, workoutToday } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

function openToday(path = '/portal/today') {
  server.use(...restorableSession({ role: 'client' }));
  return renderApp({ initialEntries: [path] });
}

describe('Workout progress of the day', () => {
  it('shows x of y sets with a bar and a text value', async () => {
    server.use(
      http.get(`${API}/portal/my-workout/today`, () =>
        HttpResponse.json(
          workoutToday({
            progress: {
              planned_sets: 12,
              logged_sets: 5,
              planned_exercises: 3,
              completed_exercises: 1,
            },
          })
        )
      )
    );
    openToday();

    expect(await screen.findByText('5 de 12 séries · 42 %')).toBeInTheDocument();
    const bar = screen.getByRole('progressbar', { name: 'Séries registadas' });
    expect(bar).toHaveAttribute('aria-valuenow', '42');
    expect(bar).toHaveAttribute('aria-valuetext', '5 de 12 séries');
  });
});

describe('Workout day filter', () => {
  it('lists today and the past days returned by the API', async () => {
    openToday();

    const select = await screen.findByLabelText('Ver treino de');
    const options = within(select).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual([
      'Hoje',
      'Qui, 01/10 · Semana 1',
      'Ter, 29/09 · Semana 1',
    ]);
    expect(select).toHaveValue('');
    // Hoje continua editável.
    expect(await screen.findByLabelText('Peso da série 1 de Agachamento (kg)')).toBeInTheDocument();
  });

  it('hides the filter when there are no past days', async () => {
    server.use(http.get(`${API}/portal/my-workout/days`, () => HttpResponse.json(workoutDays([]))));
    openToday();

    await screen.findByRole('heading', { name: 'Treino de hoje' });
    expect(screen.queryByLabelText('Ver treino de')).not.toBeInTheDocument();
  });

  it('opens a past day read-only with planned, logged, set note and completion note', async () => {
    const requested: (string | null)[] = [];
    server.use(
      http.get(`${API}/portal/my-workout/day`, ({ request }) => {
        requested.push(new URL(request.url).searchParams.get('date'));
        return HttpResponse.json(pastWorkoutDay());
      })
    );
    openToday('/portal/today?dia=2026-10-01');

    expect(
      await screen.findByRole('heading', { name: 'Treino de 01/10/2026' })
    ).toBeInTheDocument();
    expect(requested).toEqual(['2026-10-01']);
    expect(screen.getByText('Só leitura')).toBeInTheDocument();

    const card = screen.getByRole('region', { name: 'Agachamento' });
    const [first, second] = within(card)
      .getAllByText(/^Série \d$/)
      .map((label) => label.closest('li')!);
    expect(within(first!).getByText('8 reps')).toBeInTheDocument();
    expect(within(first!).getByText('60 kg × 9 reps · RPE 8,5')).toBeInTheDocument();
    expect(within(first!).getByText('Joelho bem')).toBeInTheDocument();
    expect(within(second!).getByText('Sem registo')).toBeInTheDocument();

    expect(screen.getByText('Treino concluído')).toBeInTheDocument();
    expect(screen.getByText('Treino forte')).toBeInTheDocument();
    expect(screen.getByText('1 de 2 séries · 50 %')).toBeInTheDocument();

    // Só leitura: nada para escrever, registar, corrigir, desmarcar ou concluir.
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /Registar|Guardar|Desmarcar|Concluir/ })
    ).not.toBeInTheDocument();
  });

  it('goes to a past day from the filter and back to today by choosing Hoje', async () => {
    const user = userEvent.setup();
    openToday();

    await user.selectOptions(
      await screen.findByLabelText('Ver treino de'),
      'Qui, 01/10 · Semana 1'
    );
    expect(
      await screen.findByRole('heading', { name: 'Treino de 01/10/2026' })
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Ver treino de')).toHaveValue('2026-10-01');
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Ver treino de'), 'Hoje');
    expect(await screen.findByRole('heading', { name: 'Treino de hoje' })).toBeInTheDocument();
    expect(screen.getByLabelText('Peso da série 1 de Agachamento (kg)')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Concluir treino' })).toBeInTheDocument();
  });

  it('returns to today with the link, restoring the editing', async () => {
    const user = userEvent.setup();
    openToday('/portal/today?dia=2026-10-01');

    await user.click(await screen.findByRole('link', { name: 'Voltar a hoje' }));

    expect(await screen.findByRole('heading', { name: 'Treino de hoje' })).toBeInTheDocument();
    expect(screen.getByLabelText('Peso da série 1 de Agachamento (kg)')).toBeInTheDocument();
  });

  it('shows a rest message for a past rest day', async () => {
    server.use(
      http.get(`${API}/portal/my-workout/day`, () =>
        HttpResponse.json(
          pastWorkoutDay({
            status: 'rest',
            day: null,
            week_number: null,
            day_of_week: null,
            completed_at: null,
            completion_notes: null,
          })
        )
      )
    );
    openToday('/portal/today?dia=2026-10-01');

    expect(await screen.findByRole('heading', { name: 'Dia de descanso' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('treats a malformed day in the address as today, without asking for it', async () => {
    let dayRequests = 0;
    server.use(
      http.get(`${API}/portal/my-workout/day`, () => {
        dayRequests += 1;
        return HttpResponse.json(pastWorkoutDay());
      })
    );
    openToday('/portal/today?dia=ontem');

    expect(await screen.findByRole('heading', { name: 'Treino de hoje' })).toBeInTheDocument();
    expect(dayRequests).toBe(0);
  });

  it('treats any 400 for the day as unavailable, also the binder one without a code', async () => {
    server.use(
      http.get(`${API}/portal/my-workout/day`, () =>
        HttpResponse.json(problem('validation_failed', { errors: [] }), { status: 400 })
      )
    );
    openToday('/portal/today?dia=2026-02-31');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Dia indisponível' })
    ).toBeInTheDocument();
    // O seletor diz o mesmo que o ecrã, em vez de «Hoje».
    const select = await screen.findByLabelText('Ver treino de');
    expect(select).toHaveValue('2026-02-31');
    expect(
      within(select).getByRole('option', { name: '2026-02-31 · indisponível' })
    ).toBeDisabled();
  });

  it('explains a day the server refuses and links back to today', async () => {
    server.use(
      http.get(`${API}/portal/my-workout/day`, () =>
        HttpResponse.json(
          problem('validation_failed', {
            errors: [
              { field: 'Date', code: 'portal_workout_day_not_available', message: 'Indisponível.' },
            ],
          }),
          { status: 400 }
        )
      )
    );
    openToday('/portal/today?dia=2099-01-01');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Dia indisponível' })
    ).toBeInTheDocument();
    expect(
      screen.getByText('Só podes consultar os dias passados do teu plano.')
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Voltar a hoje' })).toHaveAttribute(
      'href',
      '/portal/today'
    );
  });
});
