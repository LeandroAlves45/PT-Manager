import { describe, expect, it, vi } from 'vitest';

import { getGoogleClientId } from '@/shared/config/env';

describe('getGoogleClientId', () => {
  it('is null when the variable is missing or blank', () => {
    expect(getGoogleClientId()).toBeNull();

    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '   ');
    expect(getGoogleClientId()).toBeNull();
  });

  it('reads the variable at call time and trims it', () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', ' abc.apps.googleusercontent.com ');

    expect(getGoogleClientId()).toBe('abc.apps.googleusercontent.com');
  });
});
