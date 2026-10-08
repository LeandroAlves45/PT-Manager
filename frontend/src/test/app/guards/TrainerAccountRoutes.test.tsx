import { waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

describe('Trainer routes of the 6E-5', () => {
  it.each([
    ['client', '/trainer/check-ins', '/portal'],
    ['client', '/trainer/settings', '/portal'],
    ['client', '/trainer/billing', '/portal'],
    ['superuser', '/trainer/check-ins', '/admin'],
  ] as const)('sends a %s away from %s without loading trainer data', async (role, entry, home) => {
    const requests: string[] = [];
    server.use(
      ...restorableSession({ role, trainer_id: role === 'client' ? 't-1' : null }),
      http.get(`${API}/check-ins`, ({ request }) => {
        requests.push(request.url);
        return HttpResponse.json({});
      }),
      http.get(`${API}/trainer-settings`, ({ request }) => {
        requests.push(request.url);
        return HttpResponse.json({});
      }),
      http.get(`${API}/billing/subscription`, ({ request }) => {
        requests.push(request.url);
        return HttpResponse.json({});
      }),
      // A visão geral do admin carrega ao chegar a casa; responde vazio para não falhar.
      http.get(`${API}/admin/overview`, () => HttpResponse.json({}, { status: 503 }))
    );

    const { router } = renderApp({ initialEntries: [entry] });

    await waitFor(() => expect(router.state.location.pathname).toBe(home));
    expect(requests).toEqual([]);
  });
});
