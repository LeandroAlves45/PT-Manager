import { describe, expect, it } from 'vitest';

import {
  CONNECTION_ERROR_MESSAGE,
  authCodeMessage,
  authErrorMessage,
  authFieldMessage,
} from '@/features/auth/lib/auth-errors';
import { toApiProblem } from '@/shared/api/problem';

describe('authErrorMessage', () => {
  it('decides the 429 by status, because its title is "Too many requests" and not a code', () => {
    const problem = toApiProblem(429, { title: 'Too many requests', detail: 'x' });

    expect(authErrorMessage(problem)).toBe(
      'Demasiadas tentativas. Aguarda um momento e tenta novamente.'
    );
  });

  it('prefers the status over a known code on a 429', () => {
    const problem = toApiProblem(429, { title: 'authentication_invalid_credentials' });

    expect(authErrorMessage(problem)).toContain('Demasiadas tentativas');
  });

  it('never maps the rate_limit_exceeded code, which the backend does not emit', () => {
    expect(authCodeMessage('rate_limit_exceeded')).toBeUndefined();
  });

  it.each([
    ['authentication_invalid_credentials', 'Email ou palavra-passe inválidos.'],
    ['authentication_confirmation_token_expired', 'O link de confirmação expirou.'],
    ['authentication_invitation_consumed', 'Este convite já foi usado.'],
    ['google_account_not_found', 'Não há conta com este Google.'],
    ['google_identity_conflict', 'já está ligada a outro utilizador'],
    ['google_provider_unavailable', 'não está disponível agora'],
  ])('translates %s', (code, expected) => {
    expect(authErrorMessage(toApiProblem(401, { title: code }))).toContain(expected);
  });

  it('uses the code of the first field on a validation failure', () => {
    const problem = toApiProblem(400, {
      title: 'validation_failed',
      errors: [{ field: 'Password', code: 'authentication_password_too_short', message: 'x' }],
    });

    expect(authErrorMessage(problem)).toBe('A palavra-passe deve ter entre 8 e 128 caracteres.');
  });

  it('falls back to a generic message for an unknown code, never to the English detail', () => {
    const problem = toApiProblem(500, { title: 'something_new', detail: 'Internal failure.' });

    expect(authErrorMessage(problem)).toBe('Não foi possível concluir o pedido. Tenta novamente.');
  });

  it('treats a failure that is not an ApiProblem as a connection problem', () => {
    expect(authErrorMessage(new TypeError('Failed to fetch'))).toBe(CONNECTION_ERROR_MESSAGE);
  });

  it('does not resolve inherited object keys as codes', () => {
    expect(authCodeMessage('constructor')).toBeUndefined();
    expect(authCodeMessage('toString')).toBeUndefined();
  });
});

describe('authFieldMessage', () => {
  it('uses the mapped message and falls back to the server text for unknown codes', () => {
    expect(
      authFieldMessage({ field: 'Email', code: 'authentication_email_invalid', message: 'bad' })
    ).toBe('Introduz um email válido.');
    expect(
      authFieldMessage({ field: 'X', code: 'unknown_code', message: 'Texto do servidor.' })
    ).toBe('Texto do servidor.');
  });
});
