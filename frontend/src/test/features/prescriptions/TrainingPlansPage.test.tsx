import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

import { CLIENT_ID, PLAN_ID, summary } from './trainingFixtures';

describe('Training plans list', () => {
  it('lists plans with the client name and links to the plan page and the new editor', async () => {
    const requests: string[] = [];
    server.use(
      ...restorableSession(),
      http.get(API + '/training-plans', ({ request }) => {
        requests.push(new URL(request.url).pathname);
        return HttpResponse.json({
          items: [summary],
          total_count: 1,
          page_number: 1,
          page_size: 25,
        });
      })
    );

    renderApp({ initialEntries: ['/trainer/training-plans'] });
    expect(await screen.findByText('Marta Figueiredo')).toBeInTheDocument();
    expect(requests).toHaveLength(1);
    expect(screen.getByRole('link', { name: 'Ver plano' })).toHaveAttribute(
      'href',
      '/trainer/training-plans/' + PLAN_ID
    );
    expect(screen.getByRole('link', { name: 'Novo plano' })).toHaveAttribute(
      'href',
      '/trainer/training-plans/new'
    );
  });

  it.each(['/trainer/training-plans', '/trainer/meal-plans', '/trainer/supplement-assignments'])(
    'redirects a client away from %s',
    async (path) => {
      server.use(...restorableSession({ role: 'client' }));
      const { router } = renderApp({ initialEntries: [path] });
      await screen.findByRole('heading', { name: 'Hoje' });
      expect(router.state.location.pathname).toBe('/portal');
    }
  );

  it('carries client_id in the links when opened from the client page', async () => {
    server.use(
      ...restorableSession(),
      http.get(API + '/clients/:id', () =>
        HttpResponse.json({ id: CLIENT_ID, name: 'Marta Figueiredo' })
      ),
      http.get(API + '/training-plans', () =>
        HttpResponse.json({ items: [summary], total_count: 1, page_number: 1, page_size: 25 })
      )
    );
    const { TrainingPlansPage } = await import('@/features/prescriptions');
    const { render } = await import('@testing-library/react');
    const { MemoryRouter } = await import('react-router');
    const { QueryClientProvider } = await import('@tanstack/react-query');
    const { createTestQueryClient } = await import('@/test/render');
    render(
      <QueryClientProvider client={createTestQueryClient()}>
        <MemoryRouter>
          <TrainingPlansPage client={{ id: CLIENT_ID, name: 'Marta Figueiredo' }} />
        </MemoryRouter>
      </QueryClientProvider>
    );
    expect(await screen.findByRole('link', { name: 'Ver plano' })).toHaveAttribute(
      'href',
      '/trainer/training-plans/' + PLAN_ID + '?client_id=' + CLIENT_ID
    );
    expect(screen.getByRole('link', { name: 'Novo plano' })).toHaveAttribute(
      'href',
      '/trainer/training-plans/new?client_id=' + CLIENT_ID
    );
  });
});
