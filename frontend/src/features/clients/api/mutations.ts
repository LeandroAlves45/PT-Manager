import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';

import { clientKeys } from '@/features/clients/api/keys';
import { trainerDashboardKeys } from '@/features/trainer-dashboard';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type ClientRequest = components['schemas']['CreateClientRequest'];
type AssessmentRequest = components['schemas']['UpdateInitialAssessmentRequest'];

/**
 * Invalida tudo o que uma escrita de clientes pode mudar.
 *
 * Listas (incluindo a pesquisa da paleta), a ficha tocada com o resumo e a avaliação, o
 * painel (clientes ativos, sem plano) e o cartão de subscrição da sidebar
 * (`current_client_count`).
 */
async function invalidateClientData(queryClient: QueryClient, clientId?: string): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: clientKeys.lists() }),
    clientId === undefined
      ? Promise.resolve()
      : queryClient.invalidateQueries({ queryKey: clientKeys.detail(clientId) }),
    queryClient.invalidateQueries({ queryKey: trainerDashboardKeys.all }),
    queryClient.invalidateQueries({ queryKey: ['billing', 'subscription'] }),
  ]);
}

/**
 * Cria (`clientId === null`) ou atualiza a ficha do cliente.
 *
 * O PATCH do backend substitui o perfil editável inteiro, por isso o corpo é o mesmo nos
 * dois casos.
 */
export function useSaveClientMutation(clientId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: ClientRequest) =>
      clientId === null
        ? unwrap(await apiClient.POST('/api/v1/clients', { body }))
        : unwrap(
            await apiClient.PATCH('/api/v1/clients/{clientId}', {
              params: { path: { clientId } },
              body,
            })
          ),
    onSuccess: (client) => invalidateClientData(queryClient, client.id),
  });
}

/** Arquiva ou reativa um cliente (204 sem corpo). */
export function useClientActivityMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      clientId,
      action,
    }: {
      clientId: string;
      action: 'archive' | 'reactivate';
    }) => {
      const params = { params: { path: { clientId } } };
      unwrap(
        action === 'archive'
          ? await apiClient.POST('/api/v1/clients/{clientId}/archive', params)
          : await apiClient.POST('/api/v1/clients/{clientId}/reactivate', params)
      );
    },
    onSuccess: (_, { clientId }) => invalidateClientData(queryClient, clientId),
  });
}

/**
 * Envia o convite de acesso ao portal.
 *
 * O backend só aceita o email de contacto da ficha (`authentication_invitation_email_mismatch`
 * caso contrário), por isso quem chama envia sempre `contact_email` — nunca um campo livre.
 */
export function useInviteClientMutation() {
  return useMutation({
    mutationFn: async ({ clientId, email }: { clientId: string; email: string }) => {
      unwrap(
        await apiClient.POST('/api/v1/auth/invite-client', {
          body: { client_id: clientId, email },
        })
      );
    },
  });
}

/**
 * Cria (`assessmentId === null`) ou substitui a avaliação inicial.
 *
 * Invalida o detalhe do cliente: a altura e, sem check-ins, o peso do resumo vêm daqui.
 */
export function useSaveInitialAssessmentMutation(clientId: string, assessmentId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: AssessmentRequest) =>
      assessmentId === null
        ? unwrap(
            await apiClient.POST('/api/v1/initial-assessments', {
              body: { ...body, client_id: clientId },
            })
          )
        : unwrap(
            await apiClient.PUT('/api/v1/initial-assessments/{assessmentId}', {
              params: { path: { assessmentId } },
              body,
            })
          ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clientKeys.detail(clientId) }),
  });
}
