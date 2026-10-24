import { delay, http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  FakeBroadcastChannel,
  installBroadcastChannel,
  installWebLocks,
  uninstallWebLocks,
} from '@/test/browser-fakes';
import { API, problem, sessionResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';

const SUBSCRIPTION = `${API}/billing/subscription`;
const SUBSCRIPTION_BODY = {
  tier: 'STARTER',
  status: 'ACTIVE',
  client_limit: 25,
  current_client_count: 3,
  trial_ends_at: null,
};

/**
 * Carrega uma instância isolada dos módulos de sessão e cliente, como se fosse um separador
 * novo: estado em memória, `inFlightRefresh` e canal de eventos próprios.
 */
async function loadTab() {
  vi.resetModules();
  const client = await import('@/shared/api/client');
  const session = await import('@/shared/api/session');
  const events = await import('@/shared/api/session-events');
  const notice = await import('@/shared/lib/auth-notice');
  return { ...client, ...session, ...events, ...notice };
}

type Tab = Awaited<ReturnType<typeof loadTab>>;

function signIn(tab: Tab): void {
  tab.setSession(tab.toSession(sessionResponse()));
}

/** Pedidos vistos pela "rede", pela ordem de chegada: `METHOD /path`. */
let requests: string[];

function count(entry: string): number {
  return requests.filter((request) => request === entry).length;
}

/** Refresh saudável que roda o CSRF a cada chamada e emite o access token `access-2`. */
function useRotatingRefresh(): string[] {
  const sequence: string[] = [];
  let rotation = 0;

  server.use(
    http.post(`${API}/auth/csrf`, async () => {
      rotation += 1;
      const token = `csrf-${rotation}`;
      sequence.push(`csrf:${token}`);
      // O atraso abre a janela onde outro separador se intercalaria sem o lock.
      await delay(15);
      return HttpResponse.json({ csrf_token: token });
    }),
    http.post(`${API}/auth/refresh`, ({ request }) => {
      sequence.push(`refresh:${request.headers.get('X-CSRF-Token')}`);
      return HttpResponse.json(sessionResponse({ access_token: 'access-2' }));
    })
  );

  return sequence;
}

/** Endpoint protegido que só aceita o token renovado. */
function useProtectedSubscription(): void {
  server.use(
    http.get(SUBSCRIPTION, ({ request }) =>
      request.headers.get('Authorization') === 'Bearer access-2'
        ? HttpResponse.json(SUBSCRIPTION_BODY)
        : new HttpResponse(null, { status: 401 })
    )
  );
}

beforeEach(() => {
  requests = [];
  server.events.on('request:start', ({ request }) => {
    requests.push(`${request.method} ${new URL(request.url).pathname}`);
  });
});

afterEach(() => {
  server.events.removeAllListeners();
  uninstallWebLocks();
});

describe('apiClient 401 handling', () => {
  it('refreshes once and retries a protected GET once with the new token', async () => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    useProtectedSubscription();

    const result = await tab.apiClient.GET('/api/v1/billing/subscription');

    expect(result.response.status).toBe(200);
    expect(result.data).toEqual(SUBSCRIPTION_BODY);
    expect(count('GET /api/v1/billing/subscription')).toBe(2);
    expect(count('POST /api/v1/auth/refresh')).toBe(1);
    expect(tab.getAccessToken()).toBe('access-2');
  });

  it.each([
    [
      'POST',
      '/api/v1/clients',
      (tab: Tab, body: unknown) => tab.apiClient.POST('/api/v1/clients', { body: body as never }),
    ],
    [
      'PATCH',
      '/api/v1/check-ins/c1/reschedule',
      (tab: Tab, body: unknown) =>
        tab.apiClient.PATCH('/api/v1/check-ins/{checkInId}/reschedule', {
          params: { path: { checkInId: 'c1' } },
          body: body as never,
        }),
    ],
    [
      'PUT',
      '/api/v1/check-ins/c1/answer',
      (tab: Tab, body: unknown) =>
        tab.apiClient.PUT('/api/v1/check-ins/{checkInId}/answer', {
          params: { path: { checkInId: 'c1' } },
          body: body as never,
        }),
    ],
  ])('keeps the full %s body on the retry', async (method, path, send) => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    const bodies: string[] = [];
    const body = { name: 'Ana Sousa', notes: 'á é ç', nested: { value: 42 } };

    server.use(
      http.all(`${API}${path.replace('/api/v1', '')}`, async ({ request }) => {
        bodies.push(await request.text());
        return request.headers.get('Authorization') === 'Bearer access-2'
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 });
      })
    );

    const result = await send(tab, body);

    expect(result.response.status).toBe(200);
    expect(count(`${method} ${path}`)).toBe(2);
    expect(bodies).toEqual([JSON.stringify(body), JSON.stringify(body)]);
  });

  it.each([
    ['login', (tab: Tab) => tab.apiClient.POST('/api/v1/auth/login', { body: {} as never })],
    ['logout', (tab: Tab) => tab.apiClient.POST('/api/v1/auth/logout')],
    ['csrf', (tab: Tab) => tab.apiClient.POST('/api/v1/auth/csrf')],
    ['refresh', (tab: Tab) => tab.apiClient.POST('/api/v1/auth/refresh')],
  ])('never starts a recursive refresh when %s answers 401', async (endpoint, send) => {
    const tab = await loadTab();
    signIn(tab);
    server.use(
      http.post(`${API}/auth/${endpoint}`, () =>
        HttpResponse.json(problem('authentication_invalid_credentials'), { status: 401 })
      )
    );

    const result = await send(tab);

    expect(result.response.status).toBe(401);
    expect(requests).toEqual([`POST /api/v1/auth/${endpoint}`]);
  });

  it('shares one refresh between concurrent requests in the same tab', async () => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    useProtectedSubscription();

    const results = await Promise.all([
      tab.apiClient.GET('/api/v1/billing/subscription'),
      tab.apiClient.GET('/api/v1/billing/subscription'),
      tab.apiClient.GET('/api/v1/billing/subscription'),
    ]);

    expect(results.map((result) => result.response.status)).toEqual([200, 200, 200]);
    expect(count('POST /api/v1/auth/csrf')).toBe(1);
    expect(count('POST /api/v1/auth/refresh')).toBe(1);
  });

  it('returns the second 401 after the single retry without a new refresh cycle', async () => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    server.use(http.get(SUBSCRIPTION, () => new HttpResponse(null, { status: 401 })));

    const result = await tab.apiClient.GET('/api/v1/billing/subscription');

    expect(result.response.status).toBe(401);
    expect(count('GET /api/v1/billing/subscription')).toBe(2);
    expect(count('POST /api/v1/auth/refresh')).toBe(1);
  });

  it('does not refresh when the 401 carries a domain code, and keeps the body readable', async () => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    server.use(
      http.post(`${API}/auth/change-password`, () =>
        HttpResponse.json(problem('authentication_current_password_invalid'), { status: 401 })
      )
    );

    const result = await tab.apiClient.POST('/api/v1/auth/change-password', {
      body: { current_password: 'x', new_password: 'y', confirm_new_password: 'y' },
    });

    expect(result.response.status).toBe(401);
    expect(result.error).toMatchObject({ title: 'authentication_current_password_invalid' });
    expect(requests).toEqual(['POST /api/v1/auth/change-password']);
  });

  it.each([
    ['an empty body', () => new HttpResponse(null, { status: 401 })],
    [
      'a natural-language title',
      () => HttpResponse.json({ title: 'Unauthorized', status: 401 }, { status: 401 }),
    ],
    ['a malformed body', () => HttpResponse.json('not json', { status: 401 })],
  ])('still refreshes and retries when the 401 has %s', async (_name, rejection) => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    server.use(
      http.get(SUBSCRIPTION, ({ request }) =>
        request.headers.get('Authorization') === 'Bearer access-2'
          ? HttpResponse.json(SUBSCRIPTION_BODY)
          : rejection()
      )
    );

    const result = await tab.apiClient.GET('/api/v1/billing/subscription');

    expect(result.response.status).toBe(200);
    expect(count('POST /api/v1/auth/refresh')).toBe(1);
  });
});

describe('refreshSession', () => {
  it('ends anonymous on a 401 CSRF bootstrap without calling refresh', async () => {
    const tab = await loadTab();

    await expect(tab.refreshSession()).resolves.toBe(false);

    expect(requests).toEqual(['POST /api/v1/auth/csrf']);
    expect(tab.getSession()).toBeNull();
  });

  it('holds the Web Lock across CSRF and refresh, with a fresh CSRF per rotation', async () => {
    const locks = installWebLocks();
    const firstTab = await loadTab();
    const secondTab = await loadTab();
    const sequence = useRotatingRefresh();

    await Promise.all([firstTab.refreshSession(), secondTab.refreshSession()]);

    expect(locks.request).toHaveBeenCalledTimes(2);
    expect(locks.request).toHaveBeenCalledWith('pt-manager.session.refresh', expect.any(Function));
    expect(sequence).toEqual(['csrf:csrf-1', 'refresh:csrf-1', 'csrf:csrf-2', 'refresh:csrf-2']);
  });

  it.each(['csrf', 'refresh'])(
    'keeps a valid session when %s fails transiently',
    async (endpoint) => {
      const tab = await loadTab();
      signIn(tab);
      useRotatingRefresh();
      server.use(
        http.post(`${API}/auth/${endpoint}`, () =>
          HttpResponse.json(problem('service_unavailable'), { status: 503 })
        )
      );

      await expect(tab.refreshSession()).rejects.toMatchObject({ status: 503 });

      expect(tab.getAccessToken()).toBe('access-1');
    }
  );

  it('does not resurrect a session cleared while the refresh was in flight', async () => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    let releaseRefresh: () => void = () => undefined;
    const refreshArrived = new Promise<void>((arrived) => {
      server.use(
        http.post(`${API}/auth/refresh`, async () => {
          arrived();
          await new Promise<void>((release) => {
            releaseRefresh = release;
          });
          return HttpResponse.json(sessionResponse({ access_token: 'access-2' }));
        })
      );
    });

    const pending = tab.refreshSession();
    await refreshArrived;
    tab.clearSession();
    releaseRefresh();

    await expect(pending).resolves.toBe(false);
    expect(tab.getSession()).toBeNull();
  });

  it('invalidates the session on a definitive failure and tells other tabs without credentials', async () => {
    installBroadcastChannel();
    const failingTab = await loadTab();
    const otherTab = await loadTab();
    signIn(failingTab);
    const remoteListener = vi.fn();
    otherTab.subscribeSessionInvalidated(remoteListener);
    useRotatingRefresh();
    server.use(
      http.post(`${API}/auth/refresh`, () =>
        HttpResponse.json(problem('authentication_refresh_token_revoked'), { status: 401 })
      )
    );

    await expect(failingTab.refreshSession()).resolves.toBe(false);

    expect(failingTab.getSession()).toBeNull();
    expect(remoteListener).toHaveBeenCalledTimes(1);
    expect(FakeBroadcastChannel.published).toEqual([{ type: 'session-invalidated' }]);
    expect(JSON.stringify(FakeBroadcastChannel.published)).not.toMatch(/access|csrf|token/i);
  });
});

describe('apiClient access cut in the middle of a session', () => {
  it('ends the session without a refresh when the account was archived or deactivated', async () => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    server.use(
      http.get(SUBSCRIPTION, () =>
        HttpResponse.json(problem('authentication_session_ended'), { status: 401 })
      )
    );

    const result = await tab.apiClient.GET('/api/v1/billing/subscription');

    expect(result.response.status).toBe(401);
    expect(count('POST /api/v1/auth/refresh')).toBe(0);
    expect(count('POST /api/v1/auth/csrf')).toBe(0);
    expect(tab.getSession()).toBeNull();
    // A mensagem é genérica: não diz que o cliente foi arquivado.
    expect(tab.peekPendingAuthNotice()).toBe('A tua sessão terminou. Entra novamente.');
  });

  it('ends the session on a blocked trainer account and keeps the reason and note for sign-in', async () => {
    const tab = await loadTab();
    signIn(tab);
    server.use(
      http.get(SUBSCRIPTION, () =>
        HttpResponse.json(
          {
            ...problem('trainer_account_blocked'),
            status: 403,
            block_reason: 'dangerous_information',
            block_note: 'Plano com doses perigosas.',
          },
          { status: 403 }
        )
      )
    );

    const result = await tab.apiClient.GET('/api/v1/billing/subscription');

    expect(result.response.status).toBe(403);
    expect(tab.getSession()).toBeNull();
    expect(tab.peekPendingAuthNotice()).toBe(
      'A tua conta está bloqueada. Motivo: Informação perigosa. Nota: Plano com doses perigosas.'
    );
  });

  it('writes a blocked account without a note when the note is null', async () => {
    const tab = await loadTab();
    signIn(tab);
    server.use(
      http.get(SUBSCRIPTION, () =>
        HttpResponse.json(
          {
            ...problem('trainer_account_blocked'),
            block_reason: 'malicious_content',
            block_note: null,
          },
          { status: 403 }
        )
      )
    );

    await tab.apiClient.GET('/api/v1/billing/subscription');

    expect(tab.peekPendingAuthNotice()).toBe(
      'A tua conta está bloqueada. Motivo: Conteúdo malicioso.'
    );
  });

  it('tells a client whose access period ended nothing but that it ended', async () => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    server.use(
      http.get(SUBSCRIPTION, () =>
        HttpResponse.json(problem('client_access_period_ended'), { status: 401 })
      )
    );

    await tab.apiClient.GET('/api/v1/billing/subscription');

    expect(count('POST /api/v1/auth/refresh')).toBe(0);
    expect(tab.getSession()).toBeNull();
    expect(tab.peekPendingAuthNotice()).toBe('O prazo de acesso terminou.');
  });

  it('cuts the session when the request repeated after a refresh finds the access ended', async () => {
    const tab = await loadTab();
    signIn(tab);
    useRotatingRefresh();
    let calls = 0;
    server.use(
      http.get(SUBSCRIPTION, () => {
        calls += 1;
        return calls === 1
          ? new HttpResponse(null, { status: 401 })
          : HttpResponse.json(problem('client_access_period_ended'), { status: 401 });
      })
    );

    const result = await tab.apiClient.GET('/api/v1/billing/subscription');

    expect(count('POST /api/v1/auth/refresh')).toBe(1);
    expect(calls).toBe(2);
    expect(result.response.status).toBe(401);
    expect(tab.getSession()).toBeNull();
    expect(tab.peekPendingAuthNotice()).toBe('O prazo de acesso terminou.');
  });

  it('leaves a failed sign-in alone: no session to cut and no notice left behind', async () => {
    const tab = await loadTab();
    server.use(
      http.post(`${API}/auth/login`, () =>
        HttpResponse.json(
          {
            ...problem('trainer_account_blocked'),
            block_reason: 'prohibited_content',
            block_note: null,
          },
          { status: 403 }
        )
      )
    );

    const result = await tab.apiClient.POST('/api/v1/auth/login', {
      body: { email: 'ana@ptmanager.test', password: 'correct-horse-1' },
    });

    expect(result.response.status).toBe(403);
    expect(tab.peekPendingAuthNotice()).toBeNull();
  });
});
