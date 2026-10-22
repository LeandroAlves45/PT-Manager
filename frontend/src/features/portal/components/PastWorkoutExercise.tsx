import { PlannedSetChips } from '@/features/portal/components/PlannedSetChips';
import {
  exerciseDisplayName,
  loggedSetLabel,
  prescriptionSummary,
} from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';
import { formatNumber } from '@/shared/lib/format';

type WorkoutExercise = components['schemas']['MyWorkoutExerciseResponse'];

/**
 * Exercício de um dia passado, só para ler: por série, o planeado ao lado do registado e a nota
 * da série. Uma série sem registo diz «Sem registo». Não tem campos nem botões de escrita.
 */
export function PastWorkoutExercise({ exercise }: { exercise: WorkoutExercise }) {
  const name = exerciseDisplayName(exercise);

  return (
    <section aria-label={name} className="border-border bg-card space-y-3 rounded-xl border p-3">
      <header className="space-y-0.5">
        <h2 className="flex items-center gap-2 font-medium">
          {name}
          {exercise.exercise_group_id !== null && <Badge variant="secondary">Superset</Badge>}
        </h2>
        <p className="text-muted-foreground text-sm">{prescriptionSummary(exercise.sets)}</p>
        {exercise.notes !== null && (
          <p className="text-muted-foreground text-sm">{exercise.notes}</p>
        )}
      </header>

      <ol className="space-y-3">
        {exercise.sets.map((set) => (
          <li key={set.id} className="border-border space-y-1.5 border-t pt-3">
            <p className="font-mono text-xs">Série {formatNumber(set.set_number)}</p>
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span className="text-muted-foreground w-20 text-xs">Planeado</span>
              <PlannedSetChips set={set} />
            </div>
            <p className="flex flex-wrap items-baseline gap-x-2 text-sm">
              <span className="text-muted-foreground w-20 text-xs">Registado</span>
              {set.logged === null ? (
                <span className="text-muted-foreground">Sem registo</span>
              ) : (
                <span className="font-medium tabular-nums">{loggedSetLabel(set.logged)}</span>
              )}
            </p>
            {set.logged !== null && set.logged.notes !== null && (
              <p className="text-muted-foreground text-sm">{set.logged.notes}</p>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
