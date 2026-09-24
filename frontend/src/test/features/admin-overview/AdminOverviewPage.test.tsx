import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('AdminOverviewPage', () => {
  it('shows counts from one aggregated request', async () => {
    let requests = 0;
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/admin/overview`, () => {
        requests += 1;
        return HttpResponse.json({
          global_foods: { total_count: 1284, active_count: 1200, archived_count: 84 },
          global_exercises: { total_count: 72, active_count: 70, archived_count: 2 },
          global_supplements: { total_count: 10, active_count: 9, archived_count: 1 },
          private_foods: { total_count: 3, blocked_count: 1 },
          private_exercises: { total_count: 4, blocked_count: 2 },
        });
      })
    );
    renderApp({ initialEntries: ['/admin'] });
    expect(await screen.findByText(/1\s*284/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Alimentos globais/ })).toHaveAttribute(
      'href',
      '/admin/catalog/foods'
    );
    await waitFor(() => expect(requests).toBe(1));
  }, 15000);

  it.each(['trainer', 'client'] as const)(
    'redirects a %s before requesting admin data',
    async (role) => {
      let requests = 0;
      server.use(
        ...restorableSession({
          role,
          trainer_id: role === 'trainer' ? '22222222-2222-2222-2222-222222222222' : null,
        }),
        http.get(`${API}/admin/overview`, () => {
          requests += 1;
          return HttpResponse.json({});
        })
      );
      const { router } = renderApp({ initialEntries: ['/admin'] });
      await waitFor(() =>
        expect(router.state.location.pathname).toBe(
          role === 'trainer' ? '/trainer' : '/portal/today'
        )
      );
      expect(requests).toBe(0);
    }
  );
});
