import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { formatISO, parseISO } from 'date-fns';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  CLIENT_ID,
  clientDetails,
  clientPack,
  clientPage,
  clientSummary,
  clientSummaryOverview,
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

/** Ficha do cliente e as listas que a tab «Sessões» pede, para a página poder voltar a ela. */
function clientHandlers() {
  server.use(
    ...restorableSession(),
    http.get(`${API}/clients/:clientId`, () => HttpResponse.json(clientDetails())),
    http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(clientSummaryOverview())),
    http.get(`${API}/clients/:clientId/initial-assessment`, () =>
      HttpResponse.json(problem('resource_not_found'), { status: 404 })
    ),
    http.get(`${API}/sessions`, () => HttpResponse.json(sessionPage([trainingSession()]))),
    http.get(`${API}/client-session-packs`, () =>
      HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
    ),
    http.get(`${API}/client-session-packs/usable`, () => HttpResponse.json([clientPack()]))
  );
}

describe('SessionEditorPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('schedules for the client of the record without asking which client, then goes back to it', async () => {
    clientHandlers();
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post(`${API}/sessions`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trainingSession(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({
      initialEntries: [`/trainer/sessions/new?client_id=${CLIENT_ID}`],
    });

    expect(await screen.findByText(/Com Marta Figueiredo/)).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Cliente' })).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Pack')).toHaveValue(PACK_ID));
    await user.clear(screen.getByLabelText('Dia'));
    await user.type(screen.getByLabelText('Dia'), '2099-01-15');
    await user.type(screen.getByLabelText('Hora'), '10:30');
    await user.click(screen.getByRole('button', { name: 'Marcar sessão' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({
      client_id: CLIENT_ID,
      client_session_pack_id: PACK_ID,
      duration_minutes: 60,
    });
    expect(body!.starts_at).toBe(formatISO(parseISO('2099-01-15T10:30')));
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/trainer/clients/${CLIENT_ID}`)
    );
    expect(router.state.location.search).toBe('?tab=sessions');
  }, 20000);

  it('lets the trainer pick the client without client_id and goes back to the agenda', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/sessions`, () => HttpResponse.json(sessionPage([]))),
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.get(`${API}/client-session-packs/usable`, () => HttpResponse.json([]))
    );
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post(`${API}/sessions`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trainingSession(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/sessions/new'] });

    await user.click(await screen.findByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await waitFor(() => expect(screen.getByLabelText('Pack')).toHaveValue('none'));
    await user.clear(screen.getByLabelText('Dia'));
    await user.type(screen.getByLabelText('Dia'), '2099-01-15');
    await user.type(screen.getByLabelText('Hora'), '10:30');
    await user.click(screen.getByRole('button', { name: 'Marcar sessão' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({ client_id: CLIENT_ID, client_session_pack_id: null });
    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer/sessions'));
  }, 20000);

  it('pre-fills the day opened in the agenda', async () => {
    server.use(...restorableSession());
    renderApp({ initialEntries: ['/trainer/sessions/new?date=2099-03-04'] });

    expect(await screen.findByLabelText('Dia')).toHaveValue('2099-03-04');
  });

  it('asks before leaving with unsaved changes and stays when the trainer keeps editing', async () => {
    server.use(...restorableSession());
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/sessions/new'] });

    await user.type(await screen.findByLabelText('Local'), 'Ginásio');
    await user.click(screen.getByRole('link', { name: /Voltar a Sessões e packs/ }));

    const dialog = await screen.findByRole('alertdialog');
    expect(within(dialog).getByText('Sair sem guardar?')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Continuar a editar' }));
    expect(router.state.location.pathname).toBe('/trainer/sessions/new');
    expect(screen.getByLabelText('Local')).toHaveValue('Ginásio');
  });

  it('leaves without asking when nothing was changed', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/sessions`, () => HttpResponse.json(sessionPage([])))
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/sessions/new'] });

    await user.click(await screen.findByRole('link', { name: /Voltar a Sessões e packs/ }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer/sessions'));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('reschedules the session loaded by id, with the day and place pre-filled', async () => {
    const startsAt = '2099-07-15T10:00:00Z';
    server.use(
      ...restorableSession(),
      http.get(`${API}/sessions/:sessionId`, ({ params }) =>
        params.sessionId === SESSION_ID
          ? HttpResponse.json(trainingSession({ starts_at: startsAt }))
          : new HttpResponse(null, { status: 404 })
      ),
      http.get(`${API}/sessions`, () => HttpResponse.json(sessionPage([])))
    );
    let body: Record<string, unknown> | null = null;
    server.use(
      http.patch(`${API}/sessions/:sessionId/reschedule`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trainingSession({ starts_at: startsAt }));
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`/trainer/sessions/${SESSION_ID}`] });

    expect(await screen.findByRole('heading', { name: 'Reagendar sessão' })).toBeInTheDocument();
    expect(screen.getByLabelText('Dia')).toHaveValue('2099-07-15');
    expect(screen.getByLabelText('Local')).toHaveValue('Estúdio A');
    await user.clear(screen.getByLabelText('Hora'));
    await user.type(screen.getByLabelText('Hora'), '12:15');
    await user.click(screen.getByRole('button', { name: 'Reagendar' }));

    await waitFor(() =>
      expect(body).toEqual({
        starts_at: '2099-07-15T12:15:00+01:00',
        duration_minutes: 60,
        location: 'Estúdio A',
      })
    );
    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer/sessions'));
  }, 20000);

  it('does not offer to reschedule a session that is no longer scheduled', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/sessions/:sessionId`, () =>
        HttpResponse.json(trainingSession({ status: 'completed' }))
      )
    );
    renderApp({ initialEntries: [`/trainer/sessions/${SESSION_ID}`] });

    expect(await screen.findByText('Esta sessão já não pode ser reagendada')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reagendar' })).not.toBeInTheDocument();
  });

  it('points the agenda buttons at the new routes', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/sessions`, () => HttpResponse.json(sessionPage([trainingSession()])))
    );
    renderApp({ initialEntries: ['/trainer/sessions'] });

    const link = await screen.findByRole('link', { name: 'Marcar sessão' });
    expect(link.getAttribute('href')).toMatch(/^\/trainer\/sessions\/new\?date=\d{4}-\d{2}-\d{2}$/);
  });

  it('points the record tab buttons at the pages with client_id', async () => {
    clientHandlers();
    const user = userEvent.setup();
    renderApp({ initialEntries: [`/trainer/clients/${CLIENT_ID}?tab=sessions`] });

    expect(await screen.findByRole('link', { name: /Marcar sessão/ })).toHaveAttribute(
      'href',
      `/trainer/sessions/new?client_id=${CLIENT_ID}`
    );
    expect(screen.getByRole('link', { name: /Vender pack/ })).toHaveAttribute(
      'href',
      `/trainer/clients/${CLIENT_ID}/sell-pack`
    );
    const trigger = await screen.findByRole('button', { name: /Mais ações da sessão de Marta/ });
    trigger.focus();
    await user.keyboard('{Enter}');
    expect(await screen.findByRole('menuitem', { name: 'Reagendar' })).toHaveAttribute(
      'href',
      `/trainer/sessions/${SESSION_ID}?client_id=${CLIENT_ID}`
    );
  }, 20000);
});
