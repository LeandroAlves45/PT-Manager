import { describe, expect, it } from 'vitest';

import {
  exerciseDisplayName,
  formatDecimal,
  isWorkoutDayUnavailable,
  loggedSetLabel,
  parseReps,
  parseWeight,
  plannedSetChips,
  plannedSetLabel,
  prescriptionSummary,
  progressPercent,
  workoutErrorMessage,
  workoutFailureMessage,
  workoutSize,
  type PlannedSet,
} from '@/features/portal/lib/workout';
import { toApiProblem } from '@/shared/api/problem';

function planned(overrides: Partial<PlannedSet> = {}): PlannedSet {
  return {
    set_number: 1,
    planned_reps: 8,
    planned_weight_kg: 60,
    rest_seconds_min: 90,
    rest_seconds_max: 120,
    planned_rpe: 8,
    ...overrides,
  };
}

describe('prescriptionSummary', () => {
  it('summarises uniform sets with the rest range', () => {
    expect(prescriptionSummary([planned(), planned(), planned(), planned()])).toBe(
      '4 séries · 8 reps · 90–120 s'
    );
  });

  it('shows ranges when reps and rest vary between sets', () => {
    expect(
      prescriptionSummary([
        planned({ planned_reps: 12, rest_seconds_min: 60, rest_seconds_max: 60 }),
        planned({ planned_reps: 8, rest_seconds_min: 90, rest_seconds_max: 90 }),
      ])
    ).toBe('2 séries · 12-8 reps · 60–90 s');
  });

  it('omits what no set prescribes instead of inventing a zero', () => {
    expect(
      prescriptionSummary([
        planned({ planned_reps: null, rest_seconds_min: null, rest_seconds_max: null }),
      ])
    ).toBe('1 série');
  });

  it('shows the rep range with the larger value first', () => {
    expect(
      prescriptionSummary([
        planned({ planned_reps: 10, planned_reps_max: 12 }),
        planned({ planned_reps: 10, planned_reps_max: 12 }),
      ])
    ).toBe('2 séries · 12-10 reps · 90–120 s');
  });

  it('falls back to the overall span when the sets have different targets', () => {
    expect(
      prescriptionSummary([
        planned({ planned_reps: 10, planned_reps_max: 12 }),
        planned({ planned_reps: 8, planned_reps_max: null }),
      ])
    ).toBe('2 séries · 12-8 reps · 90–120 s');
  });

  it('shows a single value when the rest is fixed', () => {
    expect(prescriptionSummary([planned({ rest_seconds_min: 90, rest_seconds_max: null })])).toBe(
      '1 série · 8 reps · 90 s'
    );
  });
});

describe('workoutSize', () => {
  it('counts exercises and sets without an estimated duration', () => {
    expect(workoutSize([{ sets: [1, 2, 3] }, { sets: [1, 2] }])).toBe('2 exercícios · 5 séries');
    expect(workoutSize([{ sets: [1] }])).toBe('1 exercício · 1 série');
  });
});

describe('plannedSetLabel', () => {
  it('lists every planned field of a set', () => {
    expect(plannedSetLabel(planned({ planned_weight_kg: 62.5, planned_rpe: 8.5 }))).toBe(
      'Série 1 · 8 reps · 62,5 kg · RPE 8,5 · 90–120 s'
    );
  });

  it('shows the rep range of a set', () => {
    expect(plannedSetLabel(planned({ planned_reps: 10, planned_reps_max: 12 }))).toBe(
      'Série 1 · 12-10 reps · 60 kg · RPE 8 · 90–120 s'
    );
    expect(plannedSetLabel(planned({ planned_reps: 8, planned_reps_max: null }))).toContain(
      '· 8 reps ·'
    );
  });

  it('keeps only the set number when nothing is planned', () => {
    expect(
      plannedSetLabel(
        planned({
          set_number: 3,
          planned_reps: null,
          planned_weight_kg: null,
          planned_rpe: null,
          rest_seconds_min: null,
          rest_seconds_max: null,
        })
      )
    ).toBe('Série 3');
  });
});

describe('progressPercent', () => {
  it.each([
    [0, 0, 0],
    [3, 7, 43],
    [2, 2, 100],
  ])('%i of %i is %i %%', (logged, plannedSets, expected) => {
    expect(progressPercent(logged, plannedSets)).toBe(expected);
  });
});

describe('formatDecimal', () => {
  it('uses a comma and no grouping', () => {
    expect(formatDecimal(62.5)).toBe('62,5');
    expect(formatDecimal(1000)).toBe('1000');
  });
});

describe('parseWeight', () => {
  it.each([
    ['62,5', 62.5],
    ['62.5', 62.5],
    [' 60 ', 60],
    ['0', 0],
    ['1000', 1000],
    ['1000,00', 1000],
  ])('reads "%s" as %d', (text, expected) => {
    expect(parseWeight(text)).toBe(expected);
  });

  it.each(['', 'abc', '-1', '1000,01', '62,555', '1e3', '62,'])('rejects "%s"', (text) => {
    expect(parseWeight(text)).toBeNull();
  });
});

describe('parseReps', () => {
  it.each([
    ['8', 8],
    ['0', 0],
    ['100', 100],
  ])('reads "%s" as %i', (text, expected) => {
    expect(parseReps(text)).toBe(expected);
  });

  it.each(['', '101', '8,5', '-1', 'oito'])('rejects "%s"', (text) => {
    expect(parseReps(text)).toBeNull();
  });
});

describe('workoutErrorMessage', () => {
  it('translates known codes and falls back for unknown ones', () => {
    expect(workoutErrorMessage('workout_already_completed')).toBe(
      'O treino já foi concluído. Já não podes desmarcar séries.'
    );
    expect(workoutErrorMessage('training_weight_invalid')).toBe(
      'O peso tem de estar entre 0 e 1000 kg.'
    );
    expect(workoutErrorMessage('something_else')).toBe(
      'Não foi possível guardar. Tenta novamente.'
    );
    expect(workoutErrorMessage(null)).toBe('Não foi possível guardar. Tenta novamente.');
  });
});

describe('workoutFailureMessage', () => {
  it('reads the field code of a validation failure, whose title is always validation_failed', () => {
    const failure = toApiProblem(400, {
      title: 'validation_failed',
      errors: [{ field: 'reps_done', code: 'training_reps_done_invalid', message: 'x' }],
    });

    expect(workoutFailureMessage(failure)).toBe('As repetições têm de estar entre 0 e 100.');
  });

  it('uses the problem code of a conflict and falls back for a network failure', () => {
    expect(workoutFailureMessage(toApiProblem(409, { title: 'training_plan_inactive' }))).toBe(
      'O teu plano mudou. Atualiza o treino.'
    );
    expect(workoutFailureMessage(new TypeError('Failed to fetch'))).toBe(
      'Não foi possível guardar. Tenta novamente.'
    );
  });
});

describe('exerciseDisplayName', () => {
  it('replaces the masked English name of a blocked exercise', () => {
    expect(
      exerciseDisplayName({ exercise_name: 'Unavailable exercise', is_unavailable: true })
    ).toBe('Exercício indisponível');
    expect(exerciseDisplayName({ exercise_name: 'Agachamento', is_unavailable: false })).toBe(
      'Agachamento'
    );
  });
});

describe('plannedSetChips', () => {
  it('splits the prescription in one chip per field, reps first with the larger value first', () => {
    expect(plannedSetChips(planned({ planned_reps: 10, planned_reps_max: 12 }))).toEqual([
      '12-10 reps',
      '60 kg',
      'RPE 8',
      'Descanso 90–120 s',
    ]);
  });

  it('omits what the set does not prescribe', () => {
    expect(
      plannedSetChips(
        planned({
          planned_reps: null,
          planned_weight_kg: null,
          planned_rpe: null,
          rest_seconds_min: 60,
          rest_seconds_max: 60,
        })
      )
    ).toEqual(['Descanso 60 s']);
  });
});

describe('loggedSetLabel', () => {
  it('writes the load and the reps, with the RPE only when it was given', () => {
    expect(loggedSetLabel({ weight_kg: 62.5, reps_done: 8, rpe: 8.5 })).toBe(
      '62,5 kg × 8 reps · RPE 8,5'
    );
    expect(loggedSetLabel({ weight_kg: 60, reps_done: 9, rpe: null })).toBe('60 kg × 9 reps');
  });
});

describe('isWorkoutDayUnavailable', () => {
  it('recognises the validation failure of a day that cannot be consulted', () => {
    const unavailable = toApiProblem(400, {
      title: 'validation_failed',
      errors: [{ field: 'Date', code: 'portal_workout_day_not_available', message: 'x' }],
    });
    expect(isWorkoutDayUnavailable(unavailable)).toBe(true);
  });

  it('ignores other failures', () => {
    expect(isWorkoutDayUnavailable(toApiProblem(400, { title: 'validation_failed' }))).toBe(false);
    expect(
      isWorkoutDayUnavailable(toApiProblem(404, { title: 'portal_training_plan_not_available' }))
    ).toBe(false);
    expect(isWorkoutDayUnavailable(new Error('network'))).toBe(false);
  });
});
