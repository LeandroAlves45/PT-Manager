import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type Schemas = components['schemas'];

/** Query keys da feature account. */
export const accountKeys = {
  all: ['account'] as const,
  detail: () => [...accountKeys.all, 'detail'] as const,
};

/** Email, papel e métodos de entrada (palavras-passe, Google) da conta autenticada. */
export function useAccountQuery() {
  return useQuery({
    queryKey: accountKeys.detail(),
    queryFn: async (): Promise<Schemas['AccountResponse']> =>
      unwrap(await apiClient.GET('/api/v1/auth/account')),
  });
}

/**
 * Muda a palavra-passe. `204`; o servidor revoga todas as sessões, por isso quem chama tem de
 * terminar a sessão local (`endLocalSession`) e não deve invalidar queries.
 */
export function useChangePasswordMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['ChangePasswordRequest']): Promise<void> => {
      unwrap(await apiClient.POST('/api/v1/auth/change-password', { body }));
    },
  });
}

/** Liga a conta Google (id_token do GIS) á conta local, confirmando a palavra-passe. */
export function useLinkGoogleMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: Schemas['GoogleLinkRequest']): Promise<void> => {
      unwrap(await apiClient.POST('/api/v1/auth/google/link', { body }));
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: accountKeys.all }),
  });
}
