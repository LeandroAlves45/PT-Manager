/**
 * API pública do vídeo gerido de um exercício.
 *
 * Partilhado pelo catálogo global do admin (`audience="superuser"`) e pela biblioteca do
 * trainer (`audience="trainer"`); cada consumidor diz que lista invalidar.
 */
export type { ExerciseVideoAudience } from '@/features/exercise-video/api/exerciseVideo';
export { ExerciseVideoPanel } from '@/features/exercise-video/components/ExerciseVideoPanel';
export {
  describeExerciseVideo,
  type ExerciseVideoSubject,
} from '@/features/exercise-video/lib/exerciseVideo';
