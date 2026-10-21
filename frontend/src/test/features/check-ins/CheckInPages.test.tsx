import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  answeredCheckIn,
  CHECK_IN_ID,
  checkIn,
  checkInPage,
  dayFromToday,
} from '@/test/msw/account-fixtures';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { CLIENT_ID, clientDetails, clientSummaryOverview } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

/** Lista, ficha do cliente e uma avaliação por id. */
function handlers(item = answeredCheckIn()) {
  server.use(
    ...restorableSession(),
    http.get(`${API}/check-ins/:checkInId`, () => HttpResponse.json(item)),
    http.get(`${API}/check-ins`, () => HttpResponse.json(checkInPage([item]))),
    http.get(`${API}/clients/:clientId`, () => HttpResponse.json(clientDetails())),
    http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(clientSummaryOverview())),
    http.get(`${API}/clients/:clientId/initial-assessment`, () =>
      HttpResponse.json(problem('resource_not_found'), { status: 404 })
    )
  );
}

describe('Evaluation pages', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('uses the new name «Avaliações» in the title, button and empty state', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/check-ins`, () => HttpResponse.json(checkInPage([])))
    );
    renderApp({ initialEntries: ['/trainer/check-ins'] });

    expect(await screen.findByRole('heading', { name: 'Avaliações' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Agendar avaliação' })).toHaveAttribute(
      'href',
      '/trainer/check-ins/new'
    );
    expect(await screen.findByText('Sem avaliações')).toBeInTheDocument();
    expect(screen.queryByText(/check-in/i)).not.toBeInTheDocument();
  });

  it('points the list buttons at the pages, with client_id on the record tab', async () => {
    handlers(checkIn());
    const user = userEvent.setup();
    renderApp({ initialEntries: [`/trainer/clients/${CLIENT_ID}?tab=check-ins`] });

    expect(await screen.findByRole('link', { name: 'Agendar avaliação' })).toHaveAttribute(
      'href',
      `/trainer/check-ins/new?client_id=${CLIENT_ID}`
    );
    const trigger = await screen.findByRole('button', { name: /Mais ações da avaliação de Marta/ });
    trigger.focus();
    await user.keyboard('{Enter}');
    expect(await screen.findByRole('menuitem', { name: 'Reagendar' })).toHaveAttribute(
      'href',
      `/trainer/check-ins/${CHECK_IN_ID}/reschedule?client_id=${CLIENT_ID}`
    );
  }, 20000);

  it('links «Ver resposta» to the detail page', async () => {
    handlers();
    renderApp({ initialEntries: ['/trainer/check-ins'] });

    expect(await screen.findByRole('link', { name: /Ver resposta da avaliação/ })).toHaveAttribute(
      'href',
      `/trainer/check-ins/${CHECK_IN_ID}`
    );
  });

  it('schedules for the client of the record and goes back to its evaluations tab', async () => {
    handlers(checkIn());
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post(`${API}/check-ins`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(checkIn(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({
      initialEntries: [`/trainer/check-ins/new?client_id=${CLIENT_ID}`],
    });

    expect(await screen.findByText(/Com Marta Figueiredo/)).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Cliente' })).not.toBeInTheDocument();
    await user.type(screen.getByLabelText('Dia da avaliação'), dayFromToday(3));
    await user.click(screen.getByRole('button', { name: 'Agendar' }));

    await waitFor(() =>
      expect(body).toEqual({
        client_id: CLIENT_ID,
        check_in_date: dayFromToday(3),
        target_date: null,
      })
    );
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/trainer/clients/${CLIENT_ID}`)
    );
    expect(router.state.location.search).toBe('?tab=check-ins');
  }, 20000);

  it('reschedules by id and goes back to the list', async () => {
    handlers(checkIn());
    const bodies: unknown[] = [];
    server.use(
      http.patch(`${API}/check-ins/:checkInId/reschedule`, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(checkIn({ check_in_date: dayFromToday(9) }));
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({
      initialEntries: [`/trainer/check-ins/${CHECK_IN_ID}/reschedule`],
    });

    const day = await screen.findByLabelText('Dia da avaliação');
    expect(day).toHaveValue(dayFromToday(7));
    await user.clear(day);
    await user.type(day, dayFromToday(9));
    await user.click(screen.getByRole('button', { name: 'Reagendar' }));

    await waitFor(() =>
      expect(bodies).toEqual([{ check_in_date: dayFromToday(9), target_date: null }])
    );
    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer/check-ins'));
    expect(toastMock.success).toHaveBeenCalledWith(
      'Avaliação periódica de Marta Figueiredo reagendada.'
    );
  }, 20000);

  it('does not offer to reschedule an evaluation that is not open and in the future', async () => {
    handlers(checkIn({ check_in_date: dayFromToday(0) }));
    renderApp({ initialEntries: [`/trainer/check-ins/${CHECK_IN_ID}/reschedule`] });

    expect(
      await screen.findByText('Esta avaliação já não pode ser reagendada')
    ).toBeInTheDocument();
  });

  it('asks before leaving the schedule page with unsaved changes', async () => {
    handlers(checkIn());
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/check-ins/new'] });

    await user.type(await screen.findByLabelText('Dia da avaliação'), dayFromToday(3));
    await user.click(screen.getByRole('link', { name: /Voltar a Avaliações/ }));

    const dialog = await screen.findByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'Continuar a editar' }));
    expect(router.state.location.pathname).toBe('/trainer/check-ins/new');
  });

  it('marks the answered evaluation as reviewed from the detail page', async () => {
    handlers();
    server.use(
      http.post(`${API}/check-ins/:checkInId/review`, () =>
        HttpResponse.json(answeredCheckIn({ reviewed_at: '2026-10-02T10:00:00Z' }))
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [`/trainer/check-ins/${CHECK_IN_ID}`] });

    expect(await screen.findByText('Semana boa.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Marcar como revista' }));

    await waitFor(() =>
      expect(toastMock.success).toHaveBeenCalledWith(
        'Avaliação periódica de Marta Figueiredo marcada como revista.'
      )
    );
  });

  it('asks before discarding a correction and before leaving the page with it', async () => {
    handlers();
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`/trainer/check-ins/${CHECK_IN_ID}`] });

    await user.click(await screen.findByRole('button', { name: 'Corrigir valores' }));
    const weight = screen.getByLabelText('Peso (kg)');
    await user.clear(weight);
    await user.type(weight, '60');

    // «Cancelar» com alterações pergunta antes de voltar à leitura.
    await user.click(screen.getByRole('button', { name: 'Cancelar' }));
    const discard = await screen.findByRole('alertdialog');
    await user.click(within(discard).getByRole('button', { name: 'Continuar a editar' }));
    expect(screen.getByLabelText('Peso (kg)')).toHaveValue(60);

    // Sair da página também pede confirmação.
    await user.click(screen.getByRole('link', { name: /Voltar a Avaliações/ }));
    const leave = await screen.findByRole('alertdialog');
    expect(within(leave).getByText('Sair sem guardar?')).toBeInTheDocument();
    await user.click(within(leave).getByRole('button', { name: 'Continuar a editar' }));
    expect(router.state.location.pathname).toBe(`/trainer/check-ins/${CHECK_IN_ID}`);
  }, 20000);

  it('returns to the reading view after saving a correction', async () => {
    handlers();
    server.use(
      http.put(`${API}/check-ins/:checkInId/answer`, () =>
        HttpResponse.json(answeredCheckIn({ weight_kg: 60 }))
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [`/trainer/check-ins/${CHECK_IN_ID}`] });

    await user.click(await screen.findByRole('button', { name: 'Corrigir valores' }));
    const weight = screen.getByLabelText('Peso (kg)');
    await user.clear(weight);
    await user.type(weight, '60');
    await user.click(screen.getByRole('button', { name: 'Guardar correção' }));

    expect(await screen.findByRole('button', { name: 'Corrigir valores' })).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  }, 20000);
});
