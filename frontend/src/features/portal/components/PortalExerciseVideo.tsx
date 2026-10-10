import { PlayCircle } from 'lucide-react';
import { useState } from 'react';

import { useVideoPlayback } from '@/features/exercise-video';
import { isApiProblem } from '@/shared/api/problem';
import { Button } from '@/shared/components/ui/button';

/** Mensagem da falha do URL assinado, na linguagem do cliente. */
function playbackErrorMessage(error: unknown): string {
  if (isApiProblem(error) && error.code === 'exercise_video_not_found')
    return 'O vídeo deixou de estar disponível.';
  if (isApiProblem(error) && error.code === 'exercise_video_storage_unavailable')
    return 'O vídeo não está disponível de momento. Tenta novamente mais tarde.';

  return 'Não foi possível carregar o vídeo.';
}

/**
 * Vídeo do exercício no portal: "Ver vídeo" pede o URL assinado e mostra o
 * leitor. Só existe quando a API diz `has_ready_video`, por isso nunca promete um 404.
 *
 * O URL só é pedido no clique (vale 30 min e fica em cache 10 min em `useVideoPlayback`).
 *
 * @param exerciseId `exercise_id` do catálogo, não o `id` da prescrição.
 * @param exerciseName Nome do exercício, para o rótulo acessível do leitor.
 */
export function PortalExerciseVideo({
  exerciseId,
  exerciseName,
}: {
  exerciseId: string;
  exerciseName: string;
}) {
  const [open, setOpen] = useState(false);
  const playback = useVideoPlayback('client', exerciseId, open);

  if (!open)
    return (
      <Button
        type="button"
        variant="outline"
        className="min-h-11"
        aria-label={`Ver vídeo de ${exerciseName}`}
        onClick={() => setOpen(true)}
      >
        <PlayCircle aria-hidden />
        Ver vídeo
      </Button>
    );

  if (playback.isPending) return <p className="text-sm">A carregar vídeo…</p>;

  if (playback.isError)
    return (
      <p role="alert" className="text-destructive text-sm">
        {playbackErrorMessage(playback.error)}
      </p>
    );

  return (
    <video
      controls
      preload="metadata"
      playsInline
      src={playback.data.playback_url}
      aria-label={`Vídeo de ${exerciseName}`}
      className="w-full rounded-md"
    />
  );
}
