import { ArrowLeft, ChevronDown } from 'lucide-react';
import { useId, useState } from 'react';
import { Link } from 'react-router';

import { usePortalPlanQuery, usePortalWorkoutTodayQuery } from '@/features/portal/api/portal';
import { PlannedSetChips } from '@/features/portal/components/PlannedSetChips';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { PortalExerciseVideo } from '@/features/portal/components/PortalExerciseVideo';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { TrainingPlanError } from '@/features/portal/components/TrainingPlanError';
import { weekdayLabel } from '@/features/portal/lib/schedule';
import {
  exerciseDisplayName,
  isTrainingPlanUnavailable,
  prescriptionSummary,
  workoutSize,
} from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate, formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';

type TrainingDay = components['schemas']['MyTrainingDayResponse'];

/** Dias agrupados por semana do ciclo, pela ordem da API. */
function groupByWeek(days: readonly TrainingDay[]): [number, TrainingDay[]][] {
  const weeks = new Map<number, TrainingDay[]>();
  for (const day of days) weeks.set(day.week_number, [...(weeks.get(day.week_number) ?? []), day]);
  return [...weeks.entries()];
}

/** Dia de hoje dentro do plano, a partir do treino de hoje (fuso do personal trainer). */
interface TodayInPlan {
  readonly weekNumber: number;
  readonly dayOfWeek: number;
}

/**
 * Plano de treino completo (`/portal/plan`), só de leitura.
 *
 * Semana → dia → exercício → série, a partir de `GET /portal/my-plan`. As semanas e os dias
 * recolhem: abre a semana de hoje e, dentro dela, o dia de hoje, destacado com a cor da marca.
 * Não regista nada: o registo é só no treino de hoje. O vídeo aparece onde a API diz
 * `has_ready_video`.
 */
export function PortalPlanPage() {
  const query = usePortalPlanQuery();
  const todayQuery = usePortalWorkoutTodayQuery();

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

  // Sem dados, ou com um estado de negócio (sem plano, ficha arquivada): o ecrã é o da falha.
  // Uma falha técnica com dados em cache fica só como aviso, mais abaixo.
  if (query.data === undefined || isTrainingPlanUnavailable(query.error))
    return <TrainingPlanError error={query.error} onRetry={() => void query.refetch()} />;

  const plan = query.data;
  const today = todayQuery.data;
  const todayInPlan: TodayInPlan | null =
    today?.status === 'workout' && today.week_number !== null && today.day_of_week !== null
      ? { weekNumber: today.week_number, dayOfWeek: today.day_of_week }
      : null;
  const weeks = groupByWeek(plan.days);
  // Sem treino hoje (descanso, fora do plano), abre a primeira semana para não ficar tudo fechado.
  const openWeek =
    todayInPlan !== null && weeks.some(([week]) => week === todayInPlan.weekNumber)
      ? todayInPlan.weekNumber
      : weeks[0]?.[0];

  return (
    <section className="space-y-4">
      <Button asChild variant="ghost" className="-ml-3 min-h-11">
        <Link to="/portal/today">
          <ArrowLeft aria-hidden />
          Treino de hoje
        </Link>
      </Button>

      <header className="space-y-1">
        <h1 className="font-display pe-1 text-3xl leading-none">{plan.name}</h1>
        <p className="text-muted-foreground font-mono text-xs">
          {plan.end_date === null
            ? `Desde ${formatDate(plan.start_date)}`
            : `${formatDate(plan.start_date)} a ${formatDate(plan.end_date)}`}
        </p>
        {plan.description !== null && <p className="text-sm">{plan.description}</p>}
        {plan.notes !== null && <p className="text-muted-foreground text-sm">{plan.notes}</p>}
      </header>

      {query.isError && (
        <StaleDataNotice onRetry={() => void query.refetch()} retrying={query.isFetching} />
      )}

      {plan.days.length === 0 ? (
        <p className="text-sm">O plano ainda não tem dias de treino.</p>
      ) : (
        weeks.map(([week, days]) => (
          <PlanWeek
            key={week}
            week={week}
            days={days}
            defaultOpen={week === openWeek}
            todayInPlan={todayInPlan}
          />
        ))
      )}
    </section>
  );
}

/**
 * Semana do plano recolhível. Abre por omissão a semana de hoje; depois de o cliente a tocar,
 * o estado é dele e uma resposta tardia do treino de hoje já não o muda.
 */
function PlanWeek({
  week,
  days,
  defaultOpen,
  todayInPlan,
}: {
  week: number;
  days: readonly TrainingDay[];
  defaultOpen: boolean;
  todayInPlan: TodayInPlan | null;
}) {
  const panelId = useId();
  const [override, setOverride] = useState<boolean | null>(null);
  const open = override ?? defaultOpen;

  return (
    <section aria-label={`Semana ${formatNumber(week)}`} className="space-y-3">
      <h2>
        <button
          type="button"
          aria-expanded={open}
          aria-controls={panelId}
          className="flex min-h-11 w-full items-center gap-3 text-left"
          onClick={() => setOverride(!open)}
        >
          <span className="font-display pe-1 text-xl">Semana {formatNumber(week)}</span>
          <span className="text-muted-foreground flex-1 text-sm">
            {days.length === 1 ? '1 dia' : `${formatNumber(days.length)} dias`}
          </span>
          <ChevronDown
            aria-hidden
            className={cn('size-5 shrink-0 transition-transform', open && 'rotate-180')}
          />
        </button>
      </h2>

      <div
        id={panelId}
        hidden={!open}
        className="grid grid-cols-1 items-start gap-4 md:grid-cols-2"
      >
        {days.map((day) => (
          <PlanDay
            key={day.id}
            day={day}
            isToday={
              todayInPlan !== null &&
              todayInPlan.weekNumber === day.week_number &&
              todayInPlan.dayOfWeek === day.day_of_week
            }
          />
        ))}
      </div>
    </section>
  );
}

/**
 * Dia do plano recolhível, num cartão de treino. O dia de hoje abre por omissão e leva o
 * contorno da marca e o rótulo «Hoje» (a cor nunca é a única pista).
 */
function PlanDay({ day, isToday }: { day: TrainingDay; isToday: boolean }) {
  const panelId = useId();
  const [override, setOverride] = useState<boolean | null>(null);
  const open = override ?? isToday;

  return (
    <PortalCard tone="training" className={cn(isToday && 'border-primary ring-primary/40 ring-2')}>
      <article className="space-y-3">
        <h3>
          <button
            type="button"
            aria-expanded={open}
            aria-controls={panelId}
            className="flex min-h-11 w-full items-center gap-3 text-left"
            onClick={() => setOverride(!open)}
          >
            <span className="min-w-0 flex-1 space-y-0.5">
              <span className="flex items-center gap-2">
                <span className="font-display pe-1 text-lg">{weekdayLabel(day.day_of_week)}</span>
                {isToday && <Badge>Hoje</Badge>}
              </span>
              <span className="text-muted-foreground block text-sm">
                {workoutSize(day.exercises)}
              </span>
            </span>
            <ChevronDown
              aria-hidden
              className={cn('size-5 shrink-0 transition-transform', open && 'rotate-180')}
            />
          </button>
        </h3>

        <div id={panelId} hidden={!open} className="space-y-3">
          {day.notes !== null && <p className="text-muted-foreground text-sm">{day.notes}</p>}

          <ol className="space-y-3">
            {day.exercises.map((exercise) => {
              const name = exerciseDisplayName(exercise);

              return (
                <li key={exercise.id} className="border-border space-y-2 border-t pt-3">
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{name}</span>
                    {exercise.exercise_group_id !== null && (
                      <Badge variant="secondary">Superset</Badge>
                    )}
                  </div>
                  <p className="text-muted-foreground text-sm">
                    {prescriptionSummary(exercise.sets)}
                  </p>
                  {exercise.notes !== null && (
                    <p className="text-muted-foreground text-sm">{exercise.notes}</p>
                  )}
                  <ol className="space-y-2">
                    {exercise.sets.map((set) => (
                      <li key={set.id} className="flex flex-wrap items-center gap-x-2 gap-y-1">
                        <span className="text-muted-foreground w-14 font-mono text-xs">
                          Série {formatNumber(set.set_number)}
                        </span>
                        <PlannedSetChips set={set} />
                      </li>
                    ))}
                  </ol>
                  {exercise.has_ready_video && (
                    <PortalExerciseVideo exerciseId={exercise.exercise_id} exerciseName={name} />
                  )}
                </li>
              );
            })}
          </ol>
        </div>
      </article>
    </PortalCard>
  );
}
