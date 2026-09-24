import type { components } from '@/shared/api/schema';

type GlobalExercise = components['schemas']['GlobalExerciseResponse'];

/**
 * Regras e textos do vídeo gerido de um exercício global.
 *
 * Os limites espelham `ExerciseVideoPolicy` do backend. A validação local só serve para
 * falhar cedo, sem gastar um upload de 100 MB; a decisão final é sempre do servidor.
 */

export const ACCEPTED_VIDEO_TYPES = ['video/mp4', 'video/quicktime'] as const;
export const MAX_VIDEO_BYTES = 100 * 1024 * 1024;
const MAX_VIDEO_SECONDS = 180;
const MAX_LONG_SIDE = 1920;
const MIN_SHORT_SIDE = 240;

/** Estados que já não mudam sem uma nova ação do admin. */
export const TERMINAL_VIDEO_STATUSES = ['ready', 'failed', 'rejected'];

const VIDEO_STATUS_LABELS: Record<string, string> = {
  pending: 'pendente',
  processing: 'em processamento',
  ready: 'pronto',
  rejected: 'recusado',
  failed: 'falhado',
};

/**
 * Texto curto do vídeo de um exercício, partilhado pela lista e pelo formulário.
 *
 * `has_ready_video` distingue o vídeo que continua visível do registo mais recente: durante
 * uma substituição, ou depois de uma substituição falhada, o vídeo anterior ainda se vê.
 */
export function describeExerciseVideo(
  exercise: Pick<GlobalExercise, 'managed_video_status' | 'has_ready_video' | 'video_url'>
): string {
  const { managed_video_status: status, has_ready_video: hasReadyVideo } = exercise;
  if (status === null) return exercise.video_url ? 'Ligação de vídeo externa' : 'Sem vídeo';

  const label = VIDEO_STATUS_LABELS[status] ?? 'estado indisponível';
  if (hasReadyVideo && (status === 'pending' || status === 'processing'))
    return `Vídeo disponível · substituição ${label}`;
  if (hasReadyVideo && (status === 'failed' || status === 'rejected'))
    return `Vídeo disponível · última substituição ${label}`;
  return `Vídeo ${label}`;
}

/** Tipo e tamanho, antes de qualquer pedido. */
export function validateVideoFile(file: File): string | null {
  if (!(ACCEPTED_VIDEO_TYPES as readonly string[]).includes(file.type))
    return 'Formato não suportado. Usa um vídeo MP4 ou MOV.';
  if (file.size <= 0 || file.size > MAX_VIDEO_BYTES) return 'O vídeo tem de ter até 100 MB.';
  return null;
}

interface VideoMetadata {
  duration: number;
  width: number;
  height: number;
}

/**
 * Lê duração e resolução pelo próprio browser. Devolve `null` quando não é possível
 * (codec que o browser não decifra, evento que nunca chega, ambiente sem suporte): é só
 * uma verificação antecipada e o servidor valida na mesma.
 */
export function readVideoMetadata(file: File, timeoutMs = 5000): Promise<VideoMetadata | null> {
  let url: string;
  try {
    url = URL.createObjectURL(file);
  } catch {
    return Promise.resolve(null);
  }

  return new Promise((resolve) => {
    const video = document.createElement('video');
    const timer = setTimeout(() => finish(null), timeoutMs);
    function finish(metadata: VideoMetadata | null) {
      clearTimeout(timer);
      video.onloadedmetadata = null;
      video.onerror = null;
      video.removeAttribute('src');
      URL.revokeObjectURL(url);
      resolve(metadata);
    }
    video.preload = 'metadata';
    video.onloadedmetadata = () =>
      finish({ duration: video.duration, width: video.videoWidth, height: video.videoHeight });
    video.onerror = () => finish(null);
    video.src = url;
  });
}

/** Mesmas regras de duração e resolução do backend. */
export function validateVideoMetadata(metadata: VideoMetadata): string | null {
  if (Number.isFinite(metadata.duration) && metadata.duration > MAX_VIDEO_SECONDS)
    return videoErrorMessage('exercise_video_duration_exceeded');
  const longSide = Math.max(metadata.width, metadata.height);
  const shortSide = Math.min(metadata.width, metadata.height);
  if (longSide > MAX_LONG_SIDE) return videoErrorMessage('exercise_video_resolution_exceeded');
  if (shortSide > 0 && shortSide < MIN_SHORT_SIDE)
    return videoErrorMessage('exercise_video_resolution_too_small');
  return null;
}

const VIDEO_ERROR_MESSAGES: Record<string, string> = {
  exercise_video_content_type_unsupported: 'Formato não suportado. Usa um vídeo MP4 ou MOV.',
  exercise_video_container_unsupported: 'Formato não suportado. Usa um vídeo MP4 ou MOV.',
  exercise_video_container_mismatch: 'O ficheiro não corresponde ao formato indicado.',
  exercise_video_content_type_mismatch: 'O ficheiro enviado não corresponde ao declarado.',
  exercise_video_size_mismatch: 'O ficheiro enviado não corresponde ao declarado.',
  exercise_video_size_invalid: 'O vídeo tem de ter até 100 MB.',
  exercise_video_codec_unsupported: 'Codec de vídeo não suportado. Usa H.264.',
  exercise_video_audio_codec_unsupported: 'Codec de áudio não suportado. Usa AAC.',
  exercise_video_track_layout_unsupported:
    'O vídeo tem de ter uma faixa de imagem e, no máximo, uma de áudio.',
  exercise_video_duration_exceeded: 'O vídeo tem de ter até 3 minutos.',
  exercise_video_duration_invalid: 'Não foi possível ler a duração do vídeo.',
  exercise_video_resolution_exceeded: 'A resolução máxima é 1920 px no lado maior.',
  exercise_video_resolution_too_small: 'A resolução mínima é 240 px no lado menor.',
  exercise_video_upload_in_progress: 'Já existe um envio em curso para este exercício.',
  exercise_video_upload_window_open: 'Já existe um envio em curso para este exercício.',
  exercise_video_upload_expired: 'O envio expirou. Tenta novamente.',
  exercise_video_upload_abandoned: 'O envio expirou. Tenta novamente.',
  exercise_video_upload_incomplete: 'O ficheiro não chegou ao armazenamento. Tenta novamente.',
  exercise_video_upload_rejected: 'O vídeo foi recusado na validação técnica.',
  exercise_video_processing_timeout: 'O processamento demorou demasiado. Tenta novamente.',
  exercise_video_storage_unavailable: 'O serviço de vídeo não está disponível de momento.',
  exercise_video_probe_unavailable: 'O serviço de vídeo não está disponível de momento.',
  exercise_video_exercise_inactive: 'Reativa o exercício antes de enviar um vídeo.',
  exercise_video_exercise_blocked: 'Este exercício está bloqueado.',
  exercise_video_quota_exceeded: 'Foi atingido o limite de vídeos.',
  exercise_video_not_found: 'O exercício não tem vídeo pronto.',
  exercise_video_state_conflict: 'O estado do vídeo mudou. Fecha e volta a abrir o exercício.',
};

/** Mensagem pt-PT para um código de erro da API ou `failure_code` de um vídeo. */
export function videoErrorMessage(code: string | null | undefined): string {
  return (code ? VIDEO_ERROR_MESSAGES[code] : undefined) ?? 'Não foi possível processar o vídeo.';
}
