import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  nativeSessionResponse,
  resetNativeFake,
  storedTokens,
  storeTokens,
} from '@/test/native-fakes';

const fake = vi.hoisted(() => ({
  native: true,
  store: new Map<string, string>(),
  failRead: false,
  lastAccess: undefined as number | undefined,
}));

vi.mock('@/shared/native/platform', () => ({ isNativeApp: () => fake.native }));
vi.mock('@aparajita/capacitor-secure-storage', async () =>
  (await import('@/test/native-fakes')).secureStorageModule(fake)
);

const NATIVE_REFRESH = `${API}/native/auth/refresh`;
const ACCOUNT = `${API}/auth/account`;

/** Módulos novos por teste: o `credentials` do cliente decide-se ao carregar. */
async function loadApp() {
  vi.resetModules();
  const client = await import('@/shared/api/client');
  const session = await import('@/shared/api/session');
  return { ...client, ...session };
}

let seen: Request[];

beforeEach(() => {
  resetNativeFake(fake);
  seen = [];
  server.events.on('request:start', ({ request }) => {
    seen.push(request.clone());
  });
});

afterEach(() => server.events.removeAllListeners());

const paths = () => seen.map((request) => new URL(request.url).pathname);

describe('native session refresh', () => {
  it('sends the stored pair in the body, without cookies or the CSRF bootstrap, and stores the rotated pair', async () => {
    storeTokens(fake, 'refresh-1', 'csrf-1');
    const bodies: unknown[] = [];
    server.use(
      http.post(NATIVE_REFRESH, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(
          nativeSessionResponse({
            access_token: 'access-2',
            refresh_token: 'refresh-2',
            csrf_token: 'csrf-2',
          })
        );
      })
    );
    const app = await loadApp();

    expect(await app.refreshSession()).toBe(true);

    expect(bodies).toEqual([{ refresh_token: 'refresh-1', csrf_token: 'csrf-1' }]);
    expect(paths()).toEqual(['/api/v1/native/auth/refresh']);
    expect(seen[0]?.credentials).toBe('omit');
    expect(app.getAccessToken()).toBe('access-2');
    expect(storedTokens(fake)).toEqual({ refreshToken: 'refresh-2', csrfToken: 'csrf-2' });
    // No iOS o par fica só neste aparelho (sem backup nem iCloud).
    expect(fake.lastAccess).toBe(1);
  });

  it('stays anonymous without a request when nothing is stored', async () => {
    const app = await loadApp();

    expect(await app.refreshSession()).toBe(false);
    expect(seen).toHaveLength(0);
    expect(app.getSession()).toBeNull();
  });

  it.each([401, 403])('forgets the stored pair when the server refuses it (%s)', async (status) => {
    storeTokens(fake, 'refresh-1', 'csrf-1');
    server.use(
      http.post(NATIVE_REFRESH, () =>
        HttpResponse.json(problem('authentication_refresh_session_invalid'), { status })
      )
    );
    const app = await loadApp();

    expect(await app.refreshSession()).toBe(false);
    expect(storedTokens(fake)).toBeNull();
    expect(app.getSession()).toBeNull();
  });

  it('treats an unreadable stored value as no session and deletes it', async () => {
    storeTokens(fake, 'refresh-1', 'csrf-1');
    fake.failRead = true;
    const app = await loadApp();

    expect(await app.refreshSession()).toBe(false);
    expect(seen).toHaveLength(0);
    expect(fake.store.size).toBe(0);
  });

  it('keeps the stored pair when the server is down (it may still be valid)', async () => {
    storeTokens(fake, 'refresh-1', 'csrf-1');
    server.use(http.post(NATIVE_REFRESH, () => new HttpResponse(null, { status: 503 })));
    const app = await loadApp();

    await expect(app.refreshSession()).rejects.toBeTruthy();
    expect(storedTokens(fake)).toEqual({ refreshToken: 'refresh-1', csrfToken: 'csrf-1' });
  });

  it('renews on a 401 and repeats the request once with the new access token', async () => {
    storeTokens(fake, 'refresh-1', 'csrf-1');
    server.use(
      http.post(NATIVE_REFRESH, () =>
        HttpResponse.json(
          nativeSessionResponse({ access_token: 'access-2', refresh_token: 'refresh-2' })
        )
      ),
      http.get(ACCOUNT, ({ request }) =>
        request.headers.get('Authorization') === 'Bearer access-2'
          ? HttpResponse.json({ email: 'ana@ptmanager.test' })
          : new HttpResponse(null, { status: 401 })
      )
    );
    const app = await loadApp();
    app.setSession(app.toSession(nativeSessionResponse()));

    const result = await app.apiClient.GET('/api/v1/auth/account');

    expect(result.response.status).toBe(200);
    expect(paths()).toEqual([
      '/api/v1/auth/account',
      '/api/v1/native/auth/refresh',
      '/api/v1/auth/account',
    ]);
  });
});

describe('native requests', () => {
  it('never send cookies nor the CSRF header, which the app CORS policy refuses', async () => {
    server.use(http.get(ACCOUNT, () => HttpResponse.json({ email: 'ana@ptmanager.test' })));
    const app = await loadApp();
    app.setSession(app.toSession(nativeSessionResponse()));

    await app.apiClient.GET('/api/v1/auth/account');

    expect(seen[0]?.credentials).toBe('omit');
    expect(seen[0]?.headers.get('X-CSRF-Token')).toBeNull();
    expect(seen[0]?.headers.get('Authorization')).toBe('Bearer access-1');
  });

  it('the web keeps cookies and the CSRF header on the same route', async () => {
    fake.native = false;
    server.use(http.get(ACCOUNT, () => HttpResponse.json({ email: 'ana@ptmanager.test' })));
    const app = await loadApp();
    app.setSession(app.toSession(nativeSessionResponse()));

    await app.apiClient.GET('/api/v1/auth/account');

    expect(seen[0]?.credentials).toBe('include');
    expect(seen[0]?.headers.get('X-CSRF-Token')).toBe('csrf-1');
  });
});
