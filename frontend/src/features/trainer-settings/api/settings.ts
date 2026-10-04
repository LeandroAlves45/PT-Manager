import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type Settings = components['schemas']['TrainerSettingsResponse'];
type BrandingRequest = components['schemas']['UpdateBrandingRequest'];
type ContactsRequest = components['schemas']['UpdateContactsRequest'];

/** Query keys das definições do personal trainer (um único recurso por tenant). */
export const trainerSettingsKeys = {
  all: ['trainer-settings'] as const,
};

/** Definições do personal trainer autenticado (`GET /trainer-settings`). */
export function useTrainerSettingsQuery() {
  return useQuery({
    queryKey: trainerSettingsKeys.all,
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/trainer-settings', { signal })),
  });
}

/**
 * Todas as escritas devolvem as definições completas: a resposta substitui a cache, sem
 * um `GET` a seguir.
 */
function useSettingsMutation<TVariables>(
  write: (variables: TVariables) => Promise<Settings>,
  afterSave?: (queryClient: QueryClient) => Promise<void>
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: write,
    onSuccess: async (settings) => {
      queryClient.setQueryData(trainerSettingsKeys.all, settings);
      await afterSave?.(queryClient);
    },
  });
}

/** Nome da app e cores ('null' numa cor repõe o tema). */
export function useUpdateBrandingMutation() {
  return useSettingsMutation(async (body: BrandingRequest) =>
    unwrap(await apiClient.PATCH('/api/v1/trainer-settings/branding', { body }))
  );
}

/** Repõe as duas cores do tema padrão. */
export function useResetBrandingColorsMutation() {
  return useSettingsMutation(async () =>
    unwrap(await apiClient.POST('/api/v1/trainer-settings/branding/reset-colors'))
  );
}

/**
 * Substitui o logo por `multipart/form-data` com a parte `file`.
 *
 * O OpenAPI descreve este corpo como um `IFormFile` serializado em formulário (efeito do
 * `[FromForm]` no ASP.NET Core), que não corresponde ao pedido real. O `bodySerializer`
 * envia o `FormData` tal como está; o `openapi-fetch` não acrescenta `Content-Type` a um
 * `FormData` e o browser escreve o `boundary`.
 */
export function useReplaceLogoMutation() {
  return useSettingsMutation(async (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return unwrap(
      await apiClient.PUT('/api/v1/trainer-settings/logo', {
        body: {},
        bodySerializer: () => form,
      })
    );
  });
}

/** Remove o logo atual. */
export function useRemoveLogoMutation() {
  return useSettingsMutation(async () =>
    unwrap(await apiClient.DELETE('/api/v1/trainer-settings/logo'))
  );
}

/** Telefone, morada e cidade (todos opcionais; 'null' apaga). */
export function useUpdateContactsMutation() {
  return useSettingsMutation(async (body: ContactsRequest) =>
    unwrap(await apiClient.PATCH('/api/v1/trainer-settings/contacts', { body }))
  );
}

/**
 * Muda o fuso horário IANA.
 *
 * O fuso decide o "dia local" de quase tudo no servidor: hoje do painel, estado dos
 * check-ins, regra de uma sessão por cliente por dia. Depois de mudar, invalida todas as
 * outras queries em vez de adivinhar quais dependem dele.
 */
export function useChangeTimezoneMutation() {
  return useSettingsMutation(
    async (timezone: string) =>
      unwrap(await apiClient.PATCH('/api/v1/trainer-settings/timezone', { body: { timezone } })),
    (queryClient) =>
      queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] !== trainerSettingsKeys.all[0],
      })
  );
}
