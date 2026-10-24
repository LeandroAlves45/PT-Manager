import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { setViewport } from '@/test/browser-fakes';
import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { CLIENT_ID, dashboardResponse, pendingReviewPage } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

const ROUTE = '/trainer/initial-assessments';

const item = (name: string, submittedAt: string, clientId = CLIENT_ID) => ({
  assessment_id: `99999999-9999-9999-9999-${String(name.length).padStart(12, '0')}`,
  client_id: clientId,
  client_name: name,
  submitted_at: submittedAt,
});

describe('PendingInitialAssessmentsPage', () => {
  it('lists the pending assessments, sends page_number on the first request, and links to each client', async () => {
    const queries: URLSearchParams[] = [];
    server.use(
      ...restorableSession(),
      http.get(`${API}/initial-assessments/pending-review`, ({ request }) => {
        queries.push(new URL(request.url).searchParams);
        return HttpResponse.json(
          pendingReviewPage([item('Ana Ribeiro', '2026-09-12T10:00:00Z')], 1)
        );
      })
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(
      await screen.findByRole('heading', { name: 'Avaliações iniciais por rever' })
    ).toBeInTheDocument();
    const link = await screen.findByRole('link', {
      name: 'Rever a avaliação inicial de Ana Ribeiro',
    });
    expect(link).toHaveAttribute('href', `/trainer/clients/${CLIENT_ID}/initial-assessment`);
    expect(screen.getByText('Preenchida a 12/09/2026')).toBeInTheDocument();
    expect(queries[0]?.get('page_number')).toBe('1');
    expect(queries[0]?.get('page_size')).toBe('25');
  }, 20000);

  it('asks for the next page with page_number', async () => {
    const pages: (string | null)[] = [];
    server.use(
      ...restorableSession(),
      http.get(`${API}/initial-assessments/pending-review`, ({ request }) => {
        const page = new URL(request.url).searchParams.get('page_number');
        pages.push(page);
        return HttpResponse.json(
          pendingReviewPage(
            [item(page === '2' ? 'Segunda Página' : 'Primeira Página', '2026-09-12T10:00:00Z')],
            30,
            Number(page)
          )
        );
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await screen.findByText('Primeira Página');
    await user.click(screen.getByRole('button', { name: 'Seguinte' }));

    expect(await screen.findByText('Segunda Página')).toBeInTheDocument();
    expect(pages.at(-1)).toBe('2');
  }, 20000);

  it('goes back to the last page that still exists when the asked page came back empty', async () => {
    const pages: (string | null)[] = [];
    server.use(
      ...restorableSession(),
      http.get(`${API}/initial-assessments/pending-review`, ({ request }) => {
        const page = new URL(request.url).searchParams.get('page_number');
        pages.push(page);
        return HttpResponse.json(
          page === '2'
            ? pendingReviewPage([], 25, 2)
            : pendingReviewPage([item('Ana Ribeiro', '2026-09-12T10:00:00Z')], 25, 1)
        );
      })
    );
    renderApp({ initialEntries: [`${ROUTE}?page=2`] });

    expect(
      await screen.findByRole('link', { name: 'Rever a avaliação inicial de Ana Ribeiro' })
    ).toBeInTheDocument();
    expect(screen.queryByText('Não há avaliações iniciais por rever.')).not.toBeInTheDocument();
    expect(pages).toEqual(['2', '1']);
  }, 20000);

  it('shows the empty state', async () => {
    server.use(...restorableSession());
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Não há avaliações iniciais por rever.')).toBeInTheDocument();
  }, 20000);
});

describe('Pending initial assessments badge in the navigation', () => {
  it('shows the count next to Clientes, reading only total_count with a page of one', async () => {
    setViewport(1440);
    const queries: URLSearchParams[] = [];
    server.use(
      ...restorableSession(),
      http.get(`${API}/initial-assessments/pending-review`, ({ request }) => {
        queries.push(new URL(request.url).searchParams);
        return HttpResponse.json(
          pendingReviewPage([item('Ana Ribeiro', '2026-09-12T10:00:00Z')], 3)
        );
      })
    );
    renderApp({ initialEntries: ['/trainer'] });

    const badge = await screen.findByRole('img', { name: '3 avaliações iniciais por rever' });
    expect(badge).toHaveTextContent('3');
    expect(queries[0]?.get('page_size')).toBe('1');
  }, 20000);

  it('shows no badge when nothing is pending', async () => {
    setViewport(1440);
    server.use(...restorableSession());
    renderApp({ initialEntries: ['/trainer'] });

    await screen.findByRole('link', { name: 'Clientes' });
    await waitFor(() => expect(screen.getByRole('link', { name: 'Clientes' })).toBeInTheDocument());
    expect(
      screen.queryByRole('img', { name: /avaliações iniciais por rever/ })
    ).not.toBeInTheDocument();
  }, 20000);
});

describe('Dashboard card of initial assessments', () => {
  it('lists the assessments with date and links, plus "Ver todas"', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () => HttpResponse.json(dashboardResponse()))
    );
    renderApp({ initialEntries: ['/trainer'] });

    const card = await screen.findByRole('region', { name: 'Avaliações iniciais por rever' });
    expect(within(card).getByText('2')).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: 'Ana Ribeiro' })).toHaveAttribute(
      'href',
      `/trainer/clients/${CLIENT_ID}/initial-assessment`
    );
    expect(within(card).getByText('12/09/2026')).toBeInTheDocument();
    expect(within(card).getByText('14/09/2026')).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: 'Ver todas' })).toHaveAttribute(
      'href',
      '/trainer/initial-assessments'
    );
    // O cartão das avaliações periódicas continua à parte.
    expect(screen.getByRole('region', { name: 'Avaliações por rever' })).toBeInTheDocument();
  }, 20000);

  it('shows an empty card when there is nothing to review', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/dashboard`, () =>
        HttpResponse.json(
          dashboardResponse({ initial_assessments_pending_review: { total_count: 0, items: [] } })
        )
      )
    );
    renderApp({ initialEntries: ['/trainer'] });

    const card = await screen.findByRole('region', { name: 'Avaliações iniciais por rever' });
    expect(within(card).getByText('Nenhuma avaliação inicial por rever.')).toBeInTheDocument();
  }, 20000);
});
