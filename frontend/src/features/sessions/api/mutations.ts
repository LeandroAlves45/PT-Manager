import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';

import { clientKeys } from '@/features/clients';
import { packKeys } from '@/features/packs';
import { sessionKeys } from '@/features/sessions/api/keys';
import type { SessionTransition } from '@/features/sessions/lib/sessionStatus';
import { trainerDashboardKeys } from '@/features/trainer-dashboard';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type CreateRequest = components['schemas']['CreateSessionRequest'];
type RescheduleRequest = components['schemas']['RescheduleSessionRequest'];

/**
 * Invalida tudo o que uma escrita numa sessão pode mudar: as listagens de sessões, o
 * painel ("sessões de hoje"), o detalhe do cliente (resumo e badge de packs) e os packs —
 * concluir, faltar e repor mexem no saldo.
 */
async function invalidateSessionData(queryClient: QueryClient, clientId: string) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: sessionKeys.all }),
    queryClient.invalidateQueries({ queryKey: trainerDashboardKeys.all }),
    queryClient.invalidateQueries({ queryKey: clientKeys.detail(clientId) }),
    queryClient.invalidateQueries({ queryKey: packKeys.clientPacks() }),
  ]);
}

/** Marca uma sessão nova (201). */
export function useCreateSessionMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: CreateRequest) =>
      unwrap(await apiClient.POST('/api/v1/sessions', { body })),
    onSuccess: (session) => invalidateSessionData(queryClient, session.client_id),
  });
}

/** Reagenda uma sessão agendada (início, duração e local). */
export function useRescheduleSessionMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ sessionId, body }: { sessionId: string; body: RescheduleRequest }) =>
      unwrap(
        await apiClient.PATCH('/api/v1/sessions/{sessionId}/reschedule', {
          params: { path: { sessionId } },
          body,
        })
      ),
    onSuccess: (session) => invalidateSessionData(queryClient, session.client_id),
  });
}

/** Associa a sessão a outro pack, ou a nenhum ('null'). */
export function useChangeSessionPackMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ sessionId, packId }: { sessionId: string; packId: string | null }) =>
      unwrap(
        await apiClient.PATCH('/api/v1/sessions/{sessionId}/pack', {
          params: { path: { sessionId } },
          body: { client_session_pack_id: packId },
        })
      ),
    onSuccess: (session) => invalidateSessionData(queryClient, session.client_id),
  });
}

/**
 * Muda o estado de uma sessão: concluir, falta, cancelar (por ti ou pelo cliente) e repor.
 *
 * O backend consome uma sessão do pack em `complete`/`no-show`, devolve-a em `restore` de
 * uma realizada ou falta, e recusa concluir/faltar antes da hora de início
 * (`session_transition_too_early`). É também a ação "Registar presença" do painel.
 */
export function useSessionTransitionMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      sessionId,
      transition,
    }: {
      sessionId: string;
      transition: SessionTransition;
    }) => {
      const params = { params: { path: { sessionId } } };
      switch (transition) {
        case 'complete':
          return unwrap(await apiClient.POST('/api/v1/sessions/{sessionId}/complete', params));
        case 'no-show':
          return unwrap(await apiClient.POST('/api/v1/sessions/{sessionId}/no-show', params));
        case 'cancel-by-trainer':
          return unwrap(
            await apiClient.POST('/api/v1/sessions/{sessionId}/cancel-by-trainer', params)
          );
        case 'cancel-by-client':
          return unwrap(
            await apiClient.POST('/api/v1/sessions/{sessionId}/cancel-by-client', params)
          );
        case 'restore':
          return unwrap(await apiClient.POST('/api/v1/sessions/{sessionId}/restore', params));
      }
    },
    onSuccess: (session) => invalidateSessionData(queryClient, session.client_id),
  });
}
