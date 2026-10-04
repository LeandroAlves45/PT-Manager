import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { describe, expect, it, vi } from 'vitest';

import { checkIn, checkInPage, dayFromToday } from '@/test/msw/account-fixtures';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { CLIENT_ID, clientDetails, clientSummaryOverview } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const ROUTE = `/trainer/clients/${CLIENT_ID}?tab=checkins`;

/** Detalhe do cliente e a tab Check-ins; devolve as queries de `GET /check-ins`. */
function handlers(details = clientDetails()) {
  const queries: URLSearchParams[] = [];
  server.use(
    ...restorableSession(),
    http.get(`${API}/clients/:clientId`, () => HttpResponse.json(details)),
    http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(clientSummaryOverview())),
    http.get(`${API}/clients/:clientId/initial-assessment`, () =>
      HttpResponse.json(problem('resource_not_found'), { status: 404 })
    ),
    http.get(`${API}/check-ins`, ({ request }) => {
      queries.push(new URL(request.url).searchParams);
      return HttpResponse.json(checkInPage([checkIn()]));
    })
  );
  return queries;
}

describe('ClientCheckInsTab', () => {
  it('lists only this client without the client column', async () => {
    const queries = handlers();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Agendado')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Check-ins' })).toHaveAttribute('aria-selected', 'true');
    expect(queries[0]?.get('client_id')).toBe(CLIENT_ID);
    expect(screen.queryByRole('columnheader', { name: 'Cliente' })).not.toBeInTheDocument();
  });

  it('schedules for this client without asking which client', async () => {
    handlers();
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post(`${API}/check-ins`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(checkIn(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Agendar check-in' }));
    const sheet = await screen.findByRole('dialog');
    expect(within(sheet).queryByRole('combobox', { name: 'Cliente' })).not.toBeInTheDocument();
    expect(within(sheet).getByText(/Com Marta Figueiredo/)).toBeInTheDocument();
    await user.type(within(sheet).getByLabelText('Dia do check-in'), dayFromToday(0));
    await user.type(within(sheet).getByLabelText('Data-alvo (opcional)'), dayFromToday(30));
    await user.click(within(sheet).getByRole('button', { name: 'Agendar' }));

    await waitFor(() =>
      expect(body).toEqual({
        client_id: CLIENT_ID,
        check_in_date: dayFromToday(0),
        target_date: dayFromToday(30),
      })
    );
  });

  it('does not offer scheduling for an archived client', async () => {
    handlers(clientDetails({ is_active: false }));
    renderApp({ initialEntries: [ROUTE] });

    await screen.findByText('Agendado');
    expect(screen.queryByRole('button', { name: 'Agendar check-in' })).not.toBeInTheDocument();
  });
});
