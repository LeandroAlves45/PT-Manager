import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  CLIENT_ID,
  clientDetails,
  clientPage,
  clientSummary,
  clientSummaryOverview,
} from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const OTHER_ID = '33333333-3333-3333-3333-333333333334';

describe('ClientsPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('lists a page of clients with one request and the signed-in trainer token', async () => {
    const requests: Request[] = [];
    server.use(
      ...restorableSession({ access_token: 'trainer-b-token' }),
      http.get(`${API}/clients`, ({ request }) => {
        requests.push(request);
        return HttpResponse.json(clientPage([clientSummary()], 1));
      })
    );
    renderApp({ initialEntries: ['/trainer/clients'] });

    const row = (await screen.findByRole('rowheader', { name: 'Marta Figueiredo' })).closest('tr');
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText('marta@example.com')).toBeInTheDocument();
    expect(within(row as HTMLElement).getByText('Recomposição')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Marta Figueiredo' })).toHaveAttribute(
      'href',
      `/trainer/clients/${CLIENT_ID}`
    );
    expect(requests).toHaveLength(1);
    const url = new URL(requests[0]?.url ?? '');
    expect(url.searchParams.get('activity')).toBe('active');
    expect(url.searchParams.get('page_size')).toBe('25');
    expect(requests[0]?.headers.get('authorization')).toBe('Bearer trainer-b-token');
  }, 15000);

  it('distinguishes onboarding from a filtered empty result', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([])))
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer/clients'] });

    expect(await screen.findByText('Ainda não tens clientes')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Arquivados' }));
    expect(await screen.findByText('Sem resultados')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeInTheDocument();
  }, 15000);

  it('treats an empty page beyond the first as no results, not onboarding', async () => {
    const pages: (string | null)[] = [];
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients`, ({ request }) => {
        const page = new URL(request.url).searchParams.get('page_number');
        pages.push(page);
        // 25 clientes: a página 2 ficou vazia depois de arquivar o 26.º.
        return HttpResponse.json(clientPage(page === '1' ? [clientSummary()] : [], 25));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer/clients?page=2'] });

    expect(await screen.findByText('Sem resultados')).toBeInTheDocument();
    expect(screen.queryByText('Ainda não tens clientes')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Limpar filtros' }));
    expect(await screen.findByRole('rowheader', { name: 'Marta Figueiredo' })).toBeInTheDocument();
    expect(pages).toEqual(['2', '1']);
  }, 15000);

  it('validates in PT-PT before sending anything', async () => {
    let posts = 0;
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([]))),
      http.post(`${API}/clients`, () => {
        posts += 1;
        return HttpResponse.json(clientDetails(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer/clients?new=true'] });

    await user.click(await screen.findByRole('button', { name: 'Criar cliente' }));

    expect(await screen.findByText('Indica o nome do cliente.')).toBeInTheDocument();
    expect(screen.getByText('Indica um número de telefone.')).toBeInTheDocument();
    expect(screen.getByText('Indica a data de nascimento.')).toBeInTheDocument();
    // Sem "Feminino" pré-escolhido: o sexo entra no cálculo energético.
    expect(screen.getByLabelText('Sexo biológico')).toHaveValue('');
    expect(screen.getByText('Escolhe o sexo biológico.')).toBeInTheDocument();
    expect(posts).toBe(0);
  }, 15000);

  it('maps a duplicate email conflict to the email field', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([]))),
      http.post(`${API}/clients`, () =>
        HttpResponse.json(problem('client_email_already_exists'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer/clients?new=true'] });

    await user.type(await screen.findByLabelText('Nome'), 'Marta Figueiredo');
    await user.type(screen.getByLabelText('Email de contacto'), 'marta@example.com');
    await user.type(screen.getByLabelText('Telefone'), '+351912345680');
    await user.type(screen.getByLabelText('Data de nascimento'), '1992-04-17');
    await user.selectOptions(screen.getByLabelText('Sexo biológico'), 'female');
    await user.click(screen.getByRole('button', { name: 'Criar cliente' }));

    expect(await screen.findByText('Já existe um cliente com este email.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email de contacto')).toHaveAttribute('aria-invalid', 'true');
  }, 15000);

  it('creates a client with nullable empties and opens the detail', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([]))),
      http.post(`${API}/clients`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(clientDetails(), { status: 201 });
      }),
      http.get(`${API}/clients/:clientId`, () => HttpResponse.json(clientDetails())),
      http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(clientSummaryOverview()))
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/clients?new=true'] });

    await user.type(await screen.findByLabelText('Nome'), 'Marta Figueiredo');
    await user.type(screen.getByLabelText('Telefone'), '+351912345680');
    await user.type(screen.getByLabelText('Data de nascimento'), '1992-04-17');
    await user.selectOptions(screen.getByLabelText('Sexo biológico'), 'female');
    await user.click(screen.getByRole('button', { name: 'Criar cliente' }));

    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/trainer/clients/${CLIENT_ID}`)
    );
    expect(body).toMatchObject({
      name: 'Marta Figueiredo',
      contact_email: null,
      objective: null,
      notes: null,
      sex: 'female',
    });
  }, 15000);

  it('explains the client limit with a link to the subscription', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([]))),
      http.post(`${API}/clients`, () =>
        HttpResponse.json(problem('client_limit_reached'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer/clients?new=true'] });

    await user.type(await screen.findByLabelText('Nome'), 'Marta');
    await user.type(screen.getByLabelText('Telefone'), '912');
    await user.type(screen.getByLabelText('Data de nascimento'), '1992-04-17');
    await user.selectOptions(screen.getByLabelText('Sexo biológico'), 'female');
    await user.click(screen.getByRole('button', { name: 'Criar cliente' }));

    expect(
      await screen.findByText(/Atingiste o limite de clientes do teu plano/)
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver subscrição' })).toHaveAttribute(
      'href',
      '/trainer/billing'
    );
  }, 15000);

  it('forgets the cancelled confirmation before archiving another client', async () => {
    let archived: string | null = null;
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients`, () =>
        HttpResponse.json(
          clientPage([clientSummary(), clientSummary({ id: OTHER_ID, name: 'Tiago Brito' })])
        )
      ),
      http.post(`${API}/clients/:clientId/archive`, ({ params }) => {
        archived = params.clientId as string;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer/clients'] });

    await user.click(await screen.findByRole('button', { name: 'Arquivar Marta Figueiredo' }));
    const first = await screen.findByRole('alertdialog');
    expect(within(first).getByText(/Marta Figueiredo deixa de contar/)).toBeInTheDocument();
    await user.click(within(first).getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Arquivar Tiago Brito' }));
    const second = await screen.findByRole('alertdialog');
    expect(within(second).getByText(/Tiago Brito deixa de contar/)).toBeInTheDocument();
    await user.click(within(second).getByRole('button', { name: 'Arquivar' }));

    await waitFor(() => expect(archived).toBe(OTHER_ID));
    expect(toastMock.success).toHaveBeenCalledWith('Cliente arquivado com sucesso.');
  }, 15000);

  it('translates a reactivation refused by the client limit', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients`, () =>
        HttpResponse.json(clientPage([clientSummary({ is_active: false })]))
      ),
      http.post(`${API}/clients/:clientId/reactivate`, () =>
        HttpResponse.json(problem('client_limit_reached'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer/clients?activity=all'] });

    await user.click(await screen.findByRole('button', { name: 'Reativar Marta Figueiredo' }));
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Reativar' })
    );

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith('Atingiste o limite de clientes do teu plano.')
    );
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
  }, 15000);

  it('redirects a client role away before listing clients', async () => {
    let requests = 0;
    server.use(
      ...restorableSession({ role: 'client', trainer_id: null }),
      http.get(`${API}/clients`, () => {
        requests += 1;
        return HttpResponse.json(clientPage([]));
      })
    );
    const { router } = renderApp({ initialEntries: ['/trainer/clients'] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal/today'));
    expect(requests).toBe(0);
  });
});
