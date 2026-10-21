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
  clientPack,
  clientPackPage,
  clientPage,
  clientSummary,
  clientSummaryOverview,
  PACK_TYPE_ID,
  packType,
  packTypePage,
  sessionPage,
} from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

/** Listas que a página Sessões e packs e a ficha do cliente pedem ao voltar. */
function listHandlers() {
  server.use(
    ...restorableSession(),
    http.get(`${API}/sessions`, () => HttpResponse.json(sessionPage([]))),
    http.get(`${API}/pack-types`, () => HttpResponse.json(packTypePage([packType()]))),
    http.get(`${API}/client-session-packs`, () =>
      HttpResponse.json(clientPackPage([clientPack()]))
    ),
    http.get(`${API}/clients/:clientId`, () => HttpResponse.json(clientDetails())),
    http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(clientSummaryOverview())),
    http.get(`${API}/clients/:clientId/initial-assessment`, () =>
      HttpResponse.json(problem('resource_not_found'), { status: 404 })
    )
  );
}

describe('SellPackPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('sells to the client of the record without a client picker and goes back to its sessions tab', async () => {
    listHandlers();
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post(`${API}/client-session-packs`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(clientPack(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`/trainer/clients/${CLIENT_ID}/sell-pack`] });

    expect(await screen.findByText(/Para Marta Figueiredo/)).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Cliente' })).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Tipo de pack')).toBeEnabled());
    await user.selectOptions(screen.getByLabelText('Tipo de pack'), PACK_TYPE_ID);
    await user.click(screen.getByRole('button', { name: 'Vender pack' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({ client_id: CLIENT_ID, pack_type_id: PACK_TYPE_ID });
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/trainer/clients/${CLIENT_ID}`)
    );
    expect(router.state.location.search).toBe('?tab=sessions');
    expect(toastMock.success).toHaveBeenCalledWith('Pack vendido a Marta Figueiredo.');
  }, 20000);

  it('asks for the client when opened from the packs tab and goes back to it', async () => {
    listHandlers();
    server.use(
      http.get(`${API}/clients`, () => HttpResponse.json(clientPage([clientSummary()]))),
      http.post(`${API}/client-session-packs`, () =>
        HttpResponse.json(clientPack(), { status: 201 })
      )
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/sessions?tab=packs'] });

    await user.click((await screen.findAllByRole('link', { name: 'Vender pack' }))[0]!);
    await user.click(await screen.findByRole('combobox', { name: 'Cliente' }));
    await user.type(await screen.findByPlaceholderText('Pesquisar por nome'), 'Ma');
    await user.click(await screen.findByRole('option', { name: /Marta Figueiredo/ }));
    await waitFor(() => expect(screen.getByLabelText('Tipo de pack')).toBeEnabled());
    await user.selectOptions(screen.getByLabelText('Tipo de pack'), PACK_TYPE_ID);
    await user.click(screen.getByRole('button', { name: 'Vender pack' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer/sessions'));
    expect(router.state.location.search).toBe('?tab=packs');
  }, 20000);

  it('asks before leaving with an unsaved sale', async () => {
    listHandlers();
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/sessions/sell-pack'] });

    await waitFor(() => expect(screen.getByLabelText('Tipo de pack')).toBeEnabled());
    await user.selectOptions(screen.getByLabelText('Tipo de pack'), PACK_TYPE_ID);
    await user.click(screen.getByRole('link', { name: /Voltar a Packs dos clientes/ }));

    const dialog = await screen.findByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'Continuar a editar' }));
    expect(router.state.location.pathname).toBe('/trainer/sessions/sell-pack');
  });
});

describe('PackTypeEditorPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
  });

  it('creates a type and goes back to the types tab', async () => {
    listHandlers();
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post(`${API}/pack-types`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(packType(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/sessions/pack-types/new'] });

    await user.type(await screen.findByLabelText('Nome'), 'Pack 12');
    await user.type(screen.getByLabelText('Número de sessões'), '12');
    await user.type(screen.getByLabelText('Preço'), '300');
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() =>
      expect(body).toEqual({
        name: 'Pack 12',
        session_count: 12,
        price_cents: 30000,
        currency: 'EUR',
        expected_duration_days: null,
      })
    );
    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer/sessions'));
    expect(router.state.location.search).toBe('?tab=types');
  }, 20000);

  it('loads the type by id, saves the edit and goes back', async () => {
    listHandlers();
    let body: Record<string, unknown> | null = null;
    server.use(
      http.get(`${API}/pack-types/:packTypeId`, () => HttpResponse.json(packType())),
      http.patch(`${API}/pack-types/:packTypeId`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(packType({ name: 'Pack 10 renovado' }));
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({
      initialEntries: [`/trainer/sessions/pack-types/${PACK_TYPE_ID}`],
    });

    const name = await screen.findByLabelText('Nome');
    expect(name).toHaveValue('Pack 10 sessões');
    expect(screen.getByRole('heading', { name: 'Editar Pack 10 sessões' })).toBeInTheDocument();
    await user.clear(name);
    await user.type(name, 'Pack 10 renovado');
    await user.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() =>
      expect(body).toMatchObject({ name: 'Pack 10 renovado', price_cents: 30000 })
    );
    await waitFor(() => expect(router.state.location.search).toBe('?tab=types'));
  }, 20000);

  it('asks before leaving with unsaved changes', async () => {
    listHandlers();
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/sessions/pack-types/new'] });

    await user.type(await screen.findByLabelText('Nome'), 'Pack');
    await user.click(screen.getByRole('link', { name: /Voltar a Tipos de pack/ }));

    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Continuar a editar',
      })
    );
    expect(router.state.location.pathname).toBe('/trainer/sessions/pack-types/new');
  });

  it('points the panel buttons at the pages', async () => {
    listHandlers();
    renderApp({ initialEntries: ['/trainer/sessions?tab=types'] });

    expect(await screen.findByRole('link', { name: /Novo tipo de pack/ })).toHaveAttribute(
      'href',
      '/trainer/sessions/pack-types/new'
    );
    expect(await screen.findByRole('link', { name: 'Editar Pack 10 sessões' })).toHaveAttribute(
      'href',
      `/trainer/sessions/pack-types/${PACK_TYPE_ID}`
    );
  });
});
