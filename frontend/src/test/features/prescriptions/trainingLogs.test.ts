import { describe, expect, it } from 'vitest';

import { localDay } from '@/features/prescriptions/lib/training-logs';

// O dia de uma série é o do fuso do trainer (FL-003), não o do browser (TZ dos testes: Lisboa).
describe('localDay', () => {
  it('uses the trainer time zone, not the browser one', () => {
    const instant = '2026-10-20T23:30:00Z';

    expect(localDay(instant, 'Europe/Lisbon')).toBe('2026-10-21');
    expect(localDay(instant, 'America/New_York')).toBe('2026-10-20');
    expect(localDay(instant, 'Asia/Tokyo')).toBe('2026-10-21');
  });

  it('keeps a late summer evening in Lisbon on the same day', () => {
    // 22:30 UTC em agosto são 23:30 em Lisboa (UTC+1): ainda é o mesmo dia.
    expect(localDay('2026-08-10T22:30:00Z', 'Europe/Lisbon')).toBe('2026-08-10');
  });
});
