import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const CLIENT_ID = '11111111-1111-1111-1111-111111111111';
const SUPPLEMENT_ID = '22222222-2222-2222-2222-222222222222';
const ASSIGNMENT_ID = '33333333-3333-3333-3333-333333333333';

describe('Supplement assignments', () => {
  it.each([false, true])(
    'reports existing assignment with active=%s after a 409',
    async (isActive) => {
      const activityChanges: string[] = [];
      const patches: Array<Record<string, unknown>> = [];
      server.use(
        ...restorableSession(),
        http.get(API + '/supplement-assignments', ({ request }) => {
          const url = new URL(request.url);
          return HttpResponse.json({
            items:
              url.searchParams.get('activity') === 'all'
                ? [
                    {
                      id: ASSIGNMENT_ID,
                      client_id: CLIENT_ID,
                      client_name: 'Marta Figueiredo',
                      supplement_id: SUPPLEMENT_ID,
                      supplement_name: 'Creatina',
                      serving_size: '5 g',
                      timing: 'Manhã',
                      trainer_notes: null,
                      is_active: isActive,
                      is_supplement_archived: false,
                    },
                  ]
                : [],
            total_count: url.searchParams.get('activity') === 'all' ? 1 : 0,
            page_number: 1,
            page_size: Number(url.searchParams.get('page_size')),
          });
        }),
        http.get(API + '/clients', () =>
          HttpResponse.json({
            items: [{ id: CLIENT_ID, name: 'Marta Figueiredo', phone: '912345678' }],
            total_count: 1,
            page_number: 1,
            page_size: 5,
          })
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
        http.post(API + '/supplement-assignments', () =>
          HttpResponse.json(problem('supplement_assignment_already_exists'), { status: 409 })
        ),
        http.post(API + '/supplement-assignments/:assignmentId/reactivate', ({ params }) => {
          activityChanges.push(String(params.assignmentId));
          return HttpResponse.json({ id: ASSIGNMENT_ID, is_active: true });
        }),
        http.patch(API + '/supplement-assignments/:assignmentId', async ({ params, request }) => {
          patches.push({ id: params.assignmentId, ...((await request.json()) as object) });
          return HttpResponse.json({ id: ASSIGNMENT_ID, is_active: true });
        })
      );

      renderApp({ initialEntries: ['/trainer/supplement-assignments'] });
      await userEvent.click(await screen.findByRole('button', { name: 'Nova atribuição' }));
      await userEvent.click(screen.getByRole('combobox', { name: 'Cliente' }));
      await userEvent.type(screen.getByPlaceholderText('Pesquisar por nome'), 'Mar');
      await userEvent.click(await screen.findByText('Marta Figueiredo'));
      await userEvent.click(screen.getByRole('combobox', { name: 'Suplemento' }));
      await userEvent.type(screen.getByPlaceholderText(/Pesquisar no cat/), 'Cre');
      await userEvent.click(await screen.findByText('Creatina'));
      await userEvent.clear(screen.getByLabelText('Dose'));
      await userEvent.type(screen.getByLabelText('Dose'), '10 g');
      await userEvent.click(screen.getByRole('button', { name: 'Guardar atribuição' }));
      await screen.findByText(/Esta atribui/);
      expect(screen.queryByRole('button', { name: /Reativar atribui/ }) !== null).toBe(!isActive);
      if (isActive) return;
      await userEvent.click(
        await screen.findByRole('button', {
          name: 'Reativar atribuição existente',
        })
      );
      await userEvent.click(screen.getByRole('button', { name: 'Reativar' }));
      await waitFor(() => expect(activityChanges).toEqual([ASSIGNMENT_ID]));

      // Depois de reativar, o formulário edita a atribuição reativada com a dose escrita:
      // repetir o POST daria outro 409.
      expect(await screen.findByRole('heading', { name: 'Editar atribuição' })).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      await userEvent.click(screen.getByRole('button', { name: 'Guardar atribuição' }));
      await waitFor(() => expect(patches).toHaveLength(1));
      expect(patches[0]).toMatchObject({
        id: ASSIGNMENT_ID,
        serving_size: '10 g',
        timing: 'Manhã',
      });
    },
    // O caso inativo ainda abre o diálogo de reativação. Com a suite em paralelo
    // isto passa dos 15 s nesta máquina; sozinho termina em cerca de 2 s.
    30_000
  );
});
