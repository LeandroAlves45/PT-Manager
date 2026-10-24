import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';

export type ModerationKind = 'foods' | 'exercises' | 'supplements';
export type ModerationStatus = 'all' | 'allowed' | 'blocked';

export const moderationKeys = {
  all: ['admin-moderation'] as const,
  list: (kind: ModerationKind, status: ModerationStatus, search: string, page: number) =>
    [...moderationKeys.all, kind, status, search, page] as const,
};

/** Uma listagem por filtro, com AbortSignal e dados da página anterior durante a troca. */
export function useModerationQuery(
  kind: ModerationKind,
  status: ModerationStatus,
  search: string,
  page: number
) {
  return useQuery({
    queryKey: moderationKeys.list(kind, status, search, page),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) => {
      const query = { status, search: search || undefined, page_number: page, page_size: 25 };
      if (kind === 'foods')
        return unwrap(
          await apiClient.GET('/api/v1/admin/content-moderation/foods', {
            params: { query },
            signal,
          })
        );
      if (kind === 'exercises')
        return unwrap(
          await apiClient.GET('/api/v1/admin/content-moderation/exercises', {
            params: { query },
            signal,
          })
        );
      return unwrap(
        await apiClient.GET('/api/v1/admin/content-moderation/supplements', {
          params: { query },
          signal,
        })
      );
    },
  });
}

/**
 * O backend valida a allowlist de motivos e grava auditoria na mesma transação. `note` é a nota
 * livre opcional do bloqueio (`null` quando vazia); o desbloqueio não envia corpo.
 */
export function useModerationAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      kind,
      id,
      action,
      reason,
      note = null,
    }: {
      kind: ModerationKind;
      id: string;
      action: 'block' | 'unblock';
      reason?: string;
      note?: string | null;
    }) => {
      const body = { reason_code: reason ?? '', note };
      if (kind === 'foods')
        return action === 'block'
          ? unwrap(
              await apiClient.POST('/api/v1/admin/content-moderation/foods/{foodId}/block', {
                params: { path: { foodId: id } },
                body,
              })
            )
          : unwrap(
              await apiClient.POST('/api/v1/admin/content-moderation/foods/{foodId}/unblock', {
                params: { path: { foodId: id } },
              })
            );

      if (kind === 'exercises')
        return action === 'block'
          ? unwrap(
              await apiClient.POST(
                '/api/v1/admin/content-moderation/exercises/{exerciseId}/block',
                {
                  params: { path: { exerciseId: id } },
                  body,
                }
              )
            )
          : unwrap(
              await apiClient.POST(
                '/api/v1/admin/content-moderation/exercises/{exerciseId}/unblock',
                {
                  params: { path: { exerciseId: id } },
                }
              )
            );

      return action === 'block'
        ? unwrap(
            await apiClient.POST(
              '/api/v1/admin/content-moderation/supplements/{supplementId}/block',
              {
                params: { path: { supplementId: id } },
                body,
              }
            )
          )
        : unwrap(
            await apiClient.POST(
              '/api/v1/admin/content-moderation/supplements/{supplementId}/unblock',
              {
                params: { path: { supplementId: id } },
              }
            )
          );
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: moderationKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
      ]);
    },
  });
}
