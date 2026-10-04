import { describe, expect, it } from 'vitest';

import {
  CHECKOUT_POLL_LIMIT,
  CHECKOUT_POLL_MS,
  checkoutPollInterval,
} from '@/features/billing/api/billing';

const free = { tier: 'FREE', status: 'ACTIVE' };

describe('checkoutPollInterval', () => {
  it('keeps reading while the plan is still free', () => {
    expect(checkoutPollInterval({ data: free, dataUpdateCount: 1, errorUpdateCount: 0 })).toBe(
      CHECKOUT_POLL_MS
    );
  });

  it('stops once the plan is paid and active', () => {
    expect(
      checkoutPollInterval({
        data: { tier: 'STARTER', status: 'ACTIVE' },
        dataUpdateCount: 2,
        errorUpdateCount: 0,
      })
    ).toBe(false);
  });

  it('stops after the limit even when every read failed', () => {
    expect(
      checkoutPollInterval({ dataUpdateCount: 0, errorUpdateCount: CHECKOUT_POLL_LIMIT })
    ).toBe(false);
    expect(checkoutPollInterval({ data: free, dataUpdateCount: 1, errorUpdateCount: 8 })).toBe(
      CHECKOUT_POLL_MS
    );
  });
});
