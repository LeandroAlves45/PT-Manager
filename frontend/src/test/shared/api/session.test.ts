import { describe, expect, it, vi } from 'vitest';

import {
  clearSession,
  getAccessToken,
  getCsrfToken,
  getSession,
  homeRouteFor,
  setCsrfToken,
  setSession,
  subscribe,
  toSession,
} from '@/shared/api/session';
import { sessionResponse } from '@/test/msw/handlers';

describe('session store', () => {
  it('maps a valid API response to the internal session', () => {
    expect(toSession(sessionResponse({ role: 'client', trainer_id: null }))).toEqual({
      userId: '11111111-1111-1111-1111-111111111111',
      trainerId: null,
      role: 'client',
      accessToken: 'access-1',
      accessTokenExpiresAt: '2026-09-23T12:00:00Z',
      csrfToken: 'csrf-session-1',
    });
  });

  it('rejects an unknown role instead of guessing permissions', () => {
    expect(() => toSession(sessionResponse({ role: 'admin' }))).toThrow(/admin/);
  });

  it('uses the bootstrap CSRF token while there is no session', () => {
    setCsrfToken('csrf-bootstrap');

    expect(getSession()).toBeNull();
    expect(getCsrfToken()).toBe('csrf-bootstrap');
  });

  it('prefers the CSRF token of the authenticated session', () => {
    setCsrfToken('csrf-bootstrap');
    setSession(toSession(sessionResponse({ csrf_token: 'csrf-newer' })));

    expect(getCsrfToken()).toBe('csrf-newer');
    expect(getAccessToken()).toBe('access-1');
  });

  it('wipes every credential from memory on sign-out and notifies subscribers', () => {
    const listener = vi.fn();
    const unsubscribe = subscribe(listener);
    setSession(toSession(sessionResponse()));

    clearSession();
    unsubscribe();

    expect(getSession()).toBeNull();
    expect(getAccessToken()).toBeNull();
    expect(getCsrfToken()).toBeNull();
    expect(listener).toHaveBeenCalledTimes(2);
  });

  it.each([
    ['superuser', '/admin'],
    ['trainer', '/trainer'],
    ['client', '/portal'],
  ] as const)('sends %s to %s', (role, route) => {
    expect(homeRouteFor(role)).toBe(route);
  });
});
