import { useMutation, useQueryClient } from '@tanstack/react-query';

import { catalogKeys } from '@/features/admin-catalog/api/catalog';
import type { ImportableCatalogKind } from '@/features/admin-catalog/lib/catalogImport';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

export type CatalogImportReport = components['schemas']['CatalogImportReportResponse'];

interface ImportRequest {
  readonly file: File;
  readonly dryRun: boolean;
}

/**
 * Envia o CSV em `multipart/form-data` (partes `file` e `dry_run`).
 *
 * O OpenAPI descreve o `IFormFile` como formulário serializado, que não corresponde ao pedido
 * real (igual ao logo e à fotografia): o `bodySerializer` envia o `FormData` tal como está.
 * Só a importação a sério invalida o catálogo; a verificação não grava nada.
 */
export function useCatalogImportMutation(kind: ImportableCatalogKind) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ file, dryRun }: ImportRequest): Promise<CatalogImportReport> => {
      const form = new FormData();
      form.append('file', file);
      form.append('dry_run', dryRun ? 'true' : 'false');
      const options = { body: {}, bodySerializer: () => form };

      return kind === 'foods'
        ? unwrap(await apiClient.POST('/api/v1/global-foods/import', options))
        : unwrap(await apiClient.POST('/api/v1/global-exercises/import', options));
    },
    onSuccess: async (report) => {
      if (report.dry_run) return;

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: catalogKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
      ]);
    },
  });
}
