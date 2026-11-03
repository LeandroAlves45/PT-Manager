/**
 * API pública do vídeo gerido de um exercício.
 *
 * Partilhado pelo catálogo global do admin (`audience="superuser"`) e pela biblioteca do
 * personal trainer (`audience="trainer"`); cada consumidor diz que lista invalidar. O portal do
 * cliente só lê o vídeo pronto (`useVideoPlayback('client', …)`).
 */
export {
  useVideoPlayback,
  type ExerciseVideoAudience,
  type ExerciseVideoReader,
} from '@/features/exercise-video/api/exerciseVideo';
export { ExerciseVideoPanel } from '@/features/exercise-video/components/ExerciseVideoPanel';
export { ExternalVideoLink } from '@/features/exercise-video/components/ExternalVideoLink';
export {
  EXTERNAL_VIDEO_URL_ERROR,
  EXTERNAL_VIDEO_URL_MAX_LENGTH,
  externalVideoLink,
  isValidExternalVideoUrl,
} from '@/features/exercise-video/lib/externalVideo';
export {
  describeExerciseVideo,
  isVideoInProgress,
  type ExerciseVideoSubject,
} from '@/features/exercise-video/lib/exerciseVideo';
