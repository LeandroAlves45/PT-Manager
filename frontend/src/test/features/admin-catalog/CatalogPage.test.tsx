import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const food = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Arroz',
  description: null,
  protein: 3,
  carbs: 28,
  fats: 1,
  kcal: 133,
  fiber: null,
  default_serving_grams: 150,
  is_active: true,
  created_at: '2026-09-23T10:00:00Z',
  updated_at: '2026-09-23T10:00:00Z',
};

describe('CatalogPage', () => {
  it('loads a page of 25 foods with count and visible actions', async () => {
    const urls: string[] = [];
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/global-foods`, ({ request }) => {
        urls.push(request.url);
        return HttpResponse.json({
          items: [food],
          total_count: 1284,
          page_number: 1,
          page_size: 25,
        });
      })
    );
    renderApp({ initialEntries: ['/admin/catalog/foods'] });
    expect(await screen.findByRole('rowheader', { name: 'Arroz' })).toBeInTheDocument();
    expect(screen.getByText(/1\s*284 alimentos/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Apagar Arroz' })).toBeVisible();
    expect(new URL(urls[0] ?? '').searchParams.get('page_size')).toBe('25');
  }, 15000);

  it('calculates kcal without sending it in a new food request', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/global-foods`, () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      ),
      http.post(`${API}/global-foods`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(food, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/catalog/foods'] });
    await user.click(await screen.findByRole('button', { name: /Novo alimento/ }));
    await user.type(screen.getByLabelText('Nome'), 'Arroz');
    expect(screen.getByText('0 kcal/100 g')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Guardar alterações' }));
    expect(screen.queryAllByRole('alert').map((element) => element.textContent)).toEqual([]);
    await waitFor(() => expect(body).not.toBeNull());
    expect(body).not.toHaveProperty('kcal');
  }, 15000);

  it('shows managed video replacement without a request per exercise', async () => {
    let requests = 0;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/global-exercises`, () => {
        requests += 1;
        return HttpResponse.json({
          items: [
            {
              id: '22222222-2222-2222-2222-222222222222',
              name: 'Agachamento',
              description: null,
              muscle_groups: null,
              equipment: null,
              difficulty_level: null,
              video_url: null,
              managed_video_status: 'processing',
              has_ready_video: true,
              is_active: true,
              created_at: '2026-09-23T10:00:00Z',
              updated_at: '2026-09-23T10:00:00Z',
            },
          ],
          total_count: 1,
          page_number: 1,
          page_size: 25,
        });
      })
    );
    renderApp({ initialEntries: ['/admin/catalog/exercises'] });
    expect(
      await screen.findByText('Vídeo disponível · substituição em processamento')
    ).toBeInTheDocument();
    expect(requests).toBe(1);
  });

  it('keeps the selected page in the URL and requests only that page', async () => {
    const requestedPages: string[] = [];
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/global-foods`, ({ request }) => {
        const page = new URL(request.url).searchParams.get('page_number') ?? '';
        requestedPages.push(page);
        return HttpResponse.json({
          items: [food],
          total_count: 26,
          page_number: Number(page),
          page_size: 25,
        });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/catalog/foods'] });
    await screen.findByRole('rowheader', { name: 'Arroz' });
    await user.click(screen.getByRole('button', { name: 'Seguinte' }));
    await waitFor(() => expect(requestedPages).toEqual(['1', '2']));
    expect(window.location.search).toContain('page=2');
  }, 15000);

  it('archives only after confirmation and reports a conflict', async () => {
    let writes = 0;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/global-foods`, () =>
        HttpResponse.json({ items: [food], total_count: 1, page_number: 1, page_size: 25 })
      ),
      http.post(`${API}/global-foods/:foodId/archive`, () => {
        writes += 1;
        return HttpResponse.json(
          { title: 'global_food_in_use', detail: 'Em uso', status: 409 },
          { status: 409 }
        );
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/catalog/foods'] });
    await user.click(await screen.findByRole('button', { name: 'Arquivar Arroz' }));
    expect(writes).toBe(0);
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    await waitFor(() => expect(writes).toBe(1));
    expect(screen.getByRole('dialog', { name: 'Arquivar item?' })).toBeVisible();
  }, 15000);

  it('saves an edited food with a serving above 100 g', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/global-foods`, () =>
        HttpResponse.json({ items: [food], total_count: 1, page_number: 1, page_size: 25 })
      ),
      http.patch(`${API}/global-foods/:foodId`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(food);
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/catalog/foods'] });
    await user.click(await screen.findByRole('button', { name: 'Editar Arroz' }));
    await user.click(screen.getByRole('button', { name: 'Guardar alterações' }));
    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({ default_serving_grams: 150 });
  }, 15000);

  it('asks for a missing macro in Portuguese without sending the request', async () => {
    let writes = 0;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/global-foods`, () =>
        HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
      ),
      http.post(`${API}/global-foods`, () => {
        writes += 1;
        return HttpResponse.json(food, { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/admin/catalog/foods'] });
    await user.click(await screen.findByRole('button', { name: /Novo alimento/ }));
    await user.type(screen.getByLabelText('Nome'), 'Arroz');
    await user.clear(screen.getByLabelText('Proteína (g)'));
    await user.click(screen.getByRole('button', { name: 'Guardar alterações' }));
    expect(await screen.findByText('Indica um valor entre 0 e 100.')).toBeInTheDocument();
    expect(writes).toBe(0);
  }, 15000);
});
