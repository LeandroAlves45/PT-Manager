import { describe, expect, it, vi } from 'vitest';

import { applyProblemToForm } from '@/features/auth/lib/form-errors';
import { readEmail, readNotice } from '@/features/auth/lib/location-state';
import type { ApiProblem } from '@/shared/api/problem';

type Values = { full_name: string; email: string };

function problemWith(...fieldErrors: ApiProblem['fieldErrors']): ApiProblem {
  return { fieldErrors } as ApiProblem;
}

describe('applyProblemToForm', () => {
  const fields = { fullname: 'full_name', email: 'email' } as const;

  it('matches the server field name in any casing and maps the code to PT-PT', () => {
    const setError = vi.fn();

    const placed = applyProblemToForm<Values>(
      problemWith(
        { field: 'FullName', code: 'authentication_full_name_required', message: 'x' },
        { field: 'email', code: 'authentication_email_invalid', message: 'y' }
      ),
      setError,
      fields
    );

    expect(placed).toBe(true);
    expect(setError).toHaveBeenCalledWith('full_name', { message: 'Indica o teu nome completo.' });
    expect(setError).toHaveBeenCalledWith('email', { message: 'Introduz um email válido.' });
  });

  it('falls back to the server text for an unmapped code', () => {
    const setError = vi.fn();

    applyProblemToForm<Values>(
      problemWith({ field: 'Email', code: 'novo_codigo', message: 'Texto do servidor' }),
      setError,
      fields
    );

    expect(setError).toHaveBeenCalledWith('email', { message: 'Texto do servidor' });
  });

  it('returns false for a field the form does not own, so the caller shows a general error', () => {
    const setError = vi.fn();

    const placed = applyProblemToForm<Values>(
      problemWith({ field: 'Role', code: 'x', message: 'y' }),
      setError,
      fields
    );

    expect(placed).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });

  it('does not treat inherited object keys as form fields', () => {
    const setError = vi.fn();

    const placed = applyProblemToForm<Values>(
      problemWith({ field: 'constructor', code: 'x', message: 'y' }),
      setError,
      fields
    );

    expect(placed).toBe(false);
  });
});

describe('location state readers', () => {
  it.each([null, undefined, {}, { notice: 42 }, { notice: '' }])(
    'readNotice ignores %j',
    (state) => {
      expect(readNotice(state)).toBeNull();
    }
  );

  it('readNotice and readEmail return what a previous screen put there', () => {
    expect(readNotice({ notice: 'Palavra-passe alterada.' })).toBe('Palavra-passe alterada.');
    expect(readEmail({ email: 'ana@ptmanager.test' })).toBe('ana@ptmanager.test');
  });

  it.each([null, undefined, {}, { email: 7 }])('readEmail ignores %j', (state) => {
    expect(readEmail(state)).toBe('');
  });
});
