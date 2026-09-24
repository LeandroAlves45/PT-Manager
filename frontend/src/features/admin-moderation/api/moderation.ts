import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';

export type ModerationKind = 'foods' | 'exercises';
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
      return kind === 'foods'
        ? unwrap(
            await apiClient.GET('/api/v1/admin/content-moderation/foods', {
              params: { query },
              signal,
            })
          )
        : unwrap(
            await apiClient.GET('/api/v1/admin/content-moderation/exercises', {
              params: { query },
              signal,
            })
          );
    },
  });
}

/** O backend valida a allowlist de motivos e grava auditoria na mesma transação. */
export function useModerationAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      kind,
      id,
      action,
      reason,
    }: {
      kind: ModerationKind;
      id: string;
      action: 'block' | 'unblock';
      reason?: string;
    }) => {
      if (kind === 'foods')
        return action === 'block'
          ? unwrap(
              await apiClient.POST('/api/v1/admin/content-moderation/foods/{foodId}/block', {
                params: { path: { foodId: id } },
                body: { reason_code: reason ?? '' },
              })
            )
          : unwrap(
              await apiClient.POST('/api/v1/admin/content-moderation/foods/{foodId}/unblock', {
                params: { path: { foodId: id } },
              })
            );

      return action === 'block'
        ? unwrap(
            await apiClient.POST('/api/v1/admin/content-moderation/exercises/{exerciseId}/block', {
              params: { path: { exerciseId: id } },
              body: { reason_code: reason ?? '' },
            })
          )
        : unwrap(
            await apiClient.POST(
              '/api/v1/admin/content-moderation/exercises/{exerciseId}/unblock',
              {
                params: { path: { exerciseId: id } },
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
