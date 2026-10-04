import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { subscription } from '@/test/msw/account-fixtures';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const redirectMock = vi.hoisted(() => vi.fn());
vi.mock('@/features/billing/lib/redirect', () => ({ redirectToProvider: redirectMock }));

const ROUTE = '/trainer/billing';
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

/** `GET /billing/subscription`; devolve o contador de pedidos. */
function subscriptionHandler(body = subscription()) {
  let requests = 0;
  server.use(
    ...restorableSession(),
    http.get(`${API}/billing/subscription`, () => {
      requests += 1;
      return HttpResponse.json(body);
    })
  );
  return () => requests;
}

describe('BillingPage', () => {
  beforeEach(() => redirectMock.mockClear());

  it('shows the free plan in trial with usage and the paid plans', async () => {
    subscriptionHandler();
    renderApp({ initialEntries: [ROUTE] });

    const current = await screen.findByRole('heading', { name: 'Free', level: 2 });
    const card = current.closest('section')!;
    expect(within(card).getByText('Ativa')).toBeInTheDocument();
    expect(within(card).getByText('3 de 5 clientes')).toBeInTheDocument();
    expect(within(card).getByText(/Período experimental até/)).toBeInTheDocument();
    expect(
      within(card).queryByRole('button', { name: 'Gerir subscrição' })
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Escolher Starter' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Escolher Pro' })).toBeInTheDocument();
  });

  it('opens the checkout with a canonical idempotency key and only the tier in the body', async () => {
    subscriptionHandler();
    const requests: { key: string | null; body: unknown }[] = [];
    server.use(
      http.post(`${API}/billing/checkout`, async ({ request }) => {
        requests.push({
          key: request.headers.get('Idempotency-Key'),
          body: await request.json(),
        });
        return HttpResponse.json({ checkout_url: 'https://checkout.stripe.com/c/pay/cs_test' });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Escolher Pro' }));

    await waitFor(() =>
      expect(redirectMock).toHaveBeenCalledWith('https://checkout.stripe.com/c/pay/cs_test')
    );
    expect(requests).toHaveLength(1);
    expect(requests[0]!.key).toMatch(UUID);
    expect(requests[0]!.body).toEqual({ tier: 'PRO' });
  });

  it('retries the same tier with the same key and a new tier with another key', async () => {
    subscriptionHandler();
    const keys: { tier: string; key: string | null }[] = [];
    server.use(
      http.post(`${API}/billing/checkout`, async ({ request }) => {
        const { tier } = (await request.json()) as { tier: string };
        keys.push({ tier, key: request.headers.get('Idempotency-Key') });
        return HttpResponse.json(problem('billing_provider_unavailable'), { status: 503 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Escolher Starter' }));
    expect(
      await screen.findByText('O serviço de pagamentos não respondeu. Tenta mais tarde.')
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Escolher Starter' }));
    await waitFor(() => expect(keys).toHaveLength(2));
    await user.click(screen.getByRole('button', { name: 'Escolher Pro' }));
    await waitFor(() => expect(keys).toHaveLength(3));

    expect(keys[1]!.key).toBe(keys[0]!.key);
    expect(keys[2]!.key).not.toBe(keys[0]!.key);
    expect(redirectMock).not.toHaveBeenCalled();
  });

  it.each([
    ['billing_provider_disabled', 503, 'Os pagamentos estão temporariamente indisponíveis.'],
    [
      'billing_active_checkout_exists',
      409,
      'Já tens um pagamento iniciado. Conclui-o ou aguarda 30 minutos para escolher outro plano.',
    ],
  ])('translates %s', async (code, status, message) => {
    subscriptionHandler();
    server.use(
      http.post(`${API}/billing/checkout`, () => HttpResponse.json(problem(code), { status }))
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Escolher Pro' }));

    expect(await screen.findByText(message)).toBeInTheDocument();
  });

  it('manages a paid subscription in the customer portal', async () => {
    subscriptionHandler(subscription({ tier: 'STARTER', client_limit: 25, trial_ends_at: null }));
    const keys: (string | null)[] = [];
    server.use(
      http.post(`${API}/billing/customer-portal`, ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        return HttpResponse.json({ portal_url: 'https://billing.stripe.com/p/session/test' });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByRole('heading', { name: 'Starter', level: 2 })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Escolher/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Gerir subscrição' }));

    await waitFor(() =>
      expect(redirectMock).toHaveBeenCalledWith('https://billing.stripe.com/p/session/test')
    );
    expect(keys[0]).toMatch(UUID);
  });

  it('flags an inactive paid plan and offers the checkout again', async () => {
    subscriptionHandler(
      subscription({ tier: 'PRO', status: 'CANCELLED', client_limit: null, trial_ends_at: null })
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Cancelada')).toBeInTheDocument();
    expect(
      screen.getByText('A subscrição não está ativa: não podes adicionar clientes novos.')
    ).toBeInTheDocument();
    expect(screen.getByText('3 clientes · ilimitado')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Escolher Pro' })).toBeInTheDocument();
  });

  it('confirms the return from a paid checkout and polls until the plan is paid', async () => {
    let requests = 0;
    server.use(
      ...restorableSession(),
      http.get(`${API}/billing/subscription`, () => {
        requests += 1;
        // O webhook "chega" entre a primeira e a segunda leitura.
        return HttpResponse.json(
          requests === 1
            ? subscription()
            : subscription({ tier: 'STARTER', client_limit: 25, trial_ends_at: null })
        );
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?checkout=success`] });

    expect(
      await screen.findByText(
        'Pagamento recebido. A confirmação do plano pode demorar alguns segundos.'
      )
    ).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Free', level: 2 })).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { name: 'Starter', level: 2 }, { timeout: 5000 })
    ).toBeInTheDocument();
    const afterPaid = requests;
    await new Promise((resolve) => setTimeout(resolve, 3500));
    expect(requests).toBe(afterPaid);

    await user.click(screen.getByRole('button', { name: 'Fechar' }));
    expect(screen.queryByText(/Pagamento recebido/)).not.toBeInTheDocument();
    expect(window.location.search).toBe('');
  });

  it('does not poll when the page is opened without a checkout result', async () => {
    const requests = subscriptionHandler();
    renderApp({ initialEntries: [ROUTE] });

    await screen.findByRole('heading', { name: 'Free', level: 2 });
    await new Promise((resolve) => setTimeout(resolve, 3500));
    expect(requests()).toBe(1);
  });

  it('tells the trainer that a cancelled checkout changed nothing', async () => {
    subscriptionHandler();
    renderApp({ initialEntries: [`${ROUTE}?checkout=cancelled`] });

    expect(
      await screen.findByText('Pagamento cancelado. O teu plano não mudou.')
    ).toBeInTheDocument();
  });
});
