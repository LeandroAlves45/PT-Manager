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
import { CLIENT_ID, clientPage, clientSummary } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const ROUTE = '/trainer/check-ins';

type CheckIn = ReturnType<typeof checkIn>;

/**
 * Responde a `GET /check-ins` com a lista atual e regista a query string de cada pedido.
 * `items` é mutável: um teste altera-o para simular o estado depois de uma escrita.
 */
function listHandler(state: { items: CheckIn[] }) {
  const queries: URLSearchParams[] = [];
  server.use(
    ...restorableSession(),
    http.get(`${API}/check-ins`, ({ request }) => {
      queries.push(new URL(request.url).searchParams);
      return HttpResponse.json(checkInPage(state.items));
    })
  );
  return queries;
}

/** Abre o menu "Mais ações" pelo teclado (o Radix abre em `pointerdown`). */
async function openMoreActions(user: ReturnType<typeof userEvent.setup>) {
  const trigger = await screen.findByRole('button', { name: /Mais ações do check-in de Marta/ });
  trigger.focus();
  await user.keyboard('{Enter}');
}

describe('CheckInsPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('opens filtered by the dashboard link and shows the client name of each row', async () => {
    const queries = listHandler({ items: [answeredCheckIn()] });
    renderApp({ initialEntries: [`${ROUTE}?status=unreviewed`] });

    expect(await screen.findByRole('link', { name: 'Marta Figueiredo' })).toHaveAttribute(
      'href',
      `/trainer/clients/${CLIENT_ID}?tab=checkins`
    );
    const table = screen.getByRole('table');
    expect(within(table).getByText('Respondido')).toBeInTheDocument();
    expect(within(table).getByText('Por rever')).toBeInTheDocument();
    expect(within(table).getByText('64,8 kg')).toBeInTheDocument();
    expect(screen.getByLabelText('Estado')).toHaveValue('unreviewed');
    expect(queries[0]?.get('status')).toBe('unreviewed');
    expect(queries[0]?.get('page_size')).toBe('25');
    expect(queries[0]?.has('client_id')).toBe(false);
  });

  it('changes the status filter with the contract value and returns to page 1', async () => {
    const queries = listHandler({ items: [checkIn()] });
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?page=2`] });

    await screen.findByText('Agendado');
    await user.selectOptions(screen.getByLabelText('Estado'), 'missed');

    await waitFor(() => expect(queries.at(-1)?.get('status')).toBe('missed'));
    expect(queries.at(-1)?.get('page_number')).toBe('1');
  });

  it('does not send an inverted date range', async () => {
    const queries = listHandler({ items: [checkIn()] });
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await screen.findByText('Agendado');
    await user.type(screen.getByLabelText('De'), '2026-10-20');
    await user.type(screen.getByLabelText('Até'), '2026-10-10');

    expect(
      await screen.findByText('A data inicial não pode ser depois da final.')
    ).toBeInTheDocument();
    expect(queries.at(-1)?.get('from_date')).toBeNull();
    expect(queries.at(-1)?.get('to_date')).toBeNull();
  });

  it('marks an answered check-in as reviewed and refreshes the list', async () => {
    const state = { items: [answeredCheckIn()] };
    const queries = listHandler(state);
    const reviewed: string[] = [];
    server.use(
      http.post(`${API}/check-ins/:checkInId/review`, ({ params }) => {
        reviewed.push(String(params.checkInId));
        state.items = [answeredCheckIn({ reviewed_at: '2026-10-02T10:00:00Z' })];
        return HttpResponse.json(state.items[0]);
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(
      await screen.findByRole('button', { name: /Marcar como revisto o check-in de Marta/ })
    );

    await waitFor(() => expect(reviewed).toEqual([CHECK_IN_ID]));
    expect(toastMock.success).toHaveBeenCalledWith(
      'Check-in de Marta Figueiredo marcado como revisto.'
    );
    await waitFor(() =>
      expect(within(screen.getByRole('table')).queryByText('Por rever')).not.toBeInTheDocument()
    );
    expect(queries.length).toBeGreaterThan(1);
  });

  it('hides reschedule and cancel for a check-in that is not open and in the future', async () => {
    listHandler({
      items: [
        checkIn({ id: 'a', check_in_date: dayFromToday(0) }),
        checkIn({ id: 'b', status: 'missed', check_in_date: dayFromToday(-3) }),
      ],
    });
    renderApp({ initialEntries: [ROUTE] });

    // Dentro da tabela: "Em falta" também é uma opção do filtro e existe antes dos dados.
    await within(await screen.findByRole('table')).findByText('Em falta');
    expect(
      screen.queryByRole('button', { name: /Mais ações do check-in/ })
    ).not.toBeInTheDocument();
  });

  it('schedules a check-in for a chosen client', async () => {
    listHandler({ items: [] });
    let body: Record<string, unknown> | null = null;
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.post(`${API}/check-ins`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(checkIn(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Agendar check-in' }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await user.type(within(sheet).getByLabelText('Dia do check-in'), dayFromToday(7));
    await user.click(within(sheet).getByRole('button', { name: 'Agendar' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toEqual({
      client_id: CLIENT_ID,
      check_in_date: dayFromToday(7),
      target_date: null,
    });
    expect(toastMock.success).toHaveBeenCalledWith('Check-in de Marta Figueiredo agendado.');
  }, 20000);

  it('refuses a past day or a target before the check-in without calling the API', async () => {
    listHandler({ items: [] });
    let posts = 0;
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.post(`${API}/check-ins`, () => {
        posts += 1;
        return HttpResponse.json(checkIn(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Agendar check-in' }));
    const sheet = await screen.findByRole('dialog');
    await user.type(within(sheet).getByLabelText('Dia do check-in'), dayFromToday(-1));
    await user.type(within(sheet).getByLabelText('Data-alvo (opcional)'), dayFromToday(-5));
    await user.click(within(sheet).getByRole('button', { name: 'Agendar' }));

    expect(await within(sheet).findByText('Escolhe o cliente.')).toBeInTheDocument();
    expect(within(sheet).getByText('Escolhe hoje ou um dia futuro.')).toBeInTheDocument();
    expect(
      within(sheet).getByText('A data-alvo não pode ser anterior ao check-in.')
    ).toBeInTheDocument();
    expect(posts).toBe(0);
  });

  it('reschedules and shows a server refusal on the day field', async () => {
    listHandler({ items: [checkIn()] });
    const bodies: unknown[] = [];
    server.use(
      http.patch(`${API}/check-ins/:checkInId/reschedule`, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(problem('check_in_date_conflict'), { status: 409 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await openMoreActions(user);
    await user.click(await screen.findByRole('menuitem', { name: 'Reagendar' }));
    const sheet = await screen.findByRole('dialog');
    const day = within(sheet).getByLabelText('Dia do check-in');
    expect(day).toHaveValue(dayFromToday(7));
    await user.clear(day);
    await user.type(day, dayFromToday(9));
    await user.click(within(sheet).getByRole('button', { name: 'Reagendar' }));

    expect(
      await within(sheet).findByText('O cliente já tem um check-in nesse dia.')
    ).toBeInTheDocument();
    expect(bodies).toEqual([{ check_in_date: dayFromToday(9), target_date: null }]);
  }, 20000);

  it('cancels a future check-in after confirmation', async () => {
    listHandler({ items: [checkIn()] });
    const cancelled: string[] = [];
    server.use(
      http.post(`${API}/check-ins/:checkInId/cancel`, ({ params }) => {
        cancelled.push(String(params.checkInId));
        return HttpResponse.json(checkIn({ status: 'cancelled' }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await openMoreActions(user);
    await user.click(await screen.findByRole('menuitem', { name: 'Cancelar' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(cancelled).toEqual([]);
    await user.click(within(dialog).getByRole('button', { name: 'Cancelar check-in' }));

    await waitFor(() => expect(cancelled).toEqual([CHECK_IN_ID]));
    expect(toastMock.success).toHaveBeenCalledWith('Check-in de Marta Figueiredo cancelado.');
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
  });

  it('shows the answer and saves a full correction', async () => {
    listHandler({ items: [answeredCheckIn()] });
    let body: Record<string, unknown> | null = null;
    server.use(
      http.put(`${API}/check-ins/:checkInId/answer`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(answeredCheckIn({ weight_kg: 64.2 }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: /Ver resposta do check-in/ }));
    const sheet = await screen.findByRole('dialog');
    expect(within(sheet).getByText('Semana boa.')).toBeInTheDocument();
    expect(within(sheet).getByText('Alta')).toBeInTheDocument();
    expect(within(sheet).getByText('Cintura (cm)')).toBeInTheDocument();

    await user.click(within(sheet).getByRole('button', { name: 'Corrigir valores' }));
    const weight = within(sheet).getByLabelText('Peso (kg)');
    await user.clear(weight);
    await user.type(weight, '64.2');
    await user.clear(within(sheet).getByLabelText('Apetite'));
    await user.click(within(sheet).getByRole('button', { name: 'Guardar correção' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({
      target_date: null,
      weight_kg: 64.2,
      body_fat_percentage: 22.5,
      notes: 'Semana boa.',
      training_adherence_score: 90,
      nutrition_adherence_score: 80,
      body_measurements: { waist_cm: 70, hip_cm: null },
      feedback: { appetite: null, energy_levels: 'Alta' },
    });
    expect(toastMock.success).toHaveBeenCalledWith('Check-in de Marta Figueiredo corrigido.');
  }, 20000);

  it('validates the correction locally and maps a server field error', async () => {
    listHandler({ items: [answeredCheckIn()] });
    let puts = 0;
    server.use(
      http.put(`${API}/check-ins/:checkInId/answer`, () => {
        puts += 1;
        return HttpResponse.json(
          {
            ...problem('validation_failed'),
            errors: [{ field: 'WeightKg', code: 'weight_invalid', message: 'Peso inválido.' }],
          },
          { status: 400 }
        );
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: /Ver resposta do check-in/ }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('button', { name: 'Corrigir valores' }));
    const adherence = within(sheet).getByLabelText('Adesão ao treino (%)');
    await user.clear(adherence);
    await user.type(adherence, '120');
    await user.click(within(sheet).getByRole('button', { name: 'Guardar correção' }));

    expect(
      await within(sheet).findByText('Indica um valor inteiro de 0 a 100.')
    ).toBeInTheDocument();
    expect(puts).toBe(0);

    await user.clear(adherence);
    await user.type(adherence, '95');
    await user.click(within(sheet).getByRole('button', { name: 'Guardar correção' }));

    expect(await within(sheet).findByText('Peso inválido.')).toBeInTheDocument();
    expect(puts).toBe(1);
  }, 20000);
});
