import { describe, expect, it } from 'vitest';

import { formatLongDate } from '@/domain/date';

describe('formatLongDate', () => {
  it('escreve a data por extenso em português', () => {
    expect(formatLongDate('2026-10-28')).toBe('28 de outubro de 2026');
  });

  it('não muda de dia com o fuso (1 de janeiro continua 1 de janeiro)', () => {
    expect(formatLongDate('2027-01-01')).toBe('1 de janeiro de 2027');
  });

  it.each(['28/10/2026', '2026-10', ''])('recusa %s', (value) => {
    expect(() => formatLongDate(value)).toThrow(/AAAA-MM-DD/);
  });
});
