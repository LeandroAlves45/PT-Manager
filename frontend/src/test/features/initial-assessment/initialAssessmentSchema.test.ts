import { describe, expect, it } from 'vitest';

import {
  assessmentIssues,
  emptyAssessmentValues,
  MAX_TEXT_LENGTH,
  serverAssessmentIssues,
} from '@/features/initial-assessment/lib/initialAssessmentSchema';

const TOO_LONG = 'a'.repeat(MAX_TEXT_LENGTH + 1);

describe('initial assessment text limits', () => {
  it('refuses objectives and medical conditions above the server limit', () => {
    const issues = assessmentIssues({
      ...emptyAssessmentValues(),
      goals: TOO_LONG,
      medical_conditions: TOO_LONG,
    });

    expect(issues.find((issue) => issue.field === 'goals')?.message).toBe(
      `O texto não pode exceder ${MAX_TEXT_LENGTH} caracteres.`
    );
    expect(issues.find((issue) => issue.field === 'medical_conditions')?.message).toBe(
      `O texto não pode exceder ${MAX_TEXT_LENGTH} caracteres.`
    );
  });

  it('accepts texts exactly at the limit', () => {
    const issues = assessmentIssues({
      ...emptyAssessmentValues(),
      goals: 'a'.repeat(MAX_TEXT_LENGTH),
      medical_conditions: 'a'.repeat(MAX_TEXT_LENGTH),
    });

    expect(issues.some((issue) => issue.field === 'goals')).toBe(false);
    expect(issues.some((issue) => issue.field === 'medical_conditions')).toBe(false);
  });

  it('places the server codes on the right fields', () => {
    const issues = serverAssessmentIssues([
      { field: 'Goals', code: 'goals_too_long', message: 'x' },
      { field: 'MedicalConditions', code: 'medical_conditions_too_long', message: 'x' },
    ]);

    expect(issues.map((issue) => issue.field)).toEqual(['goals', 'medical_conditions']);
    expect(issues.every((issue) => issue.message.includes(String(MAX_TEXT_LENGTH)))).toBe(true);
  });
});
