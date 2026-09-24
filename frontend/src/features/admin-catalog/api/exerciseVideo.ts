import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { catalogKeys } from '@/features/admin-catalog/api/catalog';
import { TERMINAL_VIDEO_STATUSES } from '@/features/admin-catalog/lib/exerciseVideo';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type UploadInstructions = components['schemas']['ExerciseVideoUploadInstructionsResponse'];

/** Intervalo e limite do acompanhamento: 100 × 3 s cobre o timeout de processamento do servidor. */
const STATUS_POLL_MS = 3000;
const MAX_STATUS_POLLS = 100;

/** Fora de `catalogKeys.all`: invalidar a lista não deve repetir o polling nem o URL assinado. */
const videoKeys = {
  upload: (exerciseId: string, videoId: string) =>
    ['admin-exercise-video', exerciseId, 'upload', videoId] as const,
  playback: (exerciseId: string) => ['admin-exercise-video', exerciseId, 'playback'] as const,
};

/** Falha do PUT direto ao storage; não é Problem Details porque não vem da API. */
export class StorageUploadError extends Error {
  readonly status: number;

  constructor(status: number) {
    super(`Storage upload failed with status ${status}`);
    this.name = 'StorageUploadError';
    this.status = status;
  }
}

/** Regista o upload pendente; a resposta traz o URL assinado e o Content-Type obrigatório. */
export async function requestVideoUpload(exerciseId: string, file: File) {
  return unwrap(
    await apiClient.POST('/api/v1/global-exercises/{exerciseId}/video/uploads', {
      params: { path: { exerciseId } },
      body: { content_type: file.type, size_bytes: file.size },
    })
  );
}

/**
 * Envia o ficheiro diretamente para o storage privado.
 *
 * Usa XHR e não `fetch` porque só o XHR expõe progresso de upload. O pedido não leva
 * `Authorization` nem CSRF: a autorização está no próprio URL assinado, e enviar o token
 * de acesso a um terceiro seria uma fuga. O `Content-Type` tem de ser exatamente o assinado.
 */
export function uploadToStorage(
  instructions: UploadInstructions,
  file: File,
  { onProgress, signal }: { onProgress: (percent: number) => void; signal: AbortSignal }
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(instructions.method, instructions.url);
    xhr.setRequestHeader('Content-Type', instructions.content_type);
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress(Math.round((event.loaded / event.total) * 100));
    };
    xhr.onload = () =>
      xhr.status >= 200 && xhr.status < 300
        ? resolve()
        : reject(new StorageUploadError(xhr.status));
    xhr.onerror = () => reject(new StorageUploadError(0));
    xhr.onabort = () => reject(new DOMException('Upload cancelado', 'AbortError'));
    if (signal.aborted) {
      xhr.abort();
      return;
    }
    signal.addEventListener('abort', () => xhr.abort(), { once: true });
    xhr.send(file);
  });
}

/** Confirma que o ficheiro chegou; o servidor valida tamanho e tipo e agenda o processamento. */
export async function completeVideoUpload(exerciseId: string, videoId: string) {
  return unwrap(
    await apiClient.POST('/api/v1/global-exercises/{exerciseId}/video/uploads/{videoId}/complete', {
      params: { path: { exerciseId, videoId } },
    })
  );
}

/**
 * Acompanha um upload até um estado terminal. Pára sozinho no terminal ou ao fim do limite;
 * ao chegar ao terminal, invalida a lista para a coluna do vídeo refletir o resultado.
 */
export function useVideoUploadStatus(exerciseId: string, videoId: string | null) {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: videoKeys.upload(exerciseId, videoId ?? ''),
    enabled: videoId !== null,
    queryFn: async ({ signal }) => {
      const video = unwrap(
        await apiClient.GET('/api/v1/global-exercises/{exerciseId}/video/uploads/{videoId}', {
          params: { path: { exerciseId, videoId: videoId ?? '' } },
          signal,
        })
      );
      if (TERMINAL_VIDEO_STATUSES.includes(video.status))
        await queryClient.invalidateQueries({ queryKey: catalogKeys.all });
      return video;
    },
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status !== undefined && TERMINAL_VIDEO_STATUSES.includes(status)) return false;
      return query.state.dataUpdateCount >= MAX_STATUS_POLLS ? false : STATUS_POLL_MS;
    },
  });
}

/** URL de reprodução assinado; só é pedido quando o admin carrega em "Ver vídeo". */
export function useVideoPlayback(exerciseId: string, enabled: boolean) {
  return useQuery({
    queryKey: videoKeys.playback(exerciseId),
    enabled,
    // O URL expira em 30 minutos; nunca reutilizar uma cópia com mais de 10.
    staleTime: 10 * 60 * 1000,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/global-exercises/{exerciseId}/video', {
          params: { path: { exerciseId } },
          signal,
        })
      ),
  });
}

/** Remove o vídeo pronto; o objeto no storage é apagado por um job do servidor. */
export function useRemoveVideo(exerciseId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () =>
      unwrap(
        await apiClient.DELETE('/api/v1/global-exercises/{exerciseId}/video', {
          params: { path: { exerciseId } },
        })
      ),
    onSuccess: async () => {
      queryClient.removeQueries({ queryKey: videoKeys.playback(exerciseId) });
      await queryClient.invalidateQueries({ queryKey: catalogKeys.all });
    },
  });
}
