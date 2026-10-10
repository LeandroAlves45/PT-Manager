import { useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';

import { TERMINAL_VIDEO_STATUSES } from '@/features/exercise-video/lib/exerciseVideo';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type UploadInstructions = components['schemas']['ExerciseVideoUploadInstructionsResponse'];

/**
 * Quem chama a API do vídeo, e por isso que família de rotas usa.
 *
 * - `superuser`: `/global-exercises/{id}/video/*` — só exercícios globais (catálogo do admin).
 * - `trainer`: `/exercises/{id}/video/*` — escreve nos exercícios privados do personal trainer e lê
 *   também o vídeo dos globais (o servidor serve a audiência do personal trainer nas duas).
 */
export type ExerciseVideoAudience = 'superuser' | 'trainer';

/**
 * Quem pode ver o vídeo pronto. Às audiências que gerem vídeos junta-se `client`
 * (`/portal/my-plan/exercises/{id}/video`, só exercícios do plano ativo), que nunca envia nem
 * remove: por isso só a leitura aceita este tipo, e as funções de escrita continuam fechadas
 * em `ExerciseVideoAudience`.
 */
export type ExerciseVideoReader = ExerciseVideoAudience | 'client';

/** Intervalo e limite do acompanhamento: 100 × 3 s cobre o timeout de processamento do servidor. */
const STATUS_POLL_MS = 3000;
const MAX_STATUS_POLLS = 100;

/**
 * Fora das query keys das listas: invalidar uma lista não deve repetir o polling nem o URL
 * assinado. A audiência entra na chave porque as duas rotas são recursos diferentes.
 */
const videoKeys = {
  upload: (audience: ExerciseVideoAudience, exerciseId: string, videoId: string) =>
    ['exercise-video', audience, exerciseId, 'upload', videoId] as const,
  playback: (audience: ExerciseVideoReader, exerciseId: string) =>
    ['exercise-video', audience, exerciseId, 'playback'] as const,
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
export async function requestVideoUpload(
  audience: ExerciseVideoAudience,
  exerciseId: string,
  file: File
) {
  const init = {
    params: { path: { exerciseId } },
    body: { content_type: file.type, size_bytes: file.size },
  };

  return unwrap(
    audience === 'superuser'
      ? await apiClient.POST('/api/v1/global-exercises/{exerciseId}/video/uploads', init)
      : await apiClient.POST('/api/v1/exercises/{exerciseId}/video/uploads', init)
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
    // Já cancelado: `abort()` entre `open()` e `send()` não dispara eventos e a Promise
    // ficaria pendurada.
    if (signal.aborted) {
      reject(new DOMException('Upload cancelado', 'AbortError'));
      return;
    }
    signal.addEventListener('abort', () => xhr.abort(), { once: true });
    xhr.send(file);
  });
}

/** Confirma que o ficheiro chegou; o servidor valida tamanho e tipo e agenda o processamento. */
export async function completeVideoUpload(
  audience: ExerciseVideoAudience,
  exerciseId: string,
  videoId: string
) {
  const init = { params: { path: { exerciseId, videoId } } };

  return unwrap(
    audience === 'superuser'
      ? await apiClient.POST(
          '/api/v1/global-exercises/{exerciseId}/video/uploads/{videoId}/complete',
          init
        )
      : await apiClient.POST(
          '/api/v1/exercises/{exerciseId}/video/uploads/{videoId}/complete',
          init
        )
  );
}

/**
 * Acompanha um upload até um estado terminal. Pára sozinho no terminal ou ao fim do limite;
 * ao chegar ao terminal, esquece o URL de reprodução em cache e invalida `listKey` para a
 * lista refletir o resultado.
 */
export function useVideoUploadStatus(
  audience: ExerciseVideoAudience,
  exerciseId: string,
  videoId: string | null,
  listKey: QueryKey
) {
  const queryClient = useQueryClient();

  return useQuery({
    queryKey: videoKeys.upload(audience, exerciseId, videoId ?? ''),
    enabled: videoId !== null,
    queryFn: async ({ signal }) => {
      const init = { params: { path: { exerciseId, videoId: videoId ?? '' } }, signal };

      const video = unwrap(
        audience === 'superuser'
          ? await apiClient.GET(
              '/api/v1/global-exercises/{exerciseId}/video/uploads/{videoId}',
              init
            )
          : await apiClient.GET('/api/v1/exercises/{exerciseId}/video/uploads/{videoId}', init)
      );

      if (TERMINAL_VIDEO_STATUSES.includes(video.status)) {
        // Uma substituição pronta muda o vídeo: o URL assinado em cache ainda é o anterior.
        queryClient.removeQueries({ queryKey: videoKeys.playback(audience, exerciseId) });
        await queryClient.invalidateQueries({ queryKey: listKey });
      }
      return video;
    },
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status !== undefined && TERMINAL_VIDEO_STATUSES.includes(status)) return false;

      return query.state.dataUpdateCount >= MAX_STATUS_POLLS ? false : STATUS_POLL_MS;
    },
  });
}

/** URL de reprodução assinado; só é pedido quando o utilizador carrega em "Ver vídeo". */
export function useVideoPlayback(
  audience: ExerciseVideoReader,
  exerciseId: string,
  enabled: boolean
) {
  return useQuery({
    queryKey: videoKeys.playback(audience, exerciseId),
    enabled,
    // O URL expira em 30 minutos; nunca reutilizar uma cópia com mais de 10.
    staleTime: 10 * 60 * 1000,
    queryFn: async ({ signal }) => {
      const init = { params: { path: { exerciseId } }, signal };
      if (audience === 'client')
        return unwrap(
          await apiClient.GET('/api/v1/portal/my-plan/exercises/{exerciseId}/video', init)
        );

      return unwrap(
        audience === 'superuser'
          ? await apiClient.GET('/api/v1/global-exercises/{exerciseId}/video', init)
          : await apiClient.GET('/api/v1/exercises/{exerciseId}/video', init)
      );
    },
  });
}

/** Remove o vídeo pronto; o objeto no storage é apagado por um job do servidor. */
export function useRemoveVideo(
  audience: ExerciseVideoAudience,
  exerciseId: string,
  listKey: QueryKey
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      const init = { params: { path: { exerciseId } } };
      return unwrap(
        audience === 'superuser'
          ? await apiClient.DELETE('/api/v1/global-exercises/{exerciseId}/video', init)
          : await apiClient.DELETE('/api/v1/exercises/{exerciseId}/video', init)
      );
    },
    onSuccess: async () => {
      queryClient.removeQueries({ queryKey: videoKeys.playback(audience, exerciseId) });
      await queryClient.invalidateQueries({ queryKey: listKey });
    },
  });
}
