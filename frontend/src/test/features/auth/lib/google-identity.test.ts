import { afterEach, describe, expect, it, vi } from 'vitest';

import { loadGoogleIdentity, resetGoogleIdentityLoader } from '@/features/auth/lib/google-identity';

const SELECTOR = 'script[src="https://accounts.google.com/gsi/client"]';

function scripts(): NodeListOf<HTMLScriptElement> {
  return document.head.querySelectorAll<HTMLScriptElement>(SELECTOR);
}

function api(): GoogleAccountsId {
  return { initialize: vi.fn(), renderButton: vi.fn() };
}

afterEach(() => {
  resetGoogleIdentityLoader();
  Reflect.deleteProperty(window, 'google');
});

describe('loadGoogleIdentity', () => {
  it('adds a single script for concurrent callers and resolves them all with the same API', async () => {
    const first = loadGoogleIdentity();
    const second = loadGoogleIdentity();

    expect(scripts()).toHaveLength(1);

    const google = api();
    window.google = { accounts: { id: google } };
    scripts()[0]?.dispatchEvent(new Event('load'));

    await expect(first).resolves.toBe(google);
    await expect(second).resolves.toBe(google);
  });

  it('resolves immediately, without adding a script, when the library is already there', async () => {
    const google = api();
    window.google = { accounts: { id: google } };

    await expect(loadGoogleIdentity()).resolves.toBe(google);

    expect(scripts()).toHaveLength(0);
  });

  it('rejects, removes the script and allows a retry when loading fails', async () => {
    const failed = loadGoogleIdentity();
    scripts()[0]?.dispatchEvent(new Event('error'));

    await expect(failed).rejects.toThrow();
    expect(scripts()).toHaveLength(0);

    const retry = loadGoogleIdentity();
    expect(scripts()).toHaveLength(1);
    window.google = { accounts: { id: api() } };
    scripts()[0]?.dispatchEvent(new Event('load'));
    await expect(retry).resolves.toBeDefined();
  });

  it('rejects when the script loads but does not expose the API', async () => {
    const loading = loadGoogleIdentity();
    scripts()[0]?.dispatchEvent(new Event('load'));

    await expect(loading).rejects.toThrow();
  });

  it('rejects on timeout and releases the singleton', async () => {
    vi.useFakeTimers();
    const outcome = loadGoogleIdentity(1000).then(
      () => null,
      (failure: unknown) => failure
    );

    await vi.advanceTimersByTimeAsync(1000);

    const failure = await outcome;
    expect(failure instanceof Error && failure.message).toContain('Timeout');
    expect(scripts()).toHaveLength(0);
  });
});
