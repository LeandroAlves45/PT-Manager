import { CheckCircle2, ChevronDown } from 'lucide-react';
import { useId, useState } from 'react';

import { PortalExerciseVideo } from '@/features/portal/components/PortalExerciseVideo';
import { WorkoutSetRow } from '@/features/portal/components/WorkoutSetRow';
import { exerciseDisplayName, prescriptionSummary } from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';

type WorkoutExercise = components['schemas']['MyWorkoutExerciseResponse'];

/**
 * Cartão colapsável de um exercício do treino de hoje: nome, resumo da prescrição, notas do
 * personal trainer, vídeo e uma linha por série.
 *
 * Abre fechado quando o exercício já está completo, para o cliente ver logo o que falta.
 * Fechar não desmonta as linhas (só as esconde): o texto escrito numa série sobrevive.
 *
 * @param completed O treino de hoje já foi concluído (bloqueia desmarcar séries).
 */
export function WorkoutExerciseCard({
  exercise,
  completed,
}: {
  exercise: WorkoutExercise;
  completed: boolean;
}) {
  const panelId = useId();
  const [expanded, setExpanded] = useState(!exercise.is_completed);
  const name = exerciseDisplayName(exercise);

  return (
    <section aria-label={name} className="border-border bg-card rounded-lg border">
      <h2>
        <button
          type="button"
          aria-expanded={expanded}
          aria-controls={panelId}
          className="flex min-h-11 w-full items-center gap-3 p-3 text-left"
          onClick={() => setExpanded((value) => !value)}
        >
          <span className="min-w-0 flex-1 space-y-0.5">
            <span className="flex items-center gap-2">
              <span className="font-medium">{name}</span>
              {exercise.exercise_group_id !== null && <Badge variant="secondary">Superset</Badge>}
            </span>
            <span className="text-muted-foreground block text-sm">
              {prescriptionSummary(exercise.sets)}
            </span>
          </span>
          {exercise.is_completed && (
            <CheckCircle2 aria-label="Exercício completo" className="text-primary size-5" />
          )}
          <ChevronDown
            aria-hidden
            className={cn('size-5 transition-transform', expanded && 'rotate-180')}
          />
        </button>
      </h2>

      <div id={panelId} hidden={!expanded} className="space-y-3 px-3 pb-3">
        {exercise.notes !== null && (
          <p className="text-muted-foreground text-sm">{exercise.notes}</p>
        )}
        {exercise.has_ready_video && (
          <PortalExerciseVideo exerciseId={exercise.exercise_id} exerciseName={name} />
        )}
        <div className="text-muted-foreground grid grid-cols-[2rem_1fr_1fr_2.75rem] gap-2 text-xs">
          <span>Série</span>
          <span>kg</span>
          <span>Reps</span>
          <span className="sr-only">Estado</span>
        </div>
        <ol className="space-y-2">
          {exercise.sets.map((set) => (
            <WorkoutSetRow
              key={set.id}
              prescriptionId={exercise.id}
              exerciseName={name}
              set={set}
              completed={completed}
            />
          ))}
        </ol>
      </div>
    </section>
  );
}
