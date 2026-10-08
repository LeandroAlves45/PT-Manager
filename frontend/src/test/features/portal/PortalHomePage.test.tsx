import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import type { components } from '@/shared/api/schema';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { homeWorkout, portalHome } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

type PortalHome = components['schemas']['MyPortalHomeResponse'];

function useHome(body: PortalHome) {
  server.use(http.get(`${API}/portal/home`, () => HttpResponse.json(body)));
}

async function openHome() {
  server.use(...restorableSession({ role: 'client' }));
  const view = renderApp({ initialEntries: ['/portal'] });
  await screen.findByRole('heading', { name: 'Hoje' });
  return view;
}

function card(name: string): HTMLElement {
  return screen.getByRole('region', { name });
}

describe('PortalHomePage', () => {
  it('shows the four cards of a workout day from a single request', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/portal/home`, () => {
        requests += 1;
        return HttpResponse.json(portalHome());
      })
    );

    await openHome();

    expect(screen.getByText('Domingo, 04/10/2026')).toBeInTheDocument();

    const workout = card('O meu treino de hoje');
    expect(within(workout).getByText('Semana 1 · Terça')).toBeInTheDocument();
    expect(within(workout).getByText('Inferiores')).toBeInTheDocument();
    expect(within(workout).getByText('2 exercícios · 7 séries')).toBeInTheDocument();
    expect(within(workout).getByText('3 de 7 séries · 43 %')).toBeInTheDocument();
    expect(within(workout).getByRole('link', { name: 'Abrir treino' })).toHaveAttribute(
      'href',
      '/portal/today'
    );

    const nutrition = card('Plano alimentar');
    expect(within(nutrition).getByText('Manutenção - 2400 kcal')).toBeInTheDocument();
    expect(within(nutrition).getByText(/2400 kcal · 1 refeição$/)).toBeInTheDocument();
    expect(within(nutrition).getByRole('link', { name: 'Ver plano' })).toHaveAttribute(
      'href',
      '/portal/nutrition'
    );

    const supplements = card('Suplementos');
    expect(within(supplements).getByText('1 de 1 tomadas hoje')).toBeInTheDocument();
    expect(within(supplements).getByText('Tudo tomado.')).toBeInTheDocument();

    const checkIn = card('Check-in');
    expect(within(checkIn).getByText('Tens um check-in para hoje.')).toBeInTheDocument();
    expect(within(checkIn).getByRole('link', { name: 'Responder' })).toHaveAttribute(
      'href',
      '/portal/check-ins'
    );

    expect(requests).toBe(1);
  });

  it('shows the empty state of every card when the client has nothing assigned', async () => {
    useHome(
      portalHome({
        workout: null,
        nutrition: null,
        supplements: { taken_count: 0, total_count: 0 },
        next_check_in: null,
      })
    );

    await openHome();

    expect(
      within(card('O meu treino de hoje')).getByText(
        'O teu personal trainer ainda não atribuiu um plano de treino.'
      )
    ).toBeInTheDocument();
    expect(
      within(card('Plano alimentar')).getByText(
        'O teu personal trainer ainda não te atribuiu um plano alimentar.'
      )
    ).toBeInTheDocument();
    expect(
      within(card('Suplementos')).getByText('Não tens suplementos atribuídos.')
    ).toBeInTheDocument();
    expect(within(card('Check-in')).getByText('Não tens check-ins agendados.')).toBeInTheDocument();
    // Sem dados, nenhum cartão oferece uma ação.
    expect(screen.queryByRole('link', { name: /Abrir treino|Ver plano|Registar|Responder/ })).toBe(
      null
    );
  });

  it('shows a rest day with the next workout and the remaining intakes', async () => {
    useHome(
      portalHome({
        workout: homeWorkout({
          status: 'rest',
          week_number: null,
          day_of_week: null,
          day_notes: null,
          exercise_count: 0,
          planned_sets: 0,
          logged_sets: 0,
          next_workout: { date: '2026-10-06', week_number: 1, day_of_week: 1 },
        }),
        supplements: { taken_count: 1, total_count: 3 },
      })
    );

    await openHome();

    const workout = card('O meu treino de hoje');
    expect(
      within(workout).getByText('Hoje é dia de descanso. Aproveita para descansar e relaxar.')
    ).toBeInTheDocument();
    expect(
      within(workout).getByText('Próximo treino: terça-feira, 06/10 · Semana 1 · Terça')
    ).toBeInTheDocument();
    expect(within(workout).getByRole('link', { name: 'Ver treino' })).toBeInTheDocument();
    expect(within(card('Suplementos')).getByText('2 por tomar.')).toBeInTheDocument();
  });

  it('explains a day outside the plan period', async () => {
    useHome(
      portalHome({
        workout: homeWorkout({ status: 'outside_plan', week_number: null, day_of_week: null }),
      })
    );

    await openHome();

    expect(
      within(card('O meu treino de hoje')).getByText('Hoje está fora do período do teu plano.')
    ).toBeInTheDocument();
  });

  it('summarises a completed workout with singular counts', async () => {
    useHome(
      portalHome({
        workout: homeWorkout({
          day_notes: null,
          exercise_count: 1,
          planned_sets: 1,
          logged_sets: 1,
          is_completed: true,
        }),
      })
    );

    await openHome();

    const workout = card('O meu treino de hoje');
    expect(within(workout).getByText('1 exercício · 1 série')).toBeInTheDocument();
    expect(within(workout).getByText('Treino concluído · 1 de 1 séries')).toBeInTheDocument();
    expect(within(workout).getByRole('link', { name: 'Ver treino' })).toBeInTheDocument();
  });

  it('shows a future check-in without letting the client answer it yet', async () => {
    useHome(
      portalHome({
        next_check_in: { id: 'c-1', check_in_date: '2026-10-08', is_today: false },
      })
    );

    await openHome();

    const checkIn = card('Check-in');
    expect(within(checkIn).getByText('Próximo check-in: quinta-feira, 08/10.')).toBeInTheDocument();
    expect(within(checkIn).getByText('Poderás responder nesse dia.')).toBeInTheDocument();
    expect(within(checkIn).queryByRole('link')).not.toBeInTheDocument();
  });

  it('tells the client the portal is unavailable when the record is no longer active', async () => {
    server.use(
      http.get(`${API}/portal/home`, () =>
        HttpResponse.json(problem('portal_profile_not_available'), { status: 404 })
      )
    );
    server.use(...restorableSession({ role: 'client' }));
    renderApp({ initialEntries: ['/portal'] });

    expect(await screen.findByText('Portal indisponível')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
  });

  it('shows the loading state and then retries after a server error', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/portal/home`, () => {
        requests += 1;
        return requests === 1
          ? HttpResponse.json(problem('internal_error'), { status: 500 })
          : HttpResponse.json(portalHome());
      })
    );
    server.use(...restorableSession({ role: 'client' }));
    renderApp({ initialEntries: ['/portal'] });

    expect(await screen.findByRole('status', { name: 'A carregar o início…' })).toBeInTheDocument();
    await userEvent.click(await screen.findByRole('button', { name: 'Tentar novamente' }));

    expect(await screen.findByRole('heading', { name: 'Hoje' })).toBeInTheDocument();
    expect(requests).toBe(2);
  });

  it('sends a trainer away from the portal without loading the home', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/portal/home`, () => {
        requests += 1;
        return HttpResponse.json(portalHome());
      })
    );
    server.use(...restorableSession({ role: 'trainer', trainer_id: 't-1' }));
    const { router } = renderApp({ initialEntries: ['/portal'] });

    await screen.findByRole('heading', { name: 'Bom treino' });
    expect(router.state.location.pathname).toBe('/trainer');
    expect(requests).toBe(0);
  });

  // Uma rota desconhecida volta à raiz e daí ao Início, sem erro: só seguir o link prova que
  // a ação do cartão abre um ecrã registado no router.
  it.each([
    ['Abrir treino', 'Treino de hoje', '/portal/today'],
    ['Ver plano', 'Nutrição', '/portal/nutrition'],
    ['Registar tomas', 'Suplementos', '/portal/supplements'],
    ['Responder', 'Check-ins', '/portal/check-ins'],
  ])('opens the portal page behind the "%s" action', async (action, heading, pathname) => {
    const { router } = await openHome();

    await userEvent.click(screen.getByRole('link', { name: action }));

    expect(await screen.findByRole('heading', { name: heading })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(pathname);
  });
});
