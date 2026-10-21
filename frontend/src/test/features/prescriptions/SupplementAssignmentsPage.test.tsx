import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { clientDetails } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const MARTA_ID = '11111111-1111-1111-1111-111111111111';
const ANA_ID = '11111111-1111-1111-1111-111111111112';
const SUPPLEMENT_ID = '22222222-2222-2222-2222-222222222222';
const ASSIGNMENT_ID = '33333333-3333-3333-3333-333333333333';
const NEW = '/trainer/supplement-assignments/new';

type Row = ReturnType<typeof assignment>;

function assignment(overrides: Record<string, unknown> = {}) {
  return {
    id: ASSIGNMENT_ID,
    client_id: MARTA_ID,
    client_name: 'Marta Figueiredo',
    supplement_id: SUPPLEMENT_ID,
    supplement_name: 'Creatina',
    supplement_description: null,
    unit_of_measure: 'g',
    serving_size: '5 g',
    timing: 'Manhã' as string | null,
    trainer_notes: null,
    is_active: true,
    is_supplement_archived: false,
    created_at: '2026-10-01T10:00:00Z',
    updated_at: '2026-10-01T10:00:00Z',
    ...overrides,
  };
}

const CLIENTS = [
  { id: MARTA_ID, name: 'Marta Figueiredo', phone: '912345678' },
  { id: ANA_ID, name: 'Ana Sousa', phone: '912345679' },
];

/**
 * Sessão de trainer, clientes, catálogo e um armazém de atribuições em memória: o GET da lista
 * respeita `client_id` e `activity`, o POST acrescenta, o PATCH altera. Devolve o armazém e os
 * pedidos feitos.
 */
function backend(initial: Row[] = []) {
  const rows: Row[] = [...initial];
  const posts: Array<Record<string, unknown>> = [];
  const patches: Array<Record<string, unknown>> = [];
  const reactivated: string[] = [];
  server.use(
    ...restorableSession(),
    http.get(API + '/supplement-assignments', ({ request }) => {
      const url = new URL(request.url);
      const clientId = url.searchParams.get('client_id');
      const activity = url.searchParams.get('activity');
      const items = rows.filter(
        (row) =>
          (clientId === null || row.client_id === clientId) &&
          (activity === 'all' || (activity === 'inactive' ? !row.is_active : row.is_active))
      );
      return HttpResponse.json({
        items,
        total_count: items.length,
        page_number: 1,
        page_size: Number(url.searchParams.get('page_size')),
      });
    }),
    http.get(API + '/supplement-assignments/:assignmentId', ({ params }) => {
      const found = rows.find((row) => row.id === params.assignmentId);
      return found === undefined
        ? HttpResponse.json(problem('supplement_assignment_not_found', { status: 404 }), {
            status: 404,
          })
        : HttpResponse.json(found);
    }),
    http.get(API + '/clients', () =>
      HttpResponse.json({ items: CLIENTS, total_count: 2, page_number: 1, page_size: 5 })
    ),
    http.get(API + '/clients/:clientId', ({ params }) =>
      HttpResponse.json(
        clientDetails({
          id: String(params.clientId),
          name: CLIENTS.find((client) => client.id === params.clientId)?.name ?? 'Cliente',
        })
      )
    ),
    http.get(API + '/supplements', () =>
      HttpResponse.json({
        items: [
          {
            id: SUPPLEMENT_ID,
            name: 'Creatina',
            serving_size: '5 g',
            timing: 'Manhã',
            is_archived: false,
          },
        ],
        total_count: 1,
        page_number: 1,
        page_size: 25,
      })
    ),
    http.post(API + '/supplement-assignments', async ({ request }) => {
      const body = (await request.json()) as Record<string, string | null>;
      posts.push(body);
      const client = CLIENTS.find((item) => item.id === body.client_id)!;
      const created = assignment({
        id: '44444444-4444-4444-4444-44444444444' + rows.length,
        client_id: client.id,
        client_name: client.name,
        serving_size: body.serving_size,
        timing: body.timing,
      });
      rows.push(created);
      return HttpResponse.json(created, { status: 201 });
    }),
    http.patch(API + '/supplement-assignments/:assignmentId', async ({ params, request }) => {
      const body = (await request.json()) as Record<string, unknown>;
      patches.push({ id: params.assignmentId, ...body });
      const row = rows.find((item) => item.id === params.assignmentId)!;
      Object.assign(row, body, { is_active: true });
      return HttpResponse.json(row);
    }),
    http.post(API + '/supplement-assignments/:assignmentId/reactivate', ({ params }) => {
      reactivated.push(String(params.assignmentId));
      const row = rows.find((item) => item.id === params.assignmentId)!;
      row.is_active = true;
      return HttpResponse.json(row);
    })
  );
  return { rows, posts, patches, reactivated };
}

async function pickClient(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(await screen.findByRole('combobox', { name: 'Cliente' }));
  await user.type(screen.getByPlaceholderText('Pesquisar por nome'), name.slice(0, 3));
  await user.click(await screen.findByText(name));
}

async function pickSupplement(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('combobox', { name: 'Suplemento' }));
  await user.type(screen.getByPlaceholderText(/Pesquisar no cat/), 'Cre');
  await user.click(await screen.findByText('Creatina'));
}

describe('Supplement assignments list', () => {
  it('only lists: «Nova atribuição» and «Editar» are links, with client_id inside a client', async () => {
    backend([assignment()]);
    renderApp({ initialEntries: ['/trainer/supplement-assignments'] });

    expect(await screen.findByRole('link', { name: 'Nova atribuição' })).toHaveAttribute(
      'href',
      NEW
    );
    expect(await screen.findByRole('link', { name: /^Editar Creatina/ })).toHaveAttribute(
      'href',
      `/trainer/supplement-assignments/${ASSIGNMENT_ID}`
    );
    expect(screen.getByRole('button', { name: 'Desativar' })).toBeVisible();
    expect(
      screen.getByText(
        'Os suplementos que juntas a uma refeição ficam no plano alimentar desse cliente.'
      )
    ).toBeVisible();
    expect(screen.queryByRole('form')).toBeNull();
  }, 20000);

  it('shows an assignment created for a second client in the list without filter', async () => {
    const store = backend([assignment()]);
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/supplement-assignments'] });
    // 1.ª leitura da lista: só a Marta.
    expect(await screen.findByText('Marta Figueiredo')).toBeVisible();
    expect(screen.queryByText('Ana Sousa')).toBeNull();

    await user.click(screen.getByRole('link', { name: 'Nova atribuição' }));
    await pickClient(user, 'Ana Sousa');
    await pickSupplement(user);
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() =>
      expect(router.state.location.pathname).toBe('/trainer/supplement-assignments')
    );
    expect(store.posts).toEqual([
      expect.objectContaining({ client_id: ANA_ID, supplement_id: SUPPLEMENT_ID }),
    ]);
    // A lista sem filtro já traz a nova (e a antiga).
    await waitFor(() =>
      expect(screen.getAllByRole('heading', { level: 3, name: 'Creatina' })).toHaveLength(2)
    );
    expect(screen.getByText('Ana Sousa')).toBeInTheDocument();
    expect(screen.getByText('Marta Figueiredo')).toBeInTheDocument();
  }, 30000);

  it('refreshes the filtered list of the second client too', async () => {
    backend([assignment()]);
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/trainer/supplement-assignments'] });
    await screen.findByText('Marta Figueiredo');

    await user.click(screen.getByRole('combobox', { name: 'Filtrar por cliente' }));
    await user.type(screen.getByPlaceholderText('Pesquisar por nome'), 'Ana');
    await user.click(await screen.findByRole('option', { name: /Ana Sousa/ }));
    expect(await screen.findByText('Sem suplementos atribuídos')).toBeVisible();

    await user.click(screen.getByRole('link', { name: 'Nova atribuição' }));
    await pickClient(user, 'Ana Sousa');
    await pickSupplement(user);
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    // Ao voltar, o filtro recomeça sem cliente; filtrar pela Ana mostra a atribuição dela.
    await user.click(await screen.findByRole('combobox', { name: 'Filtrar por cliente' }));
    await user.type(screen.getByPlaceholderText('Pesquisar por nome'), 'Ana');
    await user.click(await screen.findByRole('option', { name: /Ana Sousa/ }));
    await waitFor(() =>
      expect(screen.getAllByRole('heading', { level: 3, name: 'Creatina' })).toHaveLength(1)
    );
    expect(screen.getAllByText('Ana Sousa').length).toBeGreaterThan(0);
    expect(screen.queryByText('Marta Figueiredo')).toBeNull();
  }, 30000);
});

describe('Supplement assignment page', () => {
  it('creates without client_id, choosing the client, and sends the body in snake_case', async () => {
    const store = backend();
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [NEW] });

    expect(await screen.findByRole('heading', { name: 'Criar atribuição' })).toBeVisible();
    expect(screen.getByPlaceholderText('ex.: 5 g')).toBeVisible();
    await pickClient(user, 'Marta Figueiredo');
    await pickSupplement(user);
    await user.clear(screen.getByLabelText('Dose'));
    await user.type(screen.getByLabelText('Dose'), '10gr');
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() => expect(store.posts).toHaveLength(1));
    expect(store.posts[0]).toEqual({
      client_id: MARTA_ID,
      supplement_id: SUPPLEMENT_ID,
      serving_size: '10gr',
      timing: 'Manhã',
      trainer_notes: null,
    });
    await waitFor(() =>
      expect(router.state.location.pathname).toBe('/trainer/supplement-assignments')
    );
    // O servidor normaliza; o cliente mostra o que recebe e não mexe na dose.
  }, 30000);

  it('with client_id fixes the client and returns to the client page', async () => {
    const store = backend();
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${NEW}?client_id=${ANA_ID}`] });

    expect(await screen.findByText('Ana Sousa')).toBeVisible();
    expect(screen.queryByRole('combobox', { name: 'Cliente' })).toBeNull();
    expect(screen.getByRole('link', { name: 'Voltar ao cliente' })).toHaveAttribute(
      'href',
      `/trainer/clients/${ANA_ID}?tab=supplements`
    );
    await pickSupplement(user);
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() => expect(store.posts).toHaveLength(1));
    expect(store.posts[0]).toMatchObject({ client_id: ANA_ID });
    await waitFor(() => expect(router.state.location.pathname).toBe(`/trainer/clients/${ANA_ID}`), {
      timeout: 40000,
    });
    expect(router.state.location.search).toBe('?tab=supplements');
  }, 60000);

  it('edits an assignment with the loaded data and goes back to the list', async () => {
    const store = backend([assignment()]);
    const user = userEvent.setup();
    const { router } = renderApp({
      initialEntries: [`/trainer/supplement-assignments/${ASSIGNMENT_ID}`],
    });

    expect(await screen.findByRole('heading', { name: 'Editar atribuição' })).toBeVisible();
    expect(screen.getByLabelText('Dose')).toHaveValue('5 g');
    expect(screen.getByLabelText('Momento')).toHaveValue('Manhã');
    await user.clear(screen.getByLabelText('Dose'));
    await user.type(screen.getByLabelText('Dose'), '3 g');
    await user.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(store.patches).toHaveLength(1));
    expect(store.patches[0]).toMatchObject({
      id: ASSIGNMENT_ID,
      serving_size: '3 g',
      timing: 'Manhã',
    });
    await waitFor(() =>
      expect(router.state.location.pathname).toBe('/trainer/supplement-assignments')
    );
    expect(await screen.findByText('3 g · Manhã')).toBeVisible();
  }, 30000);

  it('does not offer the form for an inactive assignment', async () => {
    backend([assignment({ is_active: false })]);
    renderApp({ initialEntries: [`/trainer/supplement-assignments/${ASSIGNMENT_ID}`] });

    expect(await screen.findByRole('note')).toHaveTextContent('Reativa-a na lista');
    expect(screen.queryByLabelText('Dose')).toBeNull();
  }, 20000);

  it('asks before leaving with unsaved changes', async () => {
    backend();
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [NEW] });

    await user.type(await screen.findByLabelText('Notas para o cliente'), 'depois do treino');
    await user.click(screen.getByRole('link', { name: 'Voltar a Suplementos atribuídos' }));

    expect(await screen.findByRole('alertdialog', { name: 'Sair sem guardar?' })).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Continuar a editar' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());
    expect(router.state.location.pathname).toBe(NEW);
    expect(screen.getByLabelText('Notas para o cliente')).toHaveValue('depois do treino');
  }, 20000);

  it.each([false, true])(
    'reports the existing assignment with active=%s after a 409',
    async (isActive) => {
      const store = backend([assignment({ is_active: isActive })]);
      server.use(
        http.post(API + '/supplement-assignments', () =>
          HttpResponse.json(problem('supplement_assignment_already_exists'), { status: 409 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: [NEW] });

      await pickClient(user, 'Marta Figueiredo');
      await pickSupplement(user);
      await user.clear(screen.getByLabelText('Dose'));
      await user.type(screen.getByLabelText('Dose'), '10 g');
      await user.click(screen.getByRole('button', { name: 'Criar' }));
      await screen.findByText(/Esta atribui/);
      expect(screen.queryByRole('button', { name: /Reativar atribui/ }) !== null).toBe(!isActive);
      if (isActive) return;
      await user.click(
        await screen.findByRole('button', { name: 'Reativar atribuição existente' })
      );
      await user.click(screen.getByRole('button', { name: 'Reativar' }));
      await waitFor(() => expect(store.reactivated).toEqual([ASSIGNMENT_ID]));

      // Depois de reativar, a página edita a atribuição reativada com a dose escrita: repetir o
      // POST daria outro 409.
      expect(await screen.findByRole('heading', { name: 'Editar atribuição' })).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: 'Guardar' }));
      await waitFor(() => expect(store.patches).toHaveLength(1));
      expect(store.patches[0]).toMatchObject({
        id: ASSIGNMENT_ID,
        serving_size: '10 g',
        timing: 'Manhã',
      });
    },
    30_000
  );

  it('sends an empty timing, not null, when the prefilled Momento is cleared', async () => {
    const store = backend();
    const user = userEvent.setup();
    renderApp({ initialEntries: [NEW] });
    await pickClient(user, 'Marta Figueiredo');
    await pickSupplement(user);
    const timing = screen.getByLabelText('Momento');
    expect(timing).toHaveValue('Manhã');
    await user.clear(timing);
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() => expect(store.posts).toHaveLength(1));
    expect(store.posts[0]).toMatchObject({ timing: '' });
  }, 30_000);

  it('asks for the dose and no longer for the Momento', async () => {
    const store = backend();
    const user = userEvent.setup();
    renderApp({ initialEntries: [NEW] });
    await pickClient(user, 'Marta Figueiredo');
    await pickSupplement(user);
    await user.clear(screen.getByLabelText('Momento'));
    // Só espaços: o `required` nativo deixa passar e a validação própria responde.
    await user.clear(screen.getByLabelText('Dose'));
    await user.type(screen.getByLabelText('Dose'), '   ');
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    expect(await screen.findByText('Indica a dose.')).toBeInTheDocument();
    expect(store.posts).toHaveLength(0);
  }, 30_000);
});
