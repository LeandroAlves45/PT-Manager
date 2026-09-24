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
