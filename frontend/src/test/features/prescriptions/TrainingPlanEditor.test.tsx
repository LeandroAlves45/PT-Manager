import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

import { clientHandler, CLIENT_ID, details, EXERCISE_ID, PLAN_ID } from './trainingFixtures';

const NEW = '/trainer/training-plans/new?client_id=' + CLIENT_ID;

function exerciseSearch() {
  return http.get(API + '/exercises', () =>
    HttpResponse.json({
      items: [{ id: EXERCISE_ID, name: 'Agachamento', scope: 'global' }],
      total_count: 1,
      page_number: 1,
      page_size: 25,
    })
  );
}

async function fillDetails(name = 'Plano novo') {
  await userEvent.type(await screen.findByLabelText('Nome'), name);
  await userEvent.type(screen.getByLabelText('Data de início'), '2026-10-01');
  await userEvent.click(screen.getByRole('button', { name: 'Seguinte' }));
}

async function pickExercise() {
  await userEvent.click(await screen.findByRole('button', { name: 'Adicionar dia' }));
  await userEvent.click(screen.getByRole('button', { name: 'Adicionar exercício' }));
  await userEvent.click(screen.getByRole('combobox', { name: 'Exercício 1' }));
  await userEvent.type(screen.getByPlaceholderText('Pesquisar no catálogo'), 'Agach');
  await userEvent.click(await screen.findByText('Agachamento'));
}

/** Corpo enviado ao servidor, só com o que os testes leem. */
type PlanBody = Record<string, unknown> & {
  structure: {
    days: {
      exercises: {
        sets: {
          id?: string | null;
          planned_reps: number | null;
          planned_reps_max?: number | null;
        }[];
      }[];
    }[];
  };
};

describe('Training plan editor (two steps)', () => {
  it('creates a plan in two steps with a single POST that carries planned_reps_max', async () => {
    const posts: PlanBody[] = [];
    server.use(
      ...restorableSession(),
      clientHandler,
      exerciseSearch(),
      http.post(API + '/training-plans', async ({ request }) => {
        posts.push((await request.json()) as PlanBody);
        return HttpResponse.json(details(), { status: 201 });
      })
    );

    const { router } = renderApp({ initialEntries: [NEW] });
    expect(await screen.findByText('Passo 1 de 2 · Detalhes')).toBeInTheDocument();
    await fillDetails();
    expect(await screen.findByText('Passo 2 de 2 · Estrutura')).toBeInTheDocument();
    expect(posts).toHaveLength(0);

    await pickExercise();
    await userEvent.type(screen.getByLabelText('Repetições'), '10');
    await userEvent.type(screen.getByLabelText('Repetições até'), '12');
    await userEvent.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0]).toMatchObject({
      client_id: CLIENT_ID,
      name: 'Plano novo',
      start_date: '2026-10-01',
    });
    expect(posts[0]!.structure.days[0]!.exercises[0]!.sets[0]).toMatchObject({
      planned_reps: 10,
      planned_reps_max: 12,
    });
    // A rota da ficha é lazy: o primeiro import pode demorar.
    await waitFor(
      () => expect(router.state.location.pathname).toBe('/trainer/clients/' + CLIENT_ID),
      { timeout: 15000 }
    );
    expect(router.state.location.search).toBe('?tab=training');
  });

  it('keeps step 1 when going back and does not ask to leave between steps', async () => {
    server.use(...restorableSession(), clientHandler);
    const { router } = renderApp({ initialEntries: [NEW] });
    await fillDetails('Plano guardado em memória');
    await userEvent.click(await screen.findByRole('button', { name: 'Adicionar dia' }));
    expect(screen.queryByText('Sair sem guardar?')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Anterior' }));
    expect(await screen.findByLabelText('Nome')).toHaveValue('Plano guardado em memória');
    expect(screen.queryByText('Sair sem guardar?')).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/trainer/training-plans/new');

    await userEvent.click(screen.getByRole('button', { name: 'Seguinte' }));
    expect(await screen.findAllByLabelText('Semana')).toHaveLength(1);
  });

  it('redirects structure to step 1 when the first step is not valid', async () => {
    server.use(...restorableSession(), clientHandler);
    const { router } = renderApp({
      initialEntries: ['/trainer/training-plans/new/structure?client_id=' + CLIENT_ID],
    });
    expect(await screen.findByText('Passo 1 de 2 · Detalhes')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/trainer/training-plans/new');
  });

  it('asks before leaving with a draft', async () => {
    server.use(...restorableSession(), clientHandler);
    const { router } = renderApp({ initialEntries: [NEW] });
    await userEvent.type(await screen.findByLabelText('Nome'), 'Rascunho');
    await userEvent.click(screen.getByRole('link', { name: /Voltar à ficha/ }));

    const dialog = await screen.findByRole('alertdialog');
    expect(within(dialog).getByText('Sair sem guardar?')).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Continuar a editar' }));
    expect(router.state.location.pathname).toBe('/trainer/training-plans/new');
  });

  it('refuses a maximum that is not above the minimum and sends nothing', async () => {
    const posts: unknown[] = [];
    server.use(
      ...restorableSession(),
      clientHandler,
      exerciseSearch(),
      http.post(API + '/training-plans', async ({ request }) => {
        posts.push(await request.json());
        return HttpResponse.json(details(), { status: 201 });
      })
    );
    renderApp({ initialEntries: [NEW] });
    await fillDetails();
    await pickExercise();
    await userEvent.type(screen.getByLabelText('Repetições'), '10');
    await userEvent.type(screen.getByLabelText('Repetições até'), '10');
    await userEvent.click(screen.getByRole('button', { name: 'Criar' }));

    expect(await screen.findByText('Tem de ser maior que as repetições.')).toBeInTheDocument();
    expect(posts).toHaveLength(0);
  });

  it('edits the structure of an existing plan with a PUT that keeps the ids', async () => {
    const puts: PlanBody[] = [];
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () => HttpResponse.json(details())),
      http.put(API + '/training-plans/:id', async ({ request }) => {
        puts.push((await request.json()) as PlanBody);
        return HttpResponse.json(details());
      })
    );
    renderApp({ initialEntries: [`/trainer/training-plans/${PLAN_ID}/edit/structure`] });
    await userEvent.click(await screen.findByRole('button', { name: 'Adicionar série' }));
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(puts).toHaveLength(1));
    const sets = puts[0]!.structure.days[0]!.exercises[0]!.sets;
    expect(sets[0]).toMatchObject({ planned_reps: 10, planned_reps_max: 12 });
    expect(sets.map((item) => item.id)).toEqual([expect.any(String), null]);
  });

  it('sends only the metadata (PATCH) when the plan already has history', async () => {
    const patches: Array<Record<string, unknown>> = [];
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () =>
        HttpResponse.json(details({ has_history: true }))
      ),
      http.patch(API + '/training-plans/:id', async ({ request }) => {
        patches.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json(details());
      })
    );
    renderApp({ initialEntries: [`/trainer/training-plans/${PLAN_ID}/edit`] });
    expect(await screen.findAllByText(/A estrutura e as datas/)).toHaveLength(1);
    expect(screen.getByLabelText('Data de início')).toBeDisabled();
    await userEvent.clear(screen.getByLabelText('Nome'));
    await userEvent.type(screen.getByLabelText('Nome'), 'Plano editado');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(patches).toHaveLength(1));
    expect(patches[0]).toMatchObject({ name: 'Plano editado', start_date: '2026-09-01' });
    expect(patches[0]).not.toHaveProperty('structure');
  });

  it('turns a history conflict on PUT into a read-only structure', async () => {
    let conflicted = false;
    server.use(
      ...restorableSession(),
      clientHandler,
      http.get(API + '/training-plans/:id', () =>
        HttpResponse.json(details({ has_history: conflicted }))
      ),
      http.put(API + '/training-plans/:id', () => {
        conflicted = true;
        return HttpResponse.json(problem('training_structure_has_history'), { status: 409 });
      })
    );
    renderApp({ initialEntries: [`/trainer/training-plans/${PLAN_ID}/edit/structure`] });
    await userEvent.click(await screen.findByRole('button', { name: 'Guardar' }));

    expect(await screen.findAllByText(/A estrutura e as datas/)).not.toHaveLength(0);
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Adicionar série' })).not.toBeInTheDocument()
    );
  });
});
