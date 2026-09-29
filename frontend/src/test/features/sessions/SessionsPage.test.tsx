import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { format, formatISO, parseISO } from 'date-fns';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  CLIENT_ID,
  clientPack,
  clientPage,
  clientSummary,
  PACK_ID,
  SESSION_ID,
  sessionPage,
  trainingSession,
} from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const ROUTE = '/trainer/sessions';

/**
 * Abre o menu "Mais ações" pelo teclado: o Radix abre o dropdown em `pointerdown`, que o
 * jsdom não simula de forma fiável (mesma solução do ProfileMenu na 6C).
 */
async function openMoreActions(user: ReturnType<typeof userEvent.setup>) {
  const trigger = await screen.findByRole('button', { name: /Mais ações da sessão de Marta/ });
  trigger.focus();
  await user.keyboard('{Enter}');
}

/** Regista os pedidos a `GET /sessions` e responde com as sessões indicadas. */
function sessionsHandler(items = [trainingSession()]): { urls: URL[] } {
  const urls: URL[] = [];
  server.use(
    ...restorableSession(),
    http.get(`${API}/sessions`, ({ request }) => {
      urls.push(new URL(request.url));
      return HttpResponse.json(sessionPage(items));
    })
  );
  return { urls };
}

describe('SessionsPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('shows today in the day agenda with a one-day window and the client name', async () => {
    const { urls } = sessionsHandler();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Marta Figueiredo')).toBeInTheDocument();
    expect(screen.getByText('PT individual · Estúdio A · 60 min')).toBeInTheDocument();
    const url = urls[0]!;
    // Meia-noite local de hoje, com o offset do browser.
    expect(url.searchParams.get('starts_from')).toBe(
      formatISO(parseISO(format(new Date(), 'yyyy-MM-dd')))
    );
    expect(url.searchParams.get('page_size')).toBe('100');
    expect(url.searchParams.has('client_id')).toBe(false);
    expect(screen.getByRole('button', { name: 'Hoje' })).toHaveAttribute('aria-pressed', 'true');
  }, 15000);

  it('moves to the next day and asks the server for that day', async () => {
    const { urls } = sessionsHandler();
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });
    await screen.findByText('Marta Figueiredo');

    await user.click(screen.getByRole('button', { name: 'Dia seguinte' }));

    await waitFor(() => expect(urls.length).toBe(2));
    const first = new Date(urls[0]!.searchParams.get('starts_from')!);
    const second = new Date(urls[1]!.searchParams.get('starts_from')!);
    expect(second.getTime() - first.getTime()).toBeGreaterThanOrEqual(23 * 3600 * 1000);
    expect(urls[1]!.searchParams.get('starts_from')).toBe(
      urls[0]!.searchParams.get('starts_before')
    );
    expect(screen.getByRole('button', { name: 'Hoje' })).toHaveAttribute('aria-pressed', 'false');
  }, 15000);

  it('filters the list by status with the value published in the contract', async () => {
    const { urls } = sessionsHandler();
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });
    await screen.findByText('Marta Figueiredo');

    await user.click(screen.getByRole('button', { name: 'Lista' }));
    await user.selectOptions(await screen.findByLabelText('Estado'), 'no_show');

    await waitFor(() =>
      expect(urls.some((url) => url.searchParams.get('status') === 'no_show')).toBe(true)
    );
  }, 15000);

  it('registers attendance and refreshes the agenda', async () => {
    const { urls } = sessionsHandler();
    let completed = 0;
    server.use(
      http.post(`${API}/sessions/:sessionId/complete`, ({ params }) => {
        completed += params.sessionId === SESSION_ID ? 1 : 0;
        return HttpResponse.json(trainingSession({ status: 'completed' }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(
      await screen.findByRole('button', { name: /Registar presença de Marta Figueiredo/ })
    );

    await waitFor(() => expect(completed).toBe(1));
    expect(toastMock.success).toHaveBeenCalledWith('Presença registada.');
    await waitFor(() => expect(urls.length).toBeGreaterThan(1));
  }, 15000);

  it('translates a too-early attendance into a clear message', async () => {
    sessionsHandler();
    server.use(
      http.post(`${API}/sessions/:sessionId/complete`, () =>
        HttpResponse.json(problem('session_transition_too_early'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(
      await screen.findByRole('button', { name: /Registar presença de Marta Figueiredo/ })
    );

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith(
        'Só podes registar a presença ou a falta depois da hora de início.'
      )
    );
  }, 15000);

  it('asks for confirmation before registering a client cancellation', async () => {
    sessionsHandler();
    const calls: string[] = [];
    server.use(
      http.post(`${API}/sessions/:sessionId/:action`, ({ params }) => {
        calls.push(String(params.action));
        return HttpResponse.json(trainingSession({ status: 'cancelled_by_client' }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await openMoreActions(user);
    await user.click(await screen.findByRole('menuitem', { name: 'Cancelada pelo cliente' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(calls).toEqual([]);
    await user.click(within(dialog).getByRole('button', { name: 'Registar cancelamento' }));

    await waitFor(() => expect(calls).toEqual(['cancel-by-client']));
    expect(toastMock.success).toHaveBeenCalledWith('Sessão cancelada pelo cliente.');
  }, 15000);

  it('offers only "restore" for a finished session', async () => {
    sessionsHandler([trainingSession({ status: 'no_show' })]);
    let restored = 0;
    server.use(
      http.post(`${API}/sessions/:sessionId/restore`, () => {
        restored += 1;
        return HttpResponse.json(trainingSession());
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Faltou')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Registar presença/ })).not.toBeInTheDocument();
    await openMoreActions(user);
    const restore = await screen.findByRole('menuitem', { name: 'Repor como agendada' });
    expect(screen.queryByRole('menuitem', { name: 'Marcar falta' })).not.toBeInTheDocument();
    await user.click(restore);

    await waitFor(() => expect(restored).toBe(1));
  }, 15000);

  it('schedules a session with the pack that ends first pre-selected', async () => {
    sessionsHandler([]);
    let body: Record<string, unknown> | null = null;
    const secondPack = clientPack({
      id: '66666666-6666-6666-6666-000000000002',
      pack_name: 'Pack 5 sessões',
      expected_end_date: null,
    });
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.get(`${API}/client-session-packs/usable`, ({ request }) =>
        new URL(request.url).searchParams.get('client_id') === CLIENT_ID
          ? HttpResponse.json([clientPack(), secondPack])
          : HttpResponse.json([])
      ),
      http.post(`${API}/sessions`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trainingSession(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Marcar sessão' }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));

    await waitFor(() => expect(within(sheet).getByLabelText('Pack')).toHaveValue(PACK_ID));
    await user.clear(within(sheet).getByLabelText('Dia'));
    await user.type(within(sheet).getByLabelText('Dia'), '2099-01-15');
    await user.type(within(sheet).getByLabelText('Hora'), '10:30');
    await user.click(within(sheet).getByRole('button', { name: 'Marcar sessão' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({
      client_id: CLIENT_ID,
      client_session_pack_id: PACK_ID,
      duration_minutes: 60,
      location: null,
      session_type: null,
      notes: null,
    });
    expect(body!.starts_at).toBe(formatISO(parseISO('2099-01-15T10:30')));
    expect(toastMock.success).toHaveBeenCalledWith('Sessão marcada com Marta Figueiredo.');
  }, 20000);

  it('sends no pack when the trainer picks "Sem pack"', async () => {
    sessionsHandler([]);
    let body: Record<string, unknown> | null = null;
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.get(`${API}/client-session-packs/usable`, () => HttpResponse.json([clientPack()])),
      http.post(`${API}/sessions`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trainingSession(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Marcar sessão' }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await waitFor(() => expect(within(sheet).getByLabelText('Pack')).toHaveValue(PACK_ID));
    await user.selectOptions(within(sheet).getByLabelText('Pack'), 'none');
    await user.clear(within(sheet).getByLabelText('Dia'));
    await user.type(within(sheet).getByLabelText('Dia'), '2099-01-15');
    await user.type(within(sheet).getByLabelText('Hora'), '10:30');
    await user.click(within(sheet).getByRole('button', { name: 'Marcar sessão' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body!.client_session_pack_id).toBeNull();
  }, 20000);

  it("keeps the trainer's pack choice when the usable packs are refetched", async () => {
    sessionsHandler([]);
    let usableRequests = 0;
    const secondPack = clientPack({
      id: '66666666-6666-6666-6666-000000000002',
      pack_name: 'Pack 5 sessões',
    });
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.get(`${API}/client-session-packs/usable`, () => {
        usableRequests += 1;
        // O 2.º pedido traz outro saldo (outra sessão foi concluída entretanto): conteúdo
        // diferente, por isso o TanStack entrega uma referência nova de `data`.
        return HttpResponse.json([
          clientPack({ sessions_remaining: usableRequests === 1 ? 3 : 2 }),
          secondPack,
        ]);
      })
    );
    const user = userEvent.setup();
    const { queryClient } = renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Marcar sessão' }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await waitFor(() => expect(within(sheet).getByLabelText('Pack')).toHaveValue(PACK_ID));
    await user.selectOptions(within(sheet).getByLabelText('Pack'), secondPack.id);

    // O mesmo que acontece ao voltar à janela depois dos 30 s de `staleTime`.
    await queryClient.refetchQueries({ queryKey: ['packs'] });

    await waitFor(() => expect(usableRequests).toBe(2));
    expect(within(sheet).getByLabelText('Pack')).toHaveValue(secondPack.id);
  }, 20000);

  it('puts a same-day conflict on the day field and keeps the sheet open', async () => {
    sessionsHandler([]);
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.get(`${API}/client-session-packs/usable`, () => HttpResponse.json([])),
      http.post(`${API}/sessions`, () =>
        HttpResponse.json(problem('session_client_day_conflict'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Marcar sessão' }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await waitFor(() => expect(within(sheet).getByLabelText('Pack')).toHaveValue('none'));
    await user.clear(within(sheet).getByLabelText('Dia'));
    await user.type(within(sheet).getByLabelText('Dia'), '2099-01-15');
    await user.type(within(sheet).getByLabelText('Hora'), '10:30');
    await user.click(within(sheet).getByRole('button', { name: 'Marcar sessão' }));

    expect(await within(sheet).findByLabelText('Dia')).toHaveAccessibleDescription(
      'Este cliente já tem uma sessão marcada nesse dia.'
    );
    expect(toastMock.success).not.toHaveBeenCalled();
  }, 20000);

  it('rejects a session in the past before calling the API', async () => {
    sessionsHandler([]);
    let posted = false;
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.get(`${API}/client-session-packs/usable`, () => HttpResponse.json([])),
      http.post(`${API}/sessions`, () => {
        posted = true;
        return HttpResponse.json(trainingSession(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Marcar sessão' }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await user.clear(within(sheet).getByLabelText('Dia'));
    await user.type(within(sheet).getByLabelText('Dia'), '2020-01-15');
    await user.type(within(sheet).getByLabelText('Hora'), '10:30');
    await user.click(within(sheet).getByRole('button', { name: 'Marcar sessão' }));

    expect(await within(sheet).findByLabelText('Hora')).toHaveAccessibleDescription(
      'A sessão tem de começar no futuro.'
    );
    expect(posted).toBe(false);
  }, 20000);

  it('redirects a client before requesting any session', async () => {
    let requests = 0;
    server.use(
      ...restorableSession({ role: 'client', trainer_id: null }),
      http.get(`${API}/sessions`, () => {
        requests += 1;
        return HttpResponse.json(sessionPage([]));
      })
    );
    const { router } = renderApp({ initialEntries: [ROUTE] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal/today'));
    expect(requests).toBe(0);
  }, 15000);

  it('opens the tab named in the URL', async () => {
    sessionsHandler();
    server.use(
      http.get(`${API}/client-session-packs`, () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      )
    );
    renderApp({ initialEntries: [`${ROUTE}?tab=packs`] });

    expect(await screen.findByRole('tab', { name: 'Packs dos clientes' })).toHaveAttribute(
      'aria-selected',
      'true'
    );
    expect(await screen.findByText('Sem packs com saldo')).toBeInTheDocument();
  }, 15000);

  // Fecho 6E-2: caminhos reais que o doc 07 não cobria.

  it('shows each final status with its label', async () => {
    sessionsHandler([
      trainingSession({ status: 'cancelled_by_trainer' }),
      trainingSession({ id: '44444444-4444-4444-4444-000000000002', status: 'completed' }),
    ]);
    renderApp({ initialEntries: [ROUTE] });

    // Rótulo de badge sem pontuação, como os outros estados.
    expect(await screen.findByText('Cancelada por ti')).toBeInTheDocument();
    expect(screen.getByText('Realizada')).toBeInTheDocument();
  }, 15000);

  it('marks a no-show only after confirmation', async () => {
    sessionsHandler();
    const calls: string[] = [];
    server.use(
      http.post(`${API}/sessions/:sessionId/:action`, ({ params }) => {
        calls.push(String(params.action));
        return HttpResponse.json(trainingSession({ status: 'no_show' }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await openMoreActions(user);
    await user.click(await screen.findByRole('menuitem', { name: 'Marcar falta' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(calls).toEqual([]);
    await user.click(within(dialog).getByRole('button', { name: 'Marcar falta' }));

    await waitFor(() => expect(calls).toEqual(['no-show']));
    expect(toastMock.success).toHaveBeenCalledWith('Falta registada.');
  }, 15000);

  it('explains a session that no longer exists', async () => {
    sessionsHandler([trainingSession({ status: 'completed' })]);
    server.use(
      http.post(`${API}/sessions/:sessionId/restore`, () =>
        HttpResponse.json(problem('session_not_found'), { status: 404 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await openMoreActions(user);
    await user.click(await screen.findByRole('menuitem', { name: 'Repor como agendada' }));

    await waitFor(() => expect(toastMock.error).toHaveBeenCalledWith('Esta sessão já não existe.'));
  }, 15000);

  it('reschedules keeping the day and sending the new hour with the local offset', async () => {
    // Verão: em Lisboa (fuso dos testes) o offset é +01:00, por isso um "Z" seria outra hora.
    const startsAt = '2099-07-15T10:00:00Z';
    sessionsHandler([trainingSession({ starts_at: startsAt })]);
    let body: Record<string, unknown> | null = null;
    server.use(
      http.patch(`${API}/sessions/:sessionId/reschedule`, async ({ request, params }) => {
        if (params.sessionId !== SESSION_ID) return new HttpResponse(null, { status: 404 });
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trainingSession({ starts_at: startsAt }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await openMoreActions(user);
    await user.click(await screen.findByRole('menuitem', { name: 'Reagendar' }));
    const sheet = await screen.findByRole('dialog');
    const day = format(parseISO(startsAt), 'yyyy-MM-dd');
    expect(within(sheet).getByLabelText('Dia')).toHaveValue(day);
    expect(within(sheet).getByLabelText('Local')).toHaveValue('Estúdio A');
    await user.clear(within(sheet).getByLabelText('Hora'));
    await user.type(within(sheet).getByLabelText('Hora'), '12:15');
    await user.click(within(sheet).getByRole('button', { name: 'Reagendar' }));

    await waitFor(() =>
      expect(body).toEqual({
        starts_at: '2099-07-15T12:15:00+01:00',
        duration_minutes: 60,
        location: 'Estúdio A',
      })
    );
    expect(toastMock.success).toHaveBeenCalledWith('Sessão de Marta Figueiredo reagendada.');
  }, 20000);

  it('puts a schedule conflict on the hour when rescheduling', async () => {
    sessionsHandler([trainingSession({ starts_at: '2099-01-15T10:00:00Z' })]);
    server.use(
      http.patch(`${API}/sessions/:sessionId/reschedule`, () =>
        HttpResponse.json(problem('session_schedule_conflict'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await openMoreActions(user);
    await user.click(await screen.findByRole('menuitem', { name: 'Reagendar' }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('button', { name: 'Reagendar' }));

    expect(await within(sheet).findByLabelText('Hora')).toHaveAccessibleDescription(
      'Já tens outra sessão marcada a essa hora.'
    );
    expect(toastMock.success).not.toHaveBeenCalled();
  }, 20000);

  it('takes a session out of its pack with "Sem pack"', async () => {
    sessionsHandler();
    let body: unknown = undefined;
    server.use(
      http.get(`${API}/client-session-packs/usable`, () => HttpResponse.json([clientPack()])),
      http.patch(`${API}/sessions/:sessionId/pack`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(trainingSession({ client_session_pack_id: null }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await openMoreActions(user);
    await user.click(await screen.findByRole('menuitem', { name: 'Trocar pack' }));
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByLabelText('Pack')).toHaveValue(PACK_ID));
    await user.selectOptions(within(dialog).getByLabelText('Pack'), 'none');
    await user.click(within(dialog).getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(body).toEqual({ client_session_pack_id: null }));
    expect(toastMock.success).toHaveBeenCalledWith('Pack da sessão atualizado.');
  }, 15000);

  it('shows the subscription refusal instead of an empty agenda', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/sessions`, () =>
        HttpResponse.json(problem('subscription_required'), { status: 403 })
      )
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(
      await screen.findByText('Esta funcionalidade exige uma subscrição ativa.')
    ).toBeInTheDocument();
    expect(screen.queryByText('Sem sessões neste dia')).not.toBeInTheDocument();
  }, 15000);

  it('offers a way back when a later page of the list comes back empty', async () => {
    const pages: string[] = [];
    server.use(
      ...restorableSession(),
      http.get(`${API}/sessions`, ({ request }) => {
        const page = new URL(request.url).searchParams.get('page_number') ?? '1';
        pages.push(page);
        // A 2.ª página esvaziou entretanto (sessões canceladas noutro separador).
        return page === '1'
          ? HttpResponse.json(sessionPage([trainingSession()], 30))
          : HttpResponse.json(sessionPage([], 25));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });
    await screen.findByText('Marta Figueiredo');

    await user.click(screen.getByRole('button', { name: 'Lista' }));
    await user.click(await screen.findByRole('button', { name: 'Seguinte' }));
    expect(await screen.findByText('Sem resultados')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Limpar filtros' }));

    expect(await screen.findByText('Marta Figueiredo')).toBeInTheDocument();
    expect(pages.at(-1)).toBe('1');
  }, 20000);

  it('writes the selected tab to the URL and drops it for the agenda', async () => {
    sessionsHandler();
    server.use(
      http.get(`${API}/pack-types`, () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });
    await screen.findByText('Marta Figueiredo');

    await user.click(screen.getByRole('tab', { name: 'Tipos de pack' }));
    await waitFor(() => expect(window.location.search).toBe('?tab=types'));
    await user.click(screen.getByRole('tab', { name: 'Agenda' }));
    await waitFor(() => expect(window.location.search).toBe(''));
  }, 15000);

  it('waits for the chosen client packs before letting the trainer schedule', async () => {
    sessionsHandler([]);
    let releaseUsable: () => void = () => undefined;
    const usableReady = new Promise<void>((resolve) => {
      releaseUsable = resolve;
    });
    let body: Record<string, unknown> | null = null;
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.get(`${API}/client-session-packs/usable`, async () => {
        await usableReady;
        return HttpResponse.json([clientPack()]);
      }),
      http.post(`${API}/sessions`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trainingSession(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Marcar sessão' }));
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await user.clear(within(sheet).getByLabelText('Dia'));
    await user.type(within(sheet).getByLabelText('Dia'), '2099-01-15');
    await user.type(within(sheet).getByLabelText('Hora'), '10:30');

    // Sem os packs ainda: submeter agora marcaria a sessão sem pack, em silêncio.
    const submit = within(sheet).getByRole('button', { name: 'Marcar sessão' });
    expect(submit).toBeDisabled();
    releaseUsable();
    await waitFor(() => expect(within(sheet).getByLabelText('Pack')).toHaveValue(PACK_ID));
    await user.click(submit);

    await waitFor(() => expect(body).not.toBeNull());
    expect(body!.client_session_pack_id).toBe(PACK_ID);
  }, 20000);
});
