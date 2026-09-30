import { describe, expect, it } from 'vitest';

import {
  formatMuscleGroups,
  MUSCLE_GROUPS,
  parseMuscleGroups,
  serializeMuscleGroups,
} from '@/shared/lib/muscleGroups';

describe('muscle groups', () => {
  it('lists the 14 codes of MuscleGroupCatalog in canonical order', () => {
    expect(MUSCLE_GROUPS.map((group) => group.code)).toEqual([
      'chest',
      'back',
      'lats',
      'traps',
      'lower_back',
      'shoulders',
      'biceps',
      'triceps',
      'forearms',
      'core',
      'glutes',
      'quadriceps',
      'hamstrings',
      'calves',
    ]);
  });

  it('parses a stored value like the server: trimmed, lower case, no repeats, canonical order', () => {
    expect(parseMuscleGroups(' Quadriceps, glutes,quadriceps ,,')).toEqual([
      'glutes',
      'quadriceps',
    ]);
    expect(parseMuscleGroups(null)).toEqual([]);
  });

  it('drops free-text codes from before the closed list', () => {
    expect(parseMuscleGroups('legs,chest')).toEqual(['chest']);
  });

  it('serializes in canonical order, or null when nothing is chosen', () => {
    expect(serializeMuscleGroups(['triceps', 'chest', 'triceps'])).toBe('chest,triceps');
    expect(serializeMuscleGroups([])).toBeNull();
  });

  it('formats labels for lists and keeps unknown codes readable', () => {
    expect(formatMuscleGroups('lower_back,calves')).toBe('Lombar, Gémeos');
    expect(formatMuscleGroups('legs')).toBe('legs');
    expect(formatMuscleGroups(null)).toBe('—');
  });
});
