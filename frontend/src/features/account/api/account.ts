import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type Schemas = components['schemas'];

/** Query keys da feature account. */
export const accountKeys = {
  all: ['account'] as const,
  detail: () => [...accountKeys.all, 'detail'] as const,
};

/**
 * Email, papel, nome e métodos de entrada (palavras-passe, Google) da conta autenticada.
 *
 * @param enabled Falso para não pedir: o menu de perfil do cliente usa o perfil do portal.
 */
export function useAccountQuery(enabled = true) {
  return useQuery({
    queryKey: accountKeys.detail(),
    enabled,
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

/** Liga a conta Google (id_token do GIS) à conta local, confirmando a palavra-passe. */
export function useLinkGoogleMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: Schemas['GoogleLinkRequest']): Promise<void> => {
      unwrap(await apiClient.POST('/api/v1/auth/google/link', { body }));
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: accountKeys.all }),
  });
}

/**
 * Elimina a conta do cliente autenticado. `204`; o servidor revoga todas as sessões e apaga
 * os dados que identificam o cliente, por isso quem chama termina a sessão local
 * (`endLocalSession`) em vez de invalidar queries.
 */
export function useDeleteMyAccountMutation() {
  return useMutation({
    mutationFn: async (body: Schemas['DeleteMyAccountRequest']): Promise<void> => {
      unwrap(await apiClient.POST('/api/v1/portal/my-account/deletion', { body }));
    },
  });
}
