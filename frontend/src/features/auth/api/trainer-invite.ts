import { useMutation, useQuery } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

/**
 * Pedidos anónimos do convite de treinador (criado pelo superuser).
 *
 * O token viaja sempre no corpo do POST, nunca no URL do pedido; não se regista em lado nenhum.
 */

type Schemas = components['schemas'];

/** Estados do convite devolvidos pelo servidor. */
export type TrainerInviteState = 'valid' | 'expired' | 'used' | 'invalid';

const KNOWN_STATES: readonly string[] = ['valid', 'expired', 'used', 'invalid'];

/** Estado do convite e, só quando é válido, o email a que se destina. */
export function useTrainerInviteStatus(token: string) {
  return useQuery({
    queryKey: ['trainer-invite-status', token],
    // O estado não muda enquanto o ecrã está aberto, e uma nova leitura não ajuda.
    staleTime: Infinity,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    queryFn: async ({ signal }): Promise<{ status: TrainerInviteState; email: string | null }> => {
      const body = unwrap(
        await apiClient.POST('/api/v1/auth/trainer-invite/status', { body: { token }, signal })
      );
      return {
        status: KNOWN_STATES.includes(body.status)
          ? (body.status as TrainerInviteState)
          : 'invalid',
        email: body.email,
      };
    },
  });
}

/** Cria a conta do treinador convidado. `204`: a conta fica criada, sem sessão. */
export function useRegisterInvitedTrainerMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['InvitedTrainerRegistrationRequest']): Promise<void> => {
      unwrap(await apiClient.POST('/api/v1/auth/trainer-invite/register', { body }));
    },
  });
}
