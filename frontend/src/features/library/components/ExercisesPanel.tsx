import { Video } from 'lucide-react';

import { useExerciseListQuery } from '@/features/library/api/queries';
import { LibraryPanel, type LibraryColumn } from '@/features/library/components/LibraryPanel';
import { useLibraryFilters, type LibraryControls } from '@/features/library/lib/useLibraryFilters';
import type { components } from '@/shared/api/schema';
import { formatMuscleGroups } from '@/shared/lib/muscleGroups';

type Exercise = components['schemas']['ExerciseResponse'];

const COLUMNS: readonly LibraryColumn<Exercise>[] = [
  { header: 'Grupos musculares', cell: (exercise) => formatMuscleGroups(exercise.muscle_groups) },
  { header: 'Equipamento', cell: (exercise) => exercise.equipment ?? '—' },
  {
    header: 'Vídeo',
    cell: (exercise) =>
      exercise.has_ready_video ? (
        <span className="text-foreground inline-flex items-center gap-1">
          <Video aria-hidden className="size-4" /> Com vídeo
        </span>
      ) : (
        '—'
      ),
  },
];

/** Separador "Exercícios": globais e privados, com vídeo. */
export function ExercisesPanel({ controls }: { controls: LibraryControls }) {
  const { filters, filtered } = useLibraryFilters(controls);
  const query = useExerciseListQuery(filters);

  return (
    <LibraryPanel
      kind="exercises"
      query={query}
      controls={controls}
      filtered={filtered}
      columns={COLUMNS}
    />
  );
}
