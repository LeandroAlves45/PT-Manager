import { describe, expect, it } from 'vitest';

import { isApiProblem, toApiProblem } from '@/shared/api/problem';
import { problem } from '@/test/msw/handlers';

describe('toApiProblem', () => {
  it('reads the stable code from the Problem Details title', () => {
    const error = toApiProblem(409, problem('client_email_already_exists'));

    expect(isApiProblem(error)).toBe(true);
    expect(error.status).toBe(409);
    expect(error.code).toBe('client_email_already_exists');
    expect(error.message).toBe('detail for client_email_already_exists');
  });

  it('keeps the correlation id and the per-field errors', () => {
    const error = toApiProblem(
      400,
      problem('validation_failed', {
        errors: [{ field: 'email', code: 'email_invalid', message: 'Email inválido.' }],
      })
    );

    expect(error.correlationId).toBe('corr-123');
    expect(error.hasFieldErrors).toBe(true);
    expect(error.fieldErrors).toEqual([
      { field: 'email', code: 'email_invalid', message: 'Email inválido.' },
    ]);
  });

  it.each([undefined, null, 'Bad gateway', { unexpected: true }])(
    'falls back safely when the body is not Problem Details (%s)',
    (body) => {
      const error = toApiProblem(502, body);

      expect(error.code).toBe('http_502');
      expect(error.correlationId).toBeNull();
      expect(error.hasFieldErrors).toBe(false);
      expect(error.message).toBe('Ocorreu um erro inesperado.');
    }
  );

  it('does not treat plain errors as API problems', () => {
    expect(isApiProblem(new Error('boom'))).toBe(false);
  });
});
