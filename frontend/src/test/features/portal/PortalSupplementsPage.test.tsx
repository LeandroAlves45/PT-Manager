import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { portalKeys } from '@/features/portal';
import type { components } from '@/shared/api/schema';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import {
  CREATINE_ASSIGNMENT_ID,
  OMEGA_ASSIGNMENT_ID,
  portalHome,
  supplementAssignment,
  supplementAssignmentsPage,
  supplementIntakesToday,
} from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

type IntakesToday = components['schemas']['MyTodaySupplementIntakesResponse'];

const INTAKE_PATH = `${API}/portal/my-supplements/:assignmentId/intakes/today`;

/** As duas tomas das fixtures, ambas tomadas. */
function allTaken(): IntakesToday {
  const today = supplementIntakesToday();
  return {
    ...today,
    taken_count: 2,
    items: today.items.map((item) => ({
      ...item,
      is_taken: true,
      taken_at: item.taken_at ?? '2026-10-06T10:00:00Z',
    })),
  };
}

/** As duas tomas das fixtures, nenhuma tomada. */
function noneTaken(): IntakesToday {
  const today = supplementIntakesToday();
  return {
    ...today,
    taken_count: 0,
    items: today.items.map((item) => ({ ...item, is_taken: false, taken_at: null })),
  };
}

async function openSupplements() {
  server.use(...restorableSession({ role: 'client' }));
  const view = renderApp({ initialEntries: ['/portal/supplements'] });
  await screen.findByRole('heading', { name: 'Suplementos' });
  // A home em cache prova que cada escrita a invalida (o cartão resume as tomas).
  view.queryClient.setQueryData(portalKeys.home(), portalHome());
  return view;
}

function intakeButton(action: 'Marcar' | 'Desmarcar', name: string): HTMLElement {
  return screen.getByRole('button', { name: `${action} toma de ${name}` });
}

describe('PortalSupplementsPage', () => {
  beforeEach(() => {
    toastMock.error.mockClear();
  });

  it("shows today's intakes with the trainer notes, from two requests", async () => {
    const calls: string[] = [];
    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () => {
        calls.push('intakes');
        return HttpResponse.json(supplementIntakesToday());
      }),
      http.get(`${API}/portal/my-supplements`, ({ request }) => {
        calls.push(`list?${new URL(request.url).searchParams.toString()}`);
        return HttpResponse.json(supplementAssignmentsPage());
      })
    );

    await openSupplements();

    expect(screen.getByText('Terça-feira, 06/10/2026')).toBeInTheDocument();
    expect(screen.getByText('1 de 2 tomadas hoje')).toBeInTheDocument();
    expect(screen.getByText('1 por tomar.')).toBeInTheDocument();
    expect(screen.getByRole('progressbar', { name: 'Tomas de hoje' })).toHaveAttribute(
      'aria-valuenow',
      '50'
    );

    const main = screen.getByRole('main');
    const rows = within(main).getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent('Creatina');
    expect(rows[0]).toHaveTextContent('5 g · Depois do treino');
    expect(rows[1]).toHaveTextContent('Ómega 3');
    expect(rows[1]).toHaveTextContent('2 cápsulas · Ao pequeno-almoço');
    expect(intakeButton('Desmarcar', 'Creatina')).toHaveAttribute('aria-pressed', 'true');
    expect(intakeButton('Marcar', 'Ómega 3')).toHaveAttribute('aria-pressed', 'false');
    // O estado também por texto: a cor da secção nunca é a única pista.
    expect(within(rows[0]!).getByText('Tomado')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('Por tomar')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Tomas de hoje' })).toBeInTheDocument();

    // As notas chegam com o segundo pedido e juntam-se à linha certa.
    expect(
      await within(rows[0]!).findByText('Toma todos os dias, também nos de descanso.')
    ).toBeInTheDocument();
    expect(within(rows[0]!).getByText('Creatina monohidratada.')).toBeInTheDocument();
    expect(within(rows[1]!).queryByText(/Toma todos os dias/)).not.toBeInTheDocument();
    expect(screen.queryByText('Descontinuado')).not.toBeInTheDocument();
    // Não existe "em atraso": a toma tem dia, não tem hora.
    expect(screen.queryByText(/atras/i)).not.toBeInTheDocument();

    // Orçamento: dois pedidos, e a lista pede uma única página de 100.
    expect([...calls].sort()).toEqual(['intakes', 'list?page_number=1&page_size=100']);
  });

  it('marks an intake with PUT, waits for the server and refreshes the home card', async () => {
    let state = supplementIntakesToday();
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    const writes: string[] = [];
    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () => HttpResponse.json(state)),
      http.put(INTAKE_PATH, async ({ params }) => {
        writes.push(`PUT ${String(params.assignmentId)}`);
        await held;
        state = allTaken();
        return HttpResponse.json({
          assignment_id: OMEGA_ASSIGNMENT_ID,
          local_date: '2026-10-06',
          taken_at: '2026-10-06T10:00:00Z',
        });
      })
    );
    const user = userEvent.setup();
    const { queryClient } = await openSupplements();

    await user.click(intakeButton('Marcar', 'Ómega 3'));

    // Espera pelo servidor: o botão fica desativado e o visto ainda não mudou.
    await waitFor(() => expect(intakeButton('Marcar', 'Ómega 3')).toBeDisabled());
    expect(screen.getByText('1 de 2 tomadas hoje')).toBeInTheDocument();
    // A outra linha não fica presa ao pedido desta.
    expect(intakeButton('Desmarcar', 'Creatina')).toBeEnabled();

    release();

    expect(await screen.findByText('2 de 2 tomadas hoje')).toBeInTheDocument();
    expect(screen.getByText('Tudo tomado.')).toBeInTheDocument();
    expect(intakeButton('Desmarcar', 'Ómega 3')).toHaveAttribute('aria-pressed', 'true');
    expect(within(screen.getAllByRole('listitem')[1]!).getByText('Tomado')).toBeInTheDocument();
    await waitFor(() => expect(intakeButton('Desmarcar', 'Ómega 3')).toBeEnabled());
    expect(writes).toEqual([`PUT ${OMEGA_ASSIGNMENT_ID}`]);
    expect(queryClient.getQueryState(portalKeys.home())?.isInvalidated).toBe(true);
    expect(toastMock.error).not.toHaveBeenCalled();
  });

  it('unmarks an intake with DELETE', async () => {
    let state = supplementIntakesToday();
    const writes: string[] = [];
    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () => HttpResponse.json(state)),
      http.delete(INTAKE_PATH, ({ params }) => {
        writes.push(`DELETE ${String(params.assignmentId)}`);
        state = noneTaken();
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    const { queryClient } = await openSupplements();

    await user.click(intakeButton('Desmarcar', 'Creatina'));

    expect(await screen.findByText('0 de 2 tomadas hoje')).toBeInTheDocument();
    expect(screen.getByText('2 por tomar.')).toBeInTheDocument();
    expect(intakeButton('Marcar', 'Creatina')).toHaveAttribute('aria-pressed', 'false');
    expect(writes).toEqual([`DELETE ${CREATINE_ASSIGNMENT_ID}`]);
    expect(queryClient.getQueryState(portalKeys.home())?.isInvalidated).toBe(true);
  });

  it('tells the client when the supplement was unassigned meanwhile and drops it', async () => {
    let state = supplementIntakesToday();
    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () => HttpResponse.json(state)),
      http.put(INTAKE_PATH, () => {
        // O personal trainer desativou a atribuição: a releitura já não a traz.
        state = { ...state, total_count: 1, items: [state.items[0]!] };
        return HttpResponse.json(problem('supplement_assignment_inactive'), { status: 409 });
      })
    );
    const user = userEvent.setup();
    const { queryClient } = await openSupplements();

    await user.click(intakeButton('Marcar', 'Ómega 3'));

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith('Este suplemento já não te está atribuído.')
    );
    expect(await screen.findByText('1 de 1 tomadas hoje')).toBeInTheDocument();
    expect(screen.queryByText('Ómega 3')).not.toBeInTheDocument();
    expect(queryClient.getQueryState(portalKeys.home())?.isInvalidated).toBe(true);
  });

  it('keeps the intake as it was when saving fails', async () => {
    server.use(
      http.put(INTAKE_PATH, () => HttpResponse.json(problem('internal_error'), { status: 500 }))
    );
    const user = userEvent.setup();
    await openSupplements();

    await user.click(intakeButton('Marcar', 'Ómega 3'));

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith(
        'Não foi possível guardar a toma do suplemento. Tenta novamente.'
      )
    );
    await waitFor(() => expect(intakeButton('Marcar', 'Ómega 3')).toBeEnabled());
    expect(intakeButton('Marcar', 'Ómega 3')).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByText('1 de 2 tomadas hoje')).toBeInTheDocument();
  });

  it('explains a supplement that no longer exists', async () => {
    server.use(
      http.delete(INTAKE_PATH, () =>
        HttpResponse.json(problem('supplement_assignment_not_found'), { status: 404 })
      )
    );
    const user = userEvent.setup();
    await openSupplements();

    await user.click(intakeButton('Desmarcar', 'Creatina'));

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith('Este suplemento já não existe na tua lista.')
    );
  });

  it('still lets the client register intakes when the notes fail to load', async () => {
    let down = true;
    server.use(
      http.get(`${API}/portal/my-supplements`, () =>
        down
          ? HttpResponse.json(problem('internal_error'), { status: 500 })
          : HttpResponse.json(supplementAssignmentsPage())
      )
    );
    const user = userEvent.setup();
    await openSupplements();

    expect(
      await screen.findByText('Não foi possível carregar as notas do teu personal trainer.')
    ).toBeInTheDocument();
    // As tomas não dependem das notas.
    expect(intakeButton('Marcar', 'Ómega 3')).toBeEnabled();
    expect(screen.getByText('5 g · Depois do treino')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    down = false;
    await user.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    expect(
      await screen.findByText('Toma todos os dias, também nos de descanso.')
    ).toBeInTheDocument();
    expect(
      screen.queryByText('Não foi possível carregar as notas do teu personal trainer.')
    ).not.toBeInTheDocument();
  });

  it('flags a supplement that the trainer archived but is still assigned', async () => {
    server.use(
      http.get(`${API}/portal/my-supplements`, () =>
        HttpResponse.json(
          supplementAssignmentsPage([supplementAssignment({ is_supplement_archived: true })])
        )
      )
    );

    await openSupplements();

    const rows = within(screen.getByRole('main')).getAllByRole('listitem');
    expect(await within(rows[0]!).findByText('Descontinuado')).toBeInTheDocument();
    expect(within(rows[1]!).queryByText('Descontinuado')).not.toBeInTheDocument();
    // A etiqueta sozinha não diz o que fazer: a frase diz, e só na linha arquivada.
    expect(
      within(rows[0]!).getByText(
        'Este suplemento foi descontinuado. Confirma com o teu personal trainer se o deves manter.'
      )
    ).toBeInTheDocument();
    expect(within(rows[1]!).queryByText(/foi descontinuado/)).not.toBeInTheDocument();
    // Continua atribuído: a toma regista-se na mesma. Sem detalhe, a linha mostra só a dose.
    expect(intakeButton('Desmarcar', 'Creatina')).toBeEnabled();
    expect(rows[1]).toHaveTextContent('2 cápsulas · Ao pequeno-almoço');
  });

  it('shows an empty state when no supplement is assigned', async () => {
    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () =>
        HttpResponse.json(supplementIntakesToday({ taken_count: 0, total_count: 0, items: [] }))
      ),
      http.get(`${API}/portal/my-supplements`, () =>
        HttpResponse.json(supplementAssignmentsPage([]))
      )
    );

    await openSupplements();

    expect(screen.getByRole('heading', { name: 'Sem suplementos' })).toBeInTheDocument();
    expect(
      screen.getByText('O teu personal trainer não te atribuiu suplementos diretamente.')
    ).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(screen.queryByText(/tomadas hoje/)).not.toBeInTheDocument();
  });

  it('treats an archived client as an unavailable portal, without retry', async () => {
    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () =>
        HttpResponse.json(problem('supplement_assignment_not_found'), { status: 404 })
      ),
      ...restorableSession({ role: 'client' })
    );

    renderApp({ initialEntries: ['/portal/supplements'] });

    expect(await screen.findByRole('heading', { name: 'Portal indisponível' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
  });

  it('lets the client retry after a technical failure', async () => {
    let down = true;
    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () =>
        down
          ? HttpResponse.json(problem('internal_error'), { status: 500 })
          : HttpResponse.json(supplementIntakesToday())
      ),
      ...restorableSession({ role: 'client' })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/supplements'] });

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar.');

    down = false;
    await user.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    expect(await screen.findByText('1 de 2 tomadas hoje')).toBeInTheDocument();
  });

  it('is reached from the bottom bar', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/portal'] });

    const nav = await screen.findByRole('navigation', { name: 'Navegação do portal' });
    await user.click(within(nav).getByRole('link', { name: 'Suplementos' }));

    expect(await screen.findByText('1 de 2 tomadas hoje')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/portal/supplements');
  });

  it('sends a trainer back to their own area without asking for the intakes', async () => {
    let requests = 0;
    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () => {
        requests += 1;
        return HttpResponse.json(supplementIntakesToday());
      }),
      ...restorableSession({ role: 'trainer' })
    );

    const { router } = renderApp({ initialEntries: ['/portal/supplements'] });

    await screen.findByRole('heading', { name: 'Bom treino' });
    expect(router.state.location.pathname).toBe('/trainer');
    expect(requests).toBe(0);
  });

  it('keeps what was on screen and warns when a refresh fails, then recovers on retry', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const { queryClient } = renderApp({ initialEntries: ['/portal/supplements'] });
    await screen.findByRole('heading', { name: 'Suplementos' });

    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () =>
        HttpResponse.json(problem('Internal server error'), { status: 500 })
      )
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.supplementIntakes() });

    expect(
      await screen.findByText(
        'Não foi possível atualizar. Estás a ver os últimos dados carregados.'
      )
    ).toBeInTheDocument();
    expect(screen.getByText('1 de 2 tomadas hoje')).toBeInTheDocument();
    // É um aviso, não o ecrã de erro.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () =>
        HttpResponse.json(supplementIntakesToday())
      )
    );
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    await waitFor(() =>
      expect(
        screen.queryByText('Não foi possível atualizar. Estás a ver os últimos dados carregados.')
      ).not.toBeInTheDocument()
    );
    expect(screen.getByText('1 de 2 tomadas hoje')).toBeInTheDocument();
  });

  it('drops the cached data when the refresh says the client was archived', async () => {
    server.use(...restorableSession({ role: 'client' }));
    const { queryClient } = renderApp({ initialEntries: ['/portal/supplements'] });
    await screen.findByRole('heading', { name: 'Suplementos' });

    server.use(
      http.get(`${API}/portal/my-supplements/intakes/today`, () =>
        HttpResponse.json(problem('supplement_assignment_not_found'), { status: 404 })
      )
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.supplementIntakes() });

    expect(await screen.findByRole('heading', { name: 'Portal indisponível' })).toBeInTheDocument();
    expect(screen.queryByText('1 de 2 tomadas hoje')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
  });
});
