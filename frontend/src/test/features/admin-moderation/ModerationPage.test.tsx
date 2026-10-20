import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const privateFood = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Alimento privado',
  owner_trainer_id: '22222222-2222-2222-2222-222222222222',
  owner_trainer_name: 'Ana',
  is_active: true,
  platform_enforcement_status: 'allowed',
  platform_enforcement_reason: null,
  platform_enforced_at: null,
  created_at: '2026-09-23T10:00:00Z',
  updated_at: '2026-09-23T10:00:00Z',
};

function queue(items: unknown[]) {
  return HttpResponse.json({ items, total_count: items.length, page_number: 1, page_size: 25 });
}

describe('ModerationPage filters', () => {
  function track(kind: 'foods' | 'exercises', seen: URLSearchParams[], total = 60) {
    return http.get(`${API}/admin/content-moderation/${kind}`, ({ request }) => {
      seen.push(new URL(request.url).searchParams);
      return HttpResponse.json({
        items: [privateFood],
        total_count: total,
        page_number: 1,
        page_size: 25,
      });
    });
  }

  it('has a single selected tab and sends kind/status/page on the first request', async () => {
    const exercises: URLSearchParams[] = [];
    const foods: URLSearchParams[] = [];
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      track('exercises', exercises),
      track('foods', foods)
    );
    renderApp({ initialEntries: ['/admin/moderation?kind=exercises&status=blocked&page=2'] });
    await screen.findByText('Alimento privado');
    expect(foods).toHaveLength(0);
    expect(exercises[0]?.get('status')).toBe('blocked');
    expect(exercises[0]?.get('page_number')).toBe('2');
    const tabs = screen.getAllByRole('tab');
    expect(tabs.filter((tab) => tab.getAttribute('aria-selected') === 'true')).toHaveLength(1);
    expect(screen.getByRole('tab', { name: 'Exercícios' })).toHaveAttribute(
      'aria-selected',
      'true'
    );
    expect(screen.getByLabelText('Estado')).toHaveValue('blocked');
    // A tab ativa aponta para um painel que existe.
    const controls = screen.getByRole('tab', { name: 'Exercícios' }).getAttribute('aria-controls');
    expect(controls).not.toBeNull();
    expect(document.getElementById(controls ?? '')).not.toBeNull();
  }, 15000);

  it('switching tab requests the other kind from page 1', async () => {
    const foods: URLSearchParams[] = [];
    const exercises: URLSearchParams[] = [];
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      track('foods', foods),
      track('exercises', exercises)
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/moderation?page=2'] });
    await screen.findByText('Alimento privado');
    expect(foods[0]?.get('page_number')).toBe('2');
    await user.click(screen.getByRole('tab', { name: 'Exercícios' }));
    await waitFor(() => expect(exercises.length).toBeGreaterThan(0));
    expect(exercises[0]?.get('page_number')).toBe('1');
    expect(screen.getByRole('tab', { name: 'Exercícios' })).toHaveAttribute(
      'aria-selected',
      'true'
    );
    expect(screen.getByRole('tab', { name: 'Alimentos' })).toHaveAttribute(
      'aria-selected',
      'false'
    );
  }, 15000);

  it('the Estado select changes the status param and resets the page', async () => {
    const seen: URLSearchParams[] = [];
    server.use(...restorableSession({ role: 'superuser', trainer_id: null }), track('foods', seen));
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/moderation?page=2'] });
    await screen.findByText('Alimento privado');
    await user.selectOptions(screen.getByLabelText('Estado'), 'blocked');
    await waitFor(() => expect(seen.at(-1)?.get('status')).toBe('blocked'));
    expect(seen.at(-1)?.get('page_number')).toBe('1');
    expect(screen.getByText('60 conteúdos privados')).toBeInTheDocument();
  }, 15000);
});

describe('ModerationPage', () => {
  it('requires a reason and sends the backend reason code', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/admin/content-moderation/foods`, () =>
        HttpResponse.json({
          items: [
            {
              id: '11111111-1111-1111-1111-111111111111',
              name: 'Alimento privado',
              owner_trainer_id: '22222222-2222-2222-2222-222222222222',
              owner_trainer_name: 'Ana',
              is_active: true,
              platform_enforcement_status: 'allowed',
              platform_enforcement_reason: null,
              platform_enforced_at: null,
              created_at: '2026-09-23T10:00:00Z',
              updated_at: '2026-09-23T10:00:00Z',
            },
          ],
          total_count: 1,
          page_number: 1,
          page_size: 25,
        })
      ),
      http.post(`${API}/admin/content-moderation/foods/:foodId/block`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/moderation'] });
    await user.click(await screen.findByRole('button', { name: 'Bloquear' }));
    expect(screen.getByRole('button', { name: 'Confirmar' })).toBeDisabled();
    await user.click(screen.getByLabelText('Conteúdo malicioso'));
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    await waitFor(() => expect(body).toEqual({ reason_code: 'malicious_content' }));
  });

  it('does not carry a cancelled reason into the next decision', async () => {
    const other = { ...privateFood, id: '33333333-3333-3333-3333-333333333333', name: 'Outro' };
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/admin/content-moderation/foods`, () => queue([privateFood, other]))
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/moderation'] });
    const [first, second] = await screen.findAllByRole('button', { name: 'Bloquear' });
    await user.click(first!);
    await user.click(screen.getByLabelText('Conteúdo malicioso'));
    await user.click(screen.getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await user.click(second!);
    expect(screen.getByLabelText('Conteúdo malicioso')).not.toBeChecked();
    expect(screen.getByRole('button', { name: 'Confirmar' })).toBeDisabled();
  }, 15000);

  it('unblocks without asking for a reason', async () => {
    let unblocks = 0;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/admin/content-moderation/foods`, () =>
        queue([
          {
            ...privateFood,
            platform_enforcement_status: 'blocked',
            platform_enforcement_reason: 'prohibited_content',
            platform_enforced_at: '2026-09-23T11:00:00Z',
          },
        ])
      ),
      http.post(`${API}/admin/content-moderation/foods/:foodId/unblock`, () => {
        unblocks += 1;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/moderation'] });
    expect(await screen.findByText('Conteúdo proibido')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Desbloquear' }));
    expect(screen.queryByRole('group', { name: /Motivo/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    await waitFor(() => expect(unblocks).toBe(1));
  }, 15000);
});
