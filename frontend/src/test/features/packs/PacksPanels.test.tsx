import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { addDays, format } from 'date-fns';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  CLIENT_ID,
  clientPack,
  clientPackPage,
  clientPage,
  clientSummary,
  PACK_ID,
  PACK_TYPE_ID,
  packType,
  packTypePage,
} from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const TYPES_ROUTE = '/trainer/sessions?tab=types';
const PACKS_ROUTE = '/trainer/sessions?tab=packs';

/** Sessão restaurada e listas de tipos e packs; devolve os URLs pedidos. */
function packHandlers({
  types = [packType()],
  packs = [clientPack()],
}: {
  types?: ReturnType<typeof packType>[];
  packs?: ReturnType<typeof clientPack>[];
} = {}): { typeUrls: URL[] } {
  const typeUrls: URL[] = [];
  server.use(
    ...restorableSession(),
    http.get(`${API}/sessions`, () =>
      HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 100 })
    ),
    http.get(`${API}/pack-types`, ({ request }) => {
      typeUrls.push(new URL(request.url));
      return HttpResponse.json(packTypePage(types));
    }),
    http.get(`${API}/client-session-packs`, () => HttpResponse.json(clientPackPage(packs)))
  );
  return { typeUrls };
}

describe('Pack types and client packs', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('lists pack types with price and duration, and filters archived ones', async () => {
    const { typeUrls } = packHandlers();
    const user = userEvent.setup();
    renderApp({ initialEntries: [TYPES_ROUTE] });

    const row = (await screen.findByRole('rowheader', { name: 'Pack 10 sessões' })).closest('tr')!;
    expect(within(row).getByText('300,00 €')).toBeInTheDocument();
    expect(within(row).getByText('90 dias')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Arquivados' }));

    await waitFor(() =>
      expect(typeUrls.some((url) => url.searchParams.get('activity') === 'archived')).toBe(true)
    );
  }, 15000);

  it('creates a pack type with the price in cents and the currency in capitals', async () => {
    packHandlers({ types: [] });
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post(`${API}/pack-types`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(packType(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [TYPES_ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Criar tipo de pack' }));
    const sheet = await screen.findByRole('dialog');
    await user.type(within(sheet).getByLabelText('Nome'), 'Pack 12');
    await user.type(within(sheet).getByLabelText('Número de sessões'), '12');
    // Uma só casa decimal: "300,5" são 300,50 € e não 300,05 €.
    await user.type(within(sheet).getByLabelText('Preço'), '300,5');
    await user.clear(within(sheet).getByLabelText('Moeda'));
    await user.type(within(sheet).getByLabelText('Moeda'), 'eur');
    await user.click(within(sheet).getByRole('button', { name: 'Criar tipo de pack' }));

    await waitFor(() =>
      expect(body).toEqual({
        name: 'Pack 12',
        session_count: 12,
        price_cents: 30050,
        currency: 'EUR',
        expected_duration_days: null,
      })
    );
    expect(toastMock.success).toHaveBeenCalledWith('Tipo de pack criado com sucesso.');
  }, 15000);

  it('shows the server validation on the matching field', async () => {
    packHandlers();
    server.use(
      http.patch(`${API}/pack-types/:packTypeId`, () =>
        HttpResponse.json(
          problem('validation_failed', {
            errors: [
              {
                field: 'SessionCount',
                code: 'pack_type_session_count_must_be_positive',
                message: 'Tem de ser positivo.',
              },
            ],
          }),
          { status: 400 }
        )
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [TYPES_ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Editar Pack 10 sessões' }));
    const sheet = await screen.findByRole('dialog');
    expect(within(sheet).getByLabelText('Preço')).toHaveValue('300,00');
    await user.click(within(sheet).getByRole('button', { name: 'Guardar alterações' }));

    expect(await within(sheet).findByLabelText('Número de sessões')).toHaveAccessibleDescription(
      'Tem de ser positivo.'
    );
  }, 15000);

  it('archives a pack type only after confirmation', async () => {
    packHandlers();
    let archived = 0;
    server.use(
      http.post(`${API}/pack-types/:packTypeId/archive`, () => {
        archived += 1;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [TYPES_ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Arquivar Pack 10 sessões' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(archived).toBe(0);
    await user.click(within(dialog).getByRole('button', { name: 'Arquivar' }));

    await waitFor(() => expect(archived).toBe(1));
    expect(toastMock.success).toHaveBeenCalledWith('Tipo de pack arquivado com sucesso.');
  }, 15000);

  it('shows sold packs with client, balance and sale price', async () => {
    packHandlers();
    renderApp({ initialEntries: [PACKS_ROUTE] });

    const row = (await screen.findByRole('link', { name: 'Marta Figueiredo' })).closest('tr')!;
    expect(within(row).getByText('3 de 10 restantes')).toBeInTheDocument();
    expect(within(row).getByText('300,00 €')).toBeInTheDocument();
    expect(within(row).getByText('30/09/2026')).toBeInTheDocument();
    // Pack já usado: não se cancela.
    expect(within(row).queryByRole('button', { name: /^Cancelar/ })).not.toBeInTheDocument();
  }, 15000);

  it('sells a pack suggesting the end date from the type duration', async () => {
    packHandlers({ packs: [] });
    let body: Record<string, unknown> | null = null;
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.post(`${API}/client-session-packs`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(clientPack({ sessions_remaining: 10 }), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [PACKS_ROUTE] });

    await user.click((await screen.findAllByRole('button', { name: 'Vender pack' }))[0]!);
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await waitFor(() => expect(within(sheet).getByLabelText('Tipo de pack')).toBeEnabled());
    await user.selectOptions(within(sheet).getByLabelText('Tipo de pack'), PACK_TYPE_ID);

    const today = format(new Date(), 'yyyy-MM-dd');
    const suggested = format(addDays(new Date(), 90), 'yyyy-MM-dd');
    expect(within(sheet).getByLabelText('Fim previsto')).toHaveValue(suggested);
    await user.click(within(sheet).getByRole('button', { name: 'Vender pack' }));

    await waitFor(() =>
      expect(body).toEqual({
        client_id: CLIENT_ID,
        pack_type_id: PACK_TYPE_ID,
        purchase_date: today,
        expected_end_date: suggested,
      })
    );
    expect(toastMock.success).toHaveBeenCalledWith('Pack vendido a Marta Figueiredo.');
  }, 20000);

  it('explains why an archived client cannot receive a pack', async () => {
    packHandlers({ packs: [] });
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.post(`${API}/client-session-packs`, () =>
        HttpResponse.json(problem('client_inactive'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [PACKS_ROUTE] });

    await user.click((await screen.findAllByRole('button', { name: 'Vender pack' }))[0]!);
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await waitFor(() => expect(within(sheet).getByLabelText('Tipo de pack')).toBeEnabled());
    await user.selectOptions(within(sheet).getByLabelText('Tipo de pack'), PACK_TYPE_ID);
    await user.click(within(sheet).getByRole('button', { name: 'Vender pack' }));

    expect(await within(sheet).findByRole('alert')).toHaveTextContent(
      'Este cliente está arquivado. Reativa-o antes de lhe atribuir um pack.'
    );
  }, 20000);

  it('cancels an unused pack and explains a refusal', async () => {
    packHandlers({ packs: [clientPack({ sessions_remaining: 10 })] });
    server.use(
      http.post(`${API}/client-session-packs/:packId/cancel`, ({ params }) =>
        params.packId === PACK_ID
          ? HttpResponse.json(problem('client_session_pack_referenced'), { status: 409 })
          : new HttpResponse(null, { status: 204 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [PACKS_ROUTE] });

    await user.click(
      await screen.findByRole('button', { name: 'Cancelar Pack 10 sessões (Marta Figueiredo)' })
    );
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Cancelar pack' })
    );

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith(
        'Há sessões associadas a este pack. Tira-as do pack antes de o cancelar.'
      )
    );
  }, 15000);

  it('refuses an end date before the purchase date without calling the API', async () => {
    packHandlers();
    let patched = false;
    server.use(
      http.patch(`${API}/client-session-packs/:packId/expected-end-date`, () => {
        patched = true;
        return HttpResponse.json(clientPack());
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [PACKS_ROUTE] });

    await user.click(
      await screen.findByRole('button', {
        name: 'Alterar fim previsto de Pack 10 sessões (Marta Figueiredo)',
      })
    );
    const dialog = await screen.findByRole('dialog');
    await user.clear(within(dialog).getByLabelText('Fim previsto'));
    await user.type(within(dialog).getByLabelText('Fim previsto'), '2026-08-01');
    await user.click(within(dialog).getByRole('button', { name: 'Guardar' }));

    expect(within(dialog).getByLabelText('Fim previsto')).toHaveAccessibleDescription(
      'O fim previsto não pode ser antes da data de compra.'
    );
    expect(patched).toBe(false);
  }, 15000);

  it('removes the end date when the field is emptied', async () => {
    packHandlers();
    let body: unknown = undefined;
    server.use(
      http.patch(`${API}/client-session-packs/:packId/expected-end-date`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(clientPack({ expected_end_date: null }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [PACKS_ROUTE] });

    await user.click(
      await screen.findByRole('button', {
        name: 'Alterar fim previsto de Pack 10 sessões (Marta Figueiredo)',
      })
    );
    const dialog = await screen.findByRole('dialog');
    await user.clear(within(dialog).getByLabelText('Fim previsto'));
    await user.click(within(dialog).getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(body).toEqual({ expected_end_date: null }));
    expect(toastMock.success).toHaveBeenCalledWith('Fim previsto removido.');
  }, 15000);

  // Fecho 6E-2: caminhos reais que o doc 07 não cobria.

  it('reactivates an archived pack type', async () => {
    const { typeUrls } = packHandlers({ types: [packType({ is_active: false })] });
    let reactivated = 0;
    server.use(
      http.post(`${API}/pack-types/:packTypeId/reactivate`, ({ params }) => {
        reactivated += params.packTypeId === PACK_TYPE_ID ? 1 : 0;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [TYPES_ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Arquivados' }));
    await waitFor(() =>
      expect(typeUrls.some((url) => url.searchParams.get('activity') === 'archived')).toBe(true)
    );
    await user.click(await screen.findByRole('button', { name: 'Reativar Pack 10 sessões' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(reactivated).toBe(0);
    await user.click(within(dialog).getByRole('button', { name: 'Reativar' }));

    await waitFor(() => expect(reactivated).toBe(1));
    expect(toastMock.success).toHaveBeenCalledWith('Tipo de pack reativado com sucesso.');
  }, 15000);

  it('drops the suggested end date when the trainer switches to a type without duration', async () => {
    const openEnded = packType({
      id: '77777777-7777-7777-7777-000000000002',
      name: 'Pack livre',
      expected_duration_days: null,
    });
    packHandlers({ types: [packType(), openEnded], packs: [] });
    let body: Record<string, unknown> | null = null;
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.post(`${API}/client-session-packs`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(clientPack(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [PACKS_ROUTE] });

    await user.click((await screen.findAllByRole('button', { name: 'Vender pack' }))[0]!);
    const sheet = await screen.findByRole('dialog');
    await user.click(within(sheet).getByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await waitFor(() => expect(within(sheet).getByLabelText('Tipo de pack')).toBeEnabled());
    await user.selectOptions(within(sheet).getByLabelText('Tipo de pack'), PACK_TYPE_ID);
    expect(within(sheet).getByLabelText('Fim previsto')).not.toHaveValue('');
    await user.selectOptions(within(sheet).getByLabelText('Tipo de pack'), openEnded.id);

    // A data sugerida pelo tipo anterior não pode seguir com um tipo sem duração.
    expect(within(sheet).getByLabelText('Fim previsto')).toHaveValue('');
    await user.click(within(sheet).getByRole('button', { name: 'Vender pack' }));
    await waitFor(() => expect(body).not.toBeNull());
    expect(body!.expected_end_date).toBeNull();
  }, 20000);

  it('keeps an end date the trainer typed when the type has no duration', async () => {
    const openEnded = packType({
      id: '77777777-7777-7777-7777-000000000002',
      name: 'Pack livre',
      expected_duration_days: null,
    });
    packHandlers({ types: [openEnded], packs: [] });
    server.use(http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))));
    const user = userEvent.setup();
    renderApp({ initialEntries: [PACKS_ROUTE] });

    await user.click((await screen.findAllByRole('button', { name: 'Vender pack' }))[0]!);
    const sheet = await screen.findByRole('dialog');
    await user.type(within(sheet).getByLabelText('Fim previsto'), '2099-12-31');
    await waitFor(() => expect(within(sheet).getByLabelText('Tipo de pack')).toBeEnabled());
    await user.selectOptions(within(sheet).getByLabelText('Tipo de pack'), openEnded.id);

    expect(within(sheet).getByLabelText('Fim previsto')).toHaveValue('2099-12-31');
  }, 15000);
});
