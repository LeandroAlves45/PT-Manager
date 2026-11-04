import { useMutation } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import { toApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { toSession, type Session } from '@/shared/api/session';
import { isNativeApp } from '@/shared/native/platform';
import { startNativeSession } from '@/shared/native/session';

/**
 * Pedidos de autenticação como mutações do TanStack Query.
 *
 * Tudo passa por `apiClient`; uma falha lança `ApiProblem` (via `unwrap`), por isso cada
 * ecrã decide a mensagem com `authErrorMessage`. Nenhuma mutação repete automaticamente:
 * repetir um login ou um pedido de email multiplicaria o efeito.
 *
 * Na app nativa as rotas que abrem sessão ou mexem na palavra-passe são as de
 * `/api/v1/native/auth`: devolvem o refresh token no corpo (guardado no armazenamento seguro)
 * e não exigem o cabeçalho Origin da web.
 */

type Schemas = components['schemas'];

/** Resultado do início de sessão com Google: sessão iniciada ou falta confirmar o email. */
export type GoogleSignInOutcome = { kind: 'session'; session: Session } | { kind: 'pending' };

export function useLoginMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['LoginRequest']): Promise<Session> =>
      isNativeApp()
        ? startNativeSession(unwrap(await apiClient.POST('/api/v1/native/auth/login', { body })))
        : toSession(unwrap(await apiClient.POST('/api/v1/auth/login', { body }))),
  });
}

/** `202` sempre, existia ou não a conta: o ecrã seguinte é igual nos dois casos. */
export function useSignUpMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['SignUpRequest']): Promise<void> => {
      unwrap(await apiClient.POST('/api/v1/auth/signup', { body }));
    },
  });
}

/** Pede um novo email de confirmação. `204` sempre. */
export function useRequestEmailConfirmationMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['EmailConfirmationRequest']): Promise<void> => {
      unwrap(await apiClient.POST('/api/v1/auth/email-confirmation/request', { body }));
    },
  });
}

/** Pede o email de recuperação. `204` sempre. */
export function useRequestPasswordResetMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['PasswordResetRequest']): Promise<void> => {
      unwrap(
        isNativeApp()
          ? await apiClient.POST('/api/v1/native/auth/password-reset/request', { body })
          : await apiClient.POST('/api/v1/auth/password-reset/request', { body })
      );
    },
  });
}

/** Define a nova palavra-passe com o token do email. O servidor revoga todas as sessões. */
export function useCompletePasswordResetMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['PasswordResetCompletionRequest']): Promise<void> => {
      unwrap(
        isNativeApp()
          ? await apiClient.POST('/api/v1/native/auth/password-reset/complete', { body })
          : await apiClient.POST('/api/v1/auth/password-reset/complete', { body })
      );
    },
  });
}

/** Confirma o email com o token do link. */
export async function confirmEmail(token: string): Promise<void> {
  unwrap(await apiClient.POST('/api/v1/auth/confirm-email', { body: { token } }));
}

/** Aceita o convite com a sessão de cliente já iniciada. */
export function useAcceptInviteMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['AcceptInvitationRequest']): Promise<Session> =>
      isNativeApp()
        ? startNativeSession(
            unwrap(await apiClient.POST('/api/v1/native/auth/accept-invite', { body }))
          )
        : toSession(unwrap(await apiClient.POST('/api/v1/auth/accept-invite', { body }))),
  });
}

/** Cria a conta do cliente convidado, já com sessão. */
export function useRegisterInvitedMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['InvitedClientRegistrationRequest']): Promise<Session> =>
      isNativeApp()
        ? startNativeSession(
            unwrap(await apiClient.POST('/api/v1/native/auth/accept-invite/register', { body }))
          )
        : toSession(unwrap(await apiClient.POST('/api/v1/auth/accept-invite/register', { body }))),
  });
}

/**
 * Entra (ou cria conta) com o id_token do Google.
 *
 * A resposta tem dois sucessos: `200` com a sessão e `202` quando falta confirmar o email,
 * por isso o ramo decide-se pelo estado HTTP e não pelo corpo.
 */
export function useGoogleSignInMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['GoogleSignInRequest']): Promise<GoogleSignInOutcome> => {
      const result = await apiClient.POST('/api/v1/auth/google/sign-in', { body });

      if (result.error !== undefined || !result.response.ok)
        throw toApiProblem(result.response.status, result.error);

      if (result.response.status === 202) return { kind: 'pending' };

      return { kind: 'session', session: toSession(result.data as Schemas['SessionResponse']) };
    },
  });
}
