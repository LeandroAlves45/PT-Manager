import type { QueryKey } from '@tanstack/react-query';
import { useEffect, useId, useRef, useState } from 'react';
import { toast } from 'sonner';

import {
  completeVideoUpload,
  requestVideoUpload,
  StorageUploadError,
  uploadToStorage,
  useRemoveVideo,
  useVideoPlayback,
  useVideoUploadStatus,
  type ExerciseVideoAudience,
} from '@/features/exercise-video/api/exerciseVideo';
import {
  ACCEPTED_VIDEO_TYPES,
  describeExerciseVideo,
  readVideoMetadata,
  TERMINAL_VIDEO_STATUSES,
  validateVideoFile,
  validateVideoMetadata,
  videoErrorMessage,
  type ExerciseVideoSubject,
} from '@/features/exercise-video/lib/exerciseVideo';
import { isApiProblem } from '@/shared/api/problem';
import { Button } from '@/shared/components/ui/button';

type Phase =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'uploading'; progress: number }
  | { kind: 'completing' }
  | { kind: 'tracking'; videoId: string }
  | { kind: 'error'; message: string };

function uploadErrorMessage(error: unknown): string {
  if (error instanceof DOMException && error.name === 'AbortError')
    return 'Envio cancelado.';
  if (error instanceof StorageUploadError)
    return 'O envio para o armazenamento falhou. Tenta novamente.';
  if (isApiProblem(error) && error.status === 429)
    return 'Demasiados envios seguidos. Aguarda um pouco e tenta novamente.';

  return videoErrorMessage(isApiProblem(error) ? error.code : null);
}

/**
 * Vídeo gerido de um exercício: estado, envio com progresso, reprodução e remoção.
 *
 * O ficheiro nunca passa pela API: a API emite um URL assinado, o browser envia para o
 * storage e a API confirma e processa em segundo plano. Fechar o painel cancela o envio.
 *
 * @param audience Família de rotas (`superuser` no catálogo global, `trainer` na biblioteca).
 * @param listKey Query key da lista a invalidar quando o vídeo muda de estado ou é removido.
 * @param mode `manage` envia, substitui e remove; `view` só reproduz (exercício global visto
 *   pelo personal trainer, arquivado ou bloqueado — o servidor recusaria o envio).
 */
export function ExerciseVideoPanel({
  exercise,
  audience,
  listKey,
  mode = 'manage',
}: {
  exercise: ExerciseVideoSubject;
  audience: ExerciseVideoAudience;
  listKey: QueryKey;
  mode?: 'manage' | 'view';
}) {
  const headingId = useId();
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const [showPlayer, setShowPlayer] = useState(false);
  const [confirmRemove, setConfirmRemove] = useState(false);
  const [removed, setRemoved] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  const tracking = useVideoUploadStatus(
    audience,
    exercise.id,
    phase.kind === 'tracking' ? phase.videoId : null,
    listKey
  );
  const trackedVideo = phase.kind === 'tracking' ? tracking.data : undefined;
  const trackedStatus = trackedVideo?.status;
  const hasReadyVideo = trackedStatus === 'ready' || (!removed && exercise.has_ready_video);
  const playback = useVideoPlayback(audience, exercise.id, showPlayer && hasReadyVideo);
  const remove = useRemoveVideo(audience, exercise.id, listKey);

  useEffect(() => () => abortRef.current?.abort(), []);

  const processing =
    phase.kind === 'tracking' &&
    (trackedStatus === undefined || !TERMINAL_VIDEO_STATUSES.includes(trackedStatus));
  const busy =
    phase.kind === 'checking' ||
    phase.kind === 'uploading' ||
    phase.kind === 'completing' ||
    processing;
  // Em modo de leitura não interessa o estado de uma substituição em curso de outra pessoa.
  const summary =
    mode === 'view'
      ? hasReadyVideo
        ? 'Vídeo disponível'
        : 'Sem vídeo'
      : describeExerciseVideo({
        managed_video_status: trackedStatus ?? (removed ? null : exercise.managed_video_status),
        has_ready_video: hasReadyVideo,
        video_url: exercise.video_url,
      });

  async function upload(file: File) {
    setConfirmRemove(false);
    const fileError = validateVideoFile(file);
    if (fileError !== null) {
      setPhase({ kind: 'error', message: fileError });
      return;
    }

    setPhase({ kind: 'checking' });
    const metadata = await readVideoMetadata(file);
    const metadataError = metadata === null ? null : validateVideoMetadata(metadata);
    if (metadataError !== null) {
      setPhase({ kind: 'error', message: metadataError });
      return;
    }

    const controller = new AbortController();
    abortRef.current = controller;
    try {
      const created = await requestVideoUpload(audience, exercise.id, file);
      setPhase({ kind: 'uploading', progress: 0 });
      await uploadToStorage(created.upload, file, {
        signal: controller.signal,
        onProgress: (progress) => setPhase({ kind: 'uploading', progress }),
      });
      setPhase({ kind: 'completing' });
      await completeVideoUpload(audience, exercise.id, created.video.id);
      setShowPlayer(false);
      setPhase({ kind: 'tracking', videoId: created.video.id });
    } catch (error) {
      setPhase({ kind: 'error', message: uploadErrorMessage(error) });
    } finally {
      abortRef.current = null;
    }
  }

  async function confirmRemoval() {
    try {
      await remove.mutateAsync();
      setRemoved(true);
      setShowPlayer(false);
      setConfirmRemove(false);
      setPhase({ kind: 'idle' });
      toast.success('Vídeo removido com sucesso.');
    } catch (error) {
      setConfirmRemove(false);
      setPhase({
        kind: 'error',
        message: videoErrorMessage(isApiProblem(error) ? error.code : null),
      });
    }
  }

  return (
    <section aria-labelledby={headingId} className="border-border space-y-3 rounded-lg border p-3">
      <h3 id={headingId} className="text-sm font-medium">
        Vídeo
      </h3>
      <p className="text-muted-foreground text-sm">{summary}</p>

      {phase.kind === 'checking' && <p className="text-sm">A verificar o ficheiro…</p>}
      {phase.kind === 'uploading' && (
        <div className="flex items-center gap-3">
          <progress
            aria-label="Progresso do envio"
            max={100}
            value={phase.progress}
            className="h-2 flex-1"
          />
          <span className="text-sm tabular-nums">{phase.progress}%</span>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => abortRef.current?.abort()}
          >
            Cancelar envio
          </Button>
        </div>
      )}
      {phase.kind === 'completing' && <p className="text-sm">A confirmar o envio…</p>}
      {processing && (
        <p role="status" className="text-sm">
          {tracking.isError
            ? 'Não foi possível consultar o estado. O processamento continua no servidor.'
            : 'A processar o vídeo. Podes fechar este painel.'}
        </p>
      )}
      {trackedStatus === 'ready' && (
        <p role="status" className="text-sm">
          Vídeo pronto.
        </p>
      )}
      {(trackedStatus === 'failed' || trackedStatus === 'rejected') && (
        <p role="alert" className="text-destructive text-sm">
          {videoErrorMessage(trackedVideo?.failure_code)}
        </p>
      )}
      {phase.kind === 'error' && (
        <p role="alert" className="text-destructive text-sm">
          {phase.message}
        </p>
      )}

      {showPlayer && hasReadyVideo && (
        <>
          {playback.isPending && <p className="text-sm">A carregar o vídeo…</p>}
          {playback.isError && (
            <p role="alert" className="text-destructive text-sm">
              Não foi possível carregar o vídeo.
            </p>
          )}
          {playback.data && (
            <video
              controls
              preload="metadata"
              src={playback.data.playback_url}
              aria-label={`Vídeo de ${exercise.name}`}
              className="w-full rounded-md"
            />
          )}
        </>
      )}

      {mode === 'view' ? (
        hasReadyVideo &&
        !showPlayer && (
          <Button type="button" size="sm" variant="outline" onClick={() => setShowPlayer(true)}>
            Ver vídeo
          </Button>
        )
      ) : confirmRemove ? (
        <div className="space-y-2">
          <p className="text-sm">
            O vídeo deixa de estar disponível. Esta ação não pode ser desfeita.
          </p>
          <div className="flex gap-2">
            <Button
              type="button"
              size="sm"
              variant="destructive"
              disabled={remove.isPending}
              onClick={() => void confirmRemoval()}
            >
              {remove.isPending ? 'A remover…' : 'Confirmar remoção'}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => setConfirmRemove(false)}
            >
              Cancelar
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <label className="border-border hover:bg-accent has-focus-visible:outline-ring inline-flex min-h-11 cursor-pointer items-center rounded-md border px-3 text-sm has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-disabled:cursor-not-allowed has-disabled:opacity-50 md:min-h-8">
            {hasReadyVideo ? 'Substituir vídeo' : 'Enviar vídeo'}
            <input
              type="file"
              accept={ACCEPTED_VIDEO_TYPES.join(',')}
              className="sr-only"
              disabled={busy}
              onChange={(event) => {
                const file = event.target.files?.[0];
                event.target.value = '';
                if (file) void upload(file);
              }}
            />
          </label>
          {hasReadyVideo && !busy && (
            <>
              {!showPlayer && (
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={() => setShowPlayer(true)}
                >
                  Ver vídeo
                </Button>
              )}
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setConfirmRemove(true)}
              >
                Remover vídeo
              </Button>
            </>
          )}
        </div>
      )}
      {mode === 'manage' && (
        <p className="text-muted-foreground text-xs">MP4 ou MOV, H.264, até 100 MB e 3 minutos.</p>
      )}
    </section>
  );
}
