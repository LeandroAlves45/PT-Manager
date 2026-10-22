import { describe, expect, it } from 'vitest';

import { pastDayOptionLabel } from '@/features/portal/lib/schedule';

describe('pastDayOptionLabel', () => {
  it('writes the weekday of the local date, the day/month and the plan week', () => {
    expect(pastDayOptionLabel('2026-10-01', 1)).toBe('Qui, 01/10 · Semana 1');
    expect(pastDayOptionLabel('2026-09-29', 2)).toBe('Ter, 29/09 · Semana 2');
  });
});
