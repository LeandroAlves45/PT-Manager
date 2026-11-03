import { describe, expect, it } from 'vitest';

import {
  dashboardGreeting,
  firstName,
  isFirstVisit,
  markWelcomed,
} from '@/features/trainer-dashboard/lib/greeting';

describe('firstName', () => {
  it.each([
    ['Rita Moreira', 'Rita'],
    ['  Ana  ', 'Ana'],
    ['', null],
    [null, null],
    [undefined, null],
  ])('takes the first name of %s', (value, expected) => {
    expect(firstName(value)).toBe(expected);
  });
});

describe('dashboardGreeting', () => {
  it('welcomes on the first visit', () => {
    expect(dashboardGreeting('Rita Moreira', true, '2026-11-03')).toEqual({
      text: 'Boas-vindas, Rita',
      emoji: null,
    });
    expect(dashboardGreeting(null, true, '2026-11-03').text).toBe('Boas-vindas');
  });

  it('keeps the same phrase all day and changes it the next day', () => {
    const today = dashboardGreeting('Rita Moreira', false, '2026-11-03');
    expect(dashboardGreeting('Rita Moreira', false, '2026-11-03')).toEqual(today);

    const week = ['03', '04', '05'].map(
      (day) => dashboardGreeting('Rita Moreira', false, `2026-11-${day}`).text
    );
    expect(new Set(week).size).toBe(3);
    expect(week.every((text) => text.includes('Rita'))).toBe(true);
    expect(week.some((text) => text.startsWith('Boas-vindas'))).toBe(false);
  });

  it('adds the emoji only as decoration, outside the text', () => {
    const phrases = ['03', '04', '05'].map((day) =>
      dashboardGreeting('Rita', false, `2026-11-${day}`)
    );
    const withEmoji = phrases.filter((phrase) => phrase.emoji !== null);
    expect(withEmoji).toHaveLength(1);
    expect(withEmoji[0]!.text).not.toContain('💪');
  });
});

describe('first visit memory', () => {
  it('is the first visit until it is marked, per user', () => {
    expect(isFirstVisit('user-a')).toBe(true);
    markWelcomed('user-a');
    expect(isFirstVisit('user-a')).toBe(false);
    expect(isFirstVisit('user-b')).toBe(true);
  });

  it('never welcomes without a user', () => {
    expect(isFirstVisit(undefined)).toBe(false);
  });
});
