import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type Schemas = components['schemas'];

export type AdminTrainer = Schemas['AdminTrainerResponse'];
export type PendingInvitation = Schemas['PendingTrainerInvitationResponse'];

/** Resultado de procurar um treinador pelo email completo. */
export type LookupOutcome =
  | { status: 'found'; trainer: AdminTrainer }
  | { status: 'invite_pending'; invitation: PendingInvitation }
  | { status: 'not_found' }
  | { status: 'not_trainer' };

/** Tamanho da página da lista de isentos. */
export const EXEMPT_PAGE_SIZE = 25;

export const trainerKeys = {
  all: ['admin-trainers'] as const,
  exempt: (page: number) => [...trainerKeys.all, 'exempt', page] as const,
  invitations: () => [...trainerKeys.all, 'invitations'] as const,
};

/** Normaliza a resposta do servidor (um único objeto com campos nulos) numa união fechada. */
function toOutcome(body: Schemas['TrainerLookupResponse']): LookupOutcome {
  if (body.status === 'found' && body.trainer !== null)
    return { status: 'found', trainer: body.trainer };
  if (body.status === 'invite_pending' && body.invitation !== null)
    return { status: 'invite_pending', invitation: body.invitation };
  if (body.status === 'not_trainer') return { status: 'not_trainer' };
  return { status: 'not_found' };
}

/**
 * Procura por email completo. É uma mutação e não uma query de propósito: só corre quando o
 * superuser submete, nunca enquanto escreve, e o resultado vive no ecrã que o pediu.
 */
export function useTrainerLookupMutation() {
  return useMutation({
    mutationFn: async (email: string): Promise<LookupOutcome> =>
      toOutcome(unwrap(await apiClient.POST('/api/v1/admin/trainers/lookup', { body: { email } }))),
  });
}

/** Treinadores isentos de pagamento, por página. */
export function useExemptTrainersQuery(page: number) {
  return useQuery({
    queryKey: trainerKeys.exempt(page),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/admin/trainers/exempt', {
          params: { query: { page_number: page, page_size: EXEMPT_PAGE_SIZE } },
          signal,
        })
      ),
  });
}

/** Convites ainda por usar (alguns já expirados). */
export function usePendingInvitationsQuery() {
  return useQuery({
    queryKey: trainerKeys.invitations(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/admin/trainer-invitations', { signal })),
  });
}

/** Atualiza listas e totais da visão geral depois de uma alteração confirmada. */
function useInvalidateTrainers() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: trainerKeys.all }),
      queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
    ]);
}

/** Isenta (`exempt: true`) ou tira a isenção de pagamento; devolve a ficha atualizada. */
export function useBillingExemptionMutation() {
  const invalidate = useInvalidateTrainers();
  return useMutation({
    mutationFn: async ({
      trainerId,
      exempt,
    }: {
      trainerId: string;
      exempt: boolean;
    }): Promise<AdminTrainer> => {
      const params = { params: { path: { trainerId } } };
      return exempt
        ? unwrap(
            await apiClient.POST('/api/v1/admin/trainers/{trainerId}/billing-exemption', params)
          )
        : unwrap(
            await apiClient.DELETE('/api/v1/admin/trainers/{trainerId}/billing-exemption', params)
          );
    },
    onSuccess: invalidate,
  });
}

/** Motivo e nota de um bloqueio de conta; a nota é `null` quando vazia. */
export interface AccessBlockInput {
  reasonCode: string;
  note: string | null;
}

/**
 * Bloqueia a conta de um treinador (`block` com motivo e nota) ou levanta o bloqueio
 * (`block: null`); devolve a ficha atualizada e invalida listas e visão geral.
 */
export function useAccessBlockMutation() {
  const invalidate = useInvalidateTrainers();
  return useMutation({
    mutationFn: async ({
      trainerId,
      block,
    }: {
      trainerId: string;
      block: AccessBlockInput | null;
    }): Promise<AdminTrainer> => {
      const path = { params: { path: { trainerId } } };
      return block !== null
        ? unwrap(
            await apiClient.POST('/api/v1/admin/trainers/{trainerId}/access-block', {
              ...path,
              body: { reason_code: block.reasonCode, note: block.note },
            })
          )
        : unwrap(await apiClient.DELETE('/api/v1/admin/trainers/{trainerId}/access-block', path));
    },
    onSuccess: invalidate,
  });
}

/** Envia (ou reenvia, revogando o link anterior) um convite de treinador. */
export function useSendTrainerInvitationMutation() {
  const invalidate = useInvalidateTrainers();
  return useMutation({
    mutationFn: async (email: string): Promise<PendingInvitation> =>
      unwrap(await apiClient.POST('/api/v1/admin/trainer-invitations', { body: { email } })),
    // onSettled: se o email falhar o convite já existe, e a lista tem de o mostrar.
    onSettled: invalidate,
  });
}

/** Anula um convite pendente. Idempotente no servidor. */
export function useRevokeTrainerInvitationMutation() {
  const invalidate = useInvalidateTrainers();
  return useMutation({
    mutationFn: async (invitationId: string): Promise<void> => {
      unwrap(
        await apiClient.DELETE('/api/v1/admin/trainer-invitations/{invitationId}', {
          params: { path: { invitationId } },
        })
      );
    },
    onSuccess: invalidate,
  });
}
