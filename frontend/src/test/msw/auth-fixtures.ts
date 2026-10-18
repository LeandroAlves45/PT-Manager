import type { components } from '@/shared/api/schema';

/**
 * Respostas da API para os testes da 6GA (autenticação e conta), tipadas pelo `schema.d.ts`.
 */

type Schemas = components['schemas'];

/** Conta local com palavra-passe, sem Google ligado. */
export function accountResponse(
  overrides: Partial<Schemas['AccountResponse']> = {}
): Schemas['AccountResponse'] {
  return {
    email: 'ana@ptmanager.test',
    role: 'trainer',
    has_password: true,
    google_linked: false,
    ...overrides,
  };
}

/** Desafio do Google; o `nonce` identifica a tentativa nos testes. */
export function googleChallenge(nonce = 'nonce-1'): Schemas['GoogleChallengeResponse'] {
  return { nonce, expires_at: '2099-01-01T00:00:00Z' };
}

/** Corpo `202` do signup. */
export const SIGN_UP_ACCEPTED: Schemas['SignUpAcceptedResponse'] = {
  status: 'confirmation_email_sent',
};

/** Corpo `202` do Google quando falta confirmar o email. */
export const GOOGLE_PENDING: Schemas['GooglePendingResponse'] = {
  status: 'email_confirmation_required',
};
