import { describe, expect, it } from 'vitest';

import { formatRepsTarget } from '@/shared/lib/reps';

describe('formatRepsTarget', () => {
  it('writes a range with the larger value first', () => {
    expect(formatRepsTarget(10, 12)).toBe('12-10');
  });

  it('writes a single target when there is no maximum', () => {
    expect(formatRepsTarget(8)).toBe('8');
    expect(formatRepsTarget(8, null)).toBe('8');
    expect(formatRepsTarget(8, undefined)).toBe('8');
  });

  it('writes a single target when the maximum equals the minimum', () => {
    expect(formatRepsTarget(8, 8)).toBe('8');
  });

  it('returns an empty text without a minimum, even with a maximum', () => {
    expect(formatRepsTarget(null)).toBe('');
    expect(formatRepsTarget(null, 12)).toBe('');
    expect(formatRepsTarget(undefined, 12)).toBe('');
  });
});
