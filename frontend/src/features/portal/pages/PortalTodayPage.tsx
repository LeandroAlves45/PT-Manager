import { CalendarDays, CheckCircle2, Moon } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';

import { usePortalWorkoutTodayQuery } from '@/features/portal/api/portal';
import { CompleteWorkoutDialog } from '@/features/portal/components/CompleteWorkoutDialog';
import { TrainingPlanError } from '@/features/portal/components/TrainingPlanError';
import { WorkoutExerciseCard } from '@/features/portal/components/WorkoutExerciseCard';
import { progressPercent, workoutSize } from '@/features/portal/lib/workout';
import { longLocalDate, planDayTitle, shortLocalDate } from '@/features/portal/lib/schedule';
import type { components } from '@/shared/api/schema';
import { EmptyState } from '@/shared/components/EmptyState';
import { Button } from '@/shared/components/ui/button';
import { Progress } from '@/shared/components/ui/progress';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatNumber } from '@/shared/lib/format';

type WorkoutToday = components['schemas']['MyWorkoutTodayResponse'];

/** Ligação ao plano completo, só de leitura: a barra não tem item para ele. */
function PlanLink() {
  return (
    <Button asChild variant="outline" className="min-h-11 w-full">
      <Link to="/portal/plan">
        <CalendarDays aria-hidden />
        Ver plano completo
      </Link>
    </Button>
  );
}

/**
 * Treino de hoje (`/portal/today`), a partir de `GET /portal/my-workout/today`.
 *
 * Estados: treino (registar, corrigir, desmarcar e concluir), descanso e fora do plano (com o
 * próximo treino), treino concluído (resumo das séries registadas sobre as planeadas) e sem
 * plano (404 `portal_training_plan_not_available`). Cada escrita volta a ler este ecrã e o
 * cartão do Início.
 */
export function PortalTodayPage() {
  const query = usePortalWorkoutTodayQuery();

  if (query.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <div role="status" aria-label="A carregar o treino…" className="space-y-3">
          <Skeleton className="h-24" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      </section>
    );

  if (query.isError)
    return <TrainingPlanError error={query.error} onRetry={() => query.refetch()} />;

  const today = query.data;

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">{longLocalDate(today.local_date)}</p>
        <h1 className="font-display text-3xl leading-none">Treino de hoje</h1>
        <p className="text-muted-foreground text-sm">{today.plan_name}</p>
      </header>

      {today.status === 'workout' && today.day !== null ? (
        <WorkoutDay today={today} day={today.day} />
      ) : (
        <NoWorkoutToday today={today} />
      )}
    </section>
  );
}

/** Descanso ou fora do período do plano, com o próximo treino quando existe. */
function NoWorkoutToday({ today }: { today: WorkoutToday }) {
  const next = today.next_workout;

  return (
    <>
      <EmptyState
        icon={Moon}
        title={today.status === 'rest' ? 'Dia de descanso' : 'Fora do período do plano'}
        description={
          next === null
            ? today.status === 'rest'
              ? 'Hoje não tens treino.'
              : 'Hoje está fora do período do teu plano.'
            : `Próximo treino: ${shortLocalDate(next.date)} · ${planDayTitle(next.week_number, next.day_of_week)}.`
        }
      />
      <PlanLink />
    </>
  );
}

function WorkoutDay({
  today,
  day,
}: {
  today: WorkoutToday;
  day: NonNullable<WorkoutToday['day']>;
}) {
  const [confirming, setConfirming] = useState(false);
  const { planned_sets: planned, logged_sets: logged } = today.progress;
  const percent = progressPercent(logged, planned);
  const completed = today.completed_at !== null;

  return (
    <>
      <div className="border-border bg-card space-y-2 rounded-lg border p-3">
        {today.week_number !== null && today.day_of_week !== null && (
          <p className="font-display text-xl leading-tight">
            {planDayTitle(today.week_number, today.day_of_week)}
          </p>
        )}
        {day.notes !== null && <p className="text-muted-foreground text-sm">{day.notes}</p>}
        <p className="text-sm">{workoutSize(day.exercises)}</p>
        <Progress value={percent} aria-label="Séries registadas" />
        <p className="text-muted-foreground font-mono text-xs tabular-nums">
          {formatNumber(logged)} de {formatNumber(planned)} séries · {formatNumber(percent)} %
        </p>
      </div>

      {completed && (
        <p
          role="status"
          className="border-border bg-card flex items-center gap-2 rounded-lg border p-3 text-sm"
        >
          <CheckCircle2 aria-hidden className="text-primary size-5 shrink-0" />
          Treino concluído · {formatNumber(logged)} de {formatNumber(planned)} séries registadas.
        </p>
      )}

      <div className="space-y-3">
        {day.exercises.map((exercise) => (
          <WorkoutExerciseCard key={exercise.id} exercise={exercise} completed={completed} />
        ))}
      </div>

      <PlanLink />

      {!completed && (
        <>
          {/* Fixo acima da barra inferior do portal (64 px + área segura). */}
          <div className="sticky bottom-[calc(64px+env(safe-area-inset-bottom,20px)+0.75rem)] z-10">
            <Button
              type="button"
              className="min-h-11 w-full shadow-md"
              onClick={() => setConfirming(true)}
            >
              Concluir treino
            </Button>
          </div>
          <CompleteWorkoutDialog
            open={confirming}
            onOpenChange={setConfirming}
            dayId={day.id}
            remainingSets={planned - logged}
          />
        </>
      )}
    </>
  );
}
