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

  it('reads the block reason and note extensions of a blocked account', () => {
    const error = toApiProblem(
      403,
      problem('trainer_account_blocked', { block_reason: 'malicious_content', block_note: 'Nota.' })
    );

    expect(error.blockReason).toBe('malicious_content');
    expect(error.blockNote).toBe('Nota.');
  });

  it('has no block reason or note when the extensions are missing or not text', () => {
    const plain = toApiProblem(403, problem('forbidden'));
    const odd = toApiProblem(
      403,
      problem('trainer_account_blocked', { block_reason: 7, block_note: null })
    );

    expect(plain.blockReason).toBeNull();
    expect(plain.blockNote).toBeNull();
    expect(odd.blockReason).toBeNull();
    expect(odd.blockNote).toBeNull();
  });

  it('does not treat plain errors as API problems', () => {
    expect(isApiProblem(new Error('boom'))).toBe(false);
  });
});
