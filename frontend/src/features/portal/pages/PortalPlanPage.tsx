import { ArrowLeft } from 'lucide-react';
import { Link } from 'react-router';

import { usePortalPlanQuery } from '@/features/portal/api/portal';
import { PortalExerciseVideo } from '@/features/portal/components/PortalExerciseVideo';
import { TrainingPlanError } from '@/features/portal/components/TrainingPlanError';
import { weekdayLabel } from '@/features/portal/lib/schedule';
import {
  exerciseDisplayName,
  plannedSetLabel,
  prescriptionSummary,
  workoutSize,
} from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate, formatNumber } from '@/shared/lib/format';

type TrainingDay = components['schemas']['MyTrainingDayResponse'];

/** Dias agrupados por semana do ciclo, pela ordem da API. */
function groupByWeek(days: readonly TrainingDay[]): [number, TrainingDay[]][] {
  const weeks = new Map<number, TrainingDay[]>();
  for (const day of days) weeks.set(day.week_number, [...(weeks.get(day.week_number) ?? []), day]);
  return [...weeks.entries()];
}

/**
 * Plano de treino completo (`/portal/plan`), só de leitura (decisão U9).
 *
 * Semana → dia → exercício → série, a partir de `GET /portal/my-plan`. Não regista nada: o
 * registo é só no treino de hoje. O vídeo aparece onde a API diz `has_ready_video`.
 */
export function PortalPlanPage() {
  const query = usePortalPlanQuery();

  if (query.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <div role="status" aria-label="A carregar o plano…" className="space-y-3">
          <Skeleton className="h-32" />
          <Skeleton className="h-32" />
        </div>
      </section>
    );

  if (query.isError)
    return <TrainingPlanError error={query.error} onRetry={() => query.refetch()} />;

  const plan = query.data;

  return (
    <section className="space-y-4">
      <Button asChild variant="ghost" className="-ml-3 min-h-11">
        <Link to="/portal/today">
          <ArrowLeft aria-hidden />
          Treino de hoje
        </Link>
      </Button>

      <header className="space-y-1">
        <h1 className="font-display text-3xl leading-none">{plan.name}</h1>
        <p className="text-muted-foreground font-mono text-xs">
          {plan.end_date === null
            ? `Desde ${formatDate(plan.start_date)}`
            : `${formatDate(plan.start_date)} a ${formatDate(plan.end_date)}`}
        </p>
        {plan.description !== null && <p className="text-sm">{plan.description}</p>}
        {plan.notes !== null && <p className="text-muted-foreground text-sm">{plan.notes}</p>}
      </header>

      {plan.days.length === 0 ? (
        <p className="text-sm">O plano ainda não tem dias de treino.</p>
      ) : (
        groupByWeek(plan.days).map(([week, days]) => (
          <section key={week} aria-label={`Semana ${formatNumber(week)}`} className="space-y-3">
            <h2 className="font-display text-xl">Semana {formatNumber(week)}</h2>
            {days.map((day) => (
              <PlanDay key={day.id} day={day} />
            ))}
          </section>
        ))
      )}
    </section>
  );
}

function PlanDay({ day }: { day: TrainingDay }) {
  return (
    <article className="border-border bg-card space-y-3 rounded-lg border p-3">
      <header className="space-y-0.5">
        <h3 className="font-medium">{weekdayLabel(day.day_of_week)}</h3>
        <p className="text-muted-foreground text-sm">{workoutSize(day.exercises)}</p>
        {day.notes !== null && <p className="text-muted-foreground text-sm">{day.notes}</p>}
      </header>

      <ol className="space-y-3">
        {day.exercises.map((exercise) => {
          const name = exerciseDisplayName(exercise);

          return (
            <li key={exercise.id} className="border-border space-y-2 border-t pt-3">
              <div className="flex items-center gap-2">
                <span className="font-medium">{name}</span>
                {exercise.exercise_group_id !== null && <Badge variant="secondary">Superset</Badge>}
              </div>
              <p className="text-muted-foreground text-sm">{prescriptionSummary(exercise.sets)}</p>
              {exercise.notes !== null && (
                <p className="text-muted-foreground text-sm">{exercise.notes}</p>
              )}
              <ul className="space-y-1 text-sm tabular-nums">
                {exercise.sets.map((set) => (
                  <li key={set.id}>{plannedSetLabel(set)}</li>
                ))}
              </ul>
              {exercise.has_ready_video && (
                <PortalExerciseVideo exerciseId={exercise.exercise_id} exerciseName={name} />
              )}
            </li>
          );
        })}
      </ol>
    </article>
  );
}
