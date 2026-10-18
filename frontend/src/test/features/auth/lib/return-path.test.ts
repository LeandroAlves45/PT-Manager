import { describe, expect, it } from 'vitest';

import { getReturnPath } from '@/features/auth/lib/return-path';

describe('getReturnPath', () => {
  it('keeps an internal destination with query string and hash', () => {
    expect(getReturnPath({ from: '/trainer/clients?search=ana&page=2#top' })).toBe(
      '/trainer/clients?search=ana&page=2#top'
    );
  });

  it.each([
    '//evil.example',
    '//evil.example/phish',
    '/\\evil.example',
    '/\\/evil.example',
    '/.//evil.example',
    'https://evil.example/phish',
    'javascript:alert(1)',
    'trainer',
    '',
  ])('rejects %j', (from) => {
    expect(getReturnPath({ from })).toBeNull();
  });

  it.each([null, undefined, {}, { from: 42 }, 'texto'])(
    'ignores a state shaped like %j',
    (state) => {
      expect(getReturnPath(state)).toBeNull();
    }
  );
});
