import { describe, expect, it } from 'vitest';

import {
  checkInIssues,
  checkInStateMessage,
  emptyCheckInValues,
  parseAdherence,
  parseBodyFat,
  parseBodyWeight,
  parseDecimal,
  parseMeasurement,
  serverCheckInIssues,
  toCheckInAnswer,
  type CheckInFormValues,
} from '@/features/portal/lib/checkIn';
import { toApiProblem } from '@/shared/api/problem';

function values(overrides: Partial<CheckInFormValues> = {}): CheckInFormValues {
  return { ...emptyCheckInValues(), weight_kg: '71,5', ...overrides };
}

describe('parseDecimal', () => {
  it.each([
    ['17,5', 17.5],
    ['17.5', 17.5],
    [' 80 ', 80],
    ['71,55', 71.55],
  ])('reads %s', (text, expected) => {
    expect(parseDecimal(text)).toBe(expected);
  });

  it.each(['', 'abc', '-1', '71,555', '1e3', '7,', ',5'])('refuses "%s"', (text) => {
    expect(parseDecimal(text)).toBeNull();
  });
});

describe('check-in parsers', () => {
  it('accepts a body weight above 0 and up to 1000 kg', () => {
    expect(parseBodyWeight('0,01')).toBe(0.01);
    expect(parseBodyWeight('1000')).toBe(1000);
    expect(parseBodyWeight('0')).toBeNull();
    expect(parseBodyWeight('1000,01')).toBeNull();
  });

  it('accepts body fat strictly between 0 and 100', () => {
    expect(parseBodyFat('0,1')).toBe(0.1);
    expect(parseBodyFat('99,99')).toBe(99.99);
    expect(parseBodyFat('0')).toBeNull();
    expect(parseBodyFat('100')).toBeNull();
  });

  it('accepts a measurement above 0 and up to 1000 cm', () => {
    expect(parseMeasurement('82,5')).toBe(82.5);
    expect(parseMeasurement('1000')).toBe(1000);
    expect(parseMeasurement('0')).toBeNull();
    expect(parseMeasurement('1000,5')).toBeNull();
  });

  it('accepts adherence as an integer from 0 to 100', () => {
    expect(parseAdherence('0')).toBe(0);
    expect(parseAdherence('100')).toBe(100);
    expect(parseAdherence('101')).toBeNull();
    expect(parseAdherence('80,5')).toBeNull();
    expect(parseAdherence('')).toBeNull();
  });
});

describe('checkInIssues', () => {
  it('finds nothing wrong when only the weight is filled', () => {
    expect(checkInIssues(values())).toEqual([]);
  });

  it('asks for the weight when it is empty', () => {
    expect(checkInIssues(values({ weight_kg: '  ' }))).toEqual([
      { field: 'weight_kg', step: 0, message: 'Indica o peso.' },
    ]);
  });

  it('puts each problem on the step where its field lives', () => {
    const filled = values({
      weight_kg: '0',
      body_fat_percentage: '100',
      training_adherence_score: '101',
      nutrition_adherence_score: 'muito',
      notes: 'a'.repeat(2001),
    });
    filled.measurements.chest_cm = 'abc';
    filled.feedback.digestion = 'b'.repeat(2001);

    const issues = checkInIssues(filled);

    expect(issues.map((issue) => [issue.field, issue.step])).toEqual([
      ['weight_kg', 0],
      ['body_fat_percentage', 0],
      ['measurements.chest_cm', 1],
      ['feedback.digestion', 2],
      ['training_adherence_score', 2],
      ['nutrition_adherence_score', 2],
      ['notes', 2],
    ]);
    expect(issues[0]?.message).toBe('Indica um peso maior que 0 e até 1000 kg.');
    expect(issues[2]?.message).toBe('Indica uma medida maior que 0 e até 1000 cm.');
    expect(issues[3]?.message).toBe('O texto não pode exceder 2000 caracteres.');
    expect(issues[4]?.message).toBe('Indica um valor inteiro de 0 a 100.');
  });

  it('accepts a text with exactly 2000 characters', () => {
    expect(checkInIssues(values({ notes: 'a'.repeat(2000) }))).toEqual([]);
  });
});

describe('toCheckInAnswer', () => {
  it('sends empty blocks as null, never as zeros', () => {
    expect(toCheckInAnswer(values())).toEqual({
      weight_kg: 71.5,
      body_fat_percentage: null,
      notes: null,
      body_measurements: null,
      feedback: null,
      training_adherence_score: null,
      nutrition_adherence_score: null,
    });
  });

  it('keeps a zero adherence as zero and fills only the written fields of a block', () => {
    const filled = values({
      body_fat_percentage: '17,25',
      training_adherence_score: '0',
      nutrition_adherence_score: '85',
      notes: '  Semana boa.  ',
    });
    filled.measurements.waist_cm = '82,5';
    filled.feedback.energy_levels = ' Alta ';

    const answer = toCheckInAnswer(filled);

    expect(answer.body_fat_percentage).toBe(17.25);
    expect(answer.training_adherence_score).toBe(0);
    expect(answer.nutrition_adherence_score).toBe(85);
    expect(answer.notes).toBe('Semana boa.');
    expect(answer.body_measurements).toEqual({
      waist_cm: 82.5,
      hip_cm: null,
      chest_cm: null,
      right_arm_cm: null,
      left_arm_cm: null,
      right_thigh_cm: null,
      left_thigh_cm: null,
      right_calf_cm: null,
      left_calf_cm: null,
    });
    expect(answer.feedback).toEqual({
      appetite: null,
      digestion: null,
      training_load: null,
      recovery_sleep: null,
      energy_levels: 'Alta',
      body_response: null,
    });
  });
});

describe('serverCheckInIssues', () => {
  it('maps the backend fields, including a measurement reported by its position', () => {
    const issues = serverCheckInIssues([
      { field: 'WeightKg', code: 'weight_invalid', message: '' },
      { field: 'BodyMeasurements[2]', code: 'body_measurement_invalid', message: '' },
      { field: 'Feedback[4]', code: 'check_in_feedback_too_long', message: '' },
      { field: 'NutritionAdherenceScore', code: 'nutrition_adherence_invalid', message: '' },
    ]);

    expect(issues.map((issue) => [issue.field, issue.step])).toEqual([
      ['weight_kg', 0],
      ['measurements.chest_cm', 1],
      ['feedback.energy_levels', 2],
      ['nutrition_adherence_score', 2],
    ]);
    expect(issues[1]?.message).toBe('Indica uma medida maior que 0 e até 1000 cm.');
  });

  it.each([
    ['WeightKg', 'weight_invalid', 'weight_kg', 0, 'Indica um peso maior que 0 e até 1000 kg.'],
    [
      'BodyFatPercentage',
      'body_fat_percentage_invalid',
      'body_fat_percentage',
      0,
      'Indica uma percentagem maior que 0 e menor que 100.',
    ],
    [
      'TrainingAdherenceScore',
      'training_adherence_invalid',
      'training_adherence_score',
      2,
      'Indica um valor inteiro de 0 a 100.',
    ],
    [
      'NutritionAdherenceScore',
      'nutrition_adherence_invalid',
      'nutrition_adherence_score',
      2,
      'Indica um valor inteiro de 0 a 100.',
    ],
    ['Notes', 'notes_too_long', 'notes', 2, 'O texto não pode exceder 2000 caracteres.'],
    [
      'Feedback[0]',
      'check_in_feedback_too_long',
      'feedback.appetite',
      2,
      'O texto não pode exceder 2000 caracteres.',
    ],
  ])('puts %s (%s) on its own field, step and message', (field, code, target, step, message) => {
    expect(serverCheckInIssues([{ field, code, message: '' }])).toEqual([
      { field: target, step, message },
    ]);
  });

  it('leaves out what the form does not know, so the screen shows the general message', () => {
    expect(
      serverCheckInIssues([
        { field: 'CheckInId', code: 'check_in_id_required', message: '' },
        { field: 'BodyMeasurements[9]', code: 'body_measurement_invalid', message: '' },
        { field: 'constructor', code: 'weight_invalid', message: '' },
      ])
    ).toEqual([]);
  });
});

describe('checkInStateMessage', () => {
  it.each([
    ['check_in_wrong_day', 'Esta avaliação só podia ser respondida no próprio dia.'],
    ['check_in_already_answered', 'Esta avaliação já foi respondida.'],
    ['check_in_cancelled', 'Esta avaliação foi cancelada pelo teu personal trainer.'],
    ['check_in_not_found', 'Esta avaliação já não existe.'],
    ['assessment_client_inactive', 'A tua ficha não está ativa. Fala com o teu personal trainer.'],
  ])('explains %s', (code, message) => {
    expect(checkInStateMessage(toApiProblem(409, { title: code }))).toBe(message);
  });

  it('is null for a validation failure, an unknown code or a network error', () => {
    expect(checkInStateMessage(toApiProblem(400, { title: 'validation_failed' }))).toBeNull();
    expect(checkInStateMessage(toApiProblem(500, { title: 'constructor' }))).toBeNull();
    expect(checkInStateMessage(new TypeError('Failed to fetch'))).toBeNull();
  });
});
