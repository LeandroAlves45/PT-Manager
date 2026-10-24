import { CalendarDays, CalendarX, CheckCircle2, Moon } from 'lucide-react';
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';

import { InitialAssessmentMissingNotice } from '@/features/portal/components/InitialAssessmentMissingNotice';
import { usePortalWorkoutDayQuery, usePortalWorkoutTodayQuery } from '@/features/portal/api/portal';
import { CompleteWorkoutDialog } from '@/features/portal/components/CompleteWorkoutDialog';
import { PastWorkoutExercise } from '@/features/portal/components/PastWorkoutExercise';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { TrainingPlanError } from '@/features/portal/components/TrainingPlanError';
import { WorkoutDayPicker } from '@/features/portal/components/WorkoutDayPicker';
import { WorkoutExerciseCard } from '@/features/portal/components/WorkoutExerciseCard';
import { WorkoutProgress } from '@/features/portal/components/WorkoutProgress';
import { longLocalDate, planDayTitle, shortLocalDate } from '@/features/portal/lib/schedule';
import {
  isTrainingPlanUnavailable,
  isWorkoutDayUnavailable,
  workoutSize,
} from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { EmptyState } from '@/shared/components/EmptyState';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate, formatNumber } from '@/shared/lib/format';

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

/** Esqueleto comum ao treino de hoje e ao de um dia passado. */
function WorkoutSkeleton({ label }: { label: string }) {
  return (
    <>
      <Skeleton className="h-10 w-48" />
      <div role="status" aria-label={label} className="space-y-3">
        <Skeleton className="h-24" />
        <Skeleton className="h-40" />
        <Skeleton className="h-40" />
      </div>
    </>
  );
}

/**
 * Treino do portal (`/portal/today`).
 *
 * Sem parâmetro mostra o treino de hoje, editável. Com `?dia=AAAA-MM-DD` mostra esse dia
 * passado só para ler. O dia escolhido vive na query string e não é guardado: abrir
 * `/portal/today` outra vez mostra sempre hoje.
 */
export function PortalTodayPage() {
  const [params, setParams] = useSearchParams();
  const raw = params.get('dia');
  // Só uma data `AAAA-MM-DD` abre um dia passado; o resto (vazio, lixo) é o treino de hoje.
  const selected = raw !== null && /^\d{4}-\d{2}-\d{2}$/.test(raw) ? raw : null;

  const select = (date: string | null) => setParams(date === null ? {} : { dia: date });

  return (
    <section className="mx-auto w-full max-w-xl space-y-4">
      <InitialAssessmentMissingNotice />
      <WorkoutDayPicker selected={selected} onSelect={select} />
      {selected === null ? <TodayWorkout /> : <PastWorkout date={selected} />}
    </section>
  );
}

/**
 * Treino de hoje, a partir de `GET /portal/my-workout/today`.
 *
 * Estados: treino (registar, corrigir, desmarcar e concluir), descanso e fora do plano (com o
 * próximo treino), treino concluído (resumo das séries registadas sobre as planeadas) e sem
 * plano (404 `portal_training_plan_not_available`). Cada escrita volta a ler este ecrã e o
 * cartão do Início.
 */
function TodayWorkout() {
  const query = usePortalWorkoutTodayQuery();

  if (query.isPending) return <WorkoutSkeleton label="A carregar o treino…" />;

  // Sem dados, ou com um estado de negócio (sem plano, ficha arquivada): o ecrã é o da falha.
  // Uma falha técnica com dados em cache fica só como aviso, mais abaixo.
  if (query.data === undefined || isTrainingPlanUnavailable(query.error))
    return <TrainingPlanError error={query.error} onRetry={() => void query.refetch()} />;

  const today = query.data;

  return (
    <>
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">{longLocalDate(today.local_date)}</p>
        <h1 className="font-display pe-1 text-3xl leading-none">Treino de hoje</h1>
        <p className="text-muted-foreground text-sm">{today.plan_name}</p>
      </header>

      {query.isError && (
        <StaleDataNotice onRetry={() => void query.refetch()} retrying={query.isFetching} />
      )}

      {today.status === 'workout' && today.day !== null ? (
        <WorkoutDay today={today} day={today.day} />
      ) : (
        <NoWorkoutToday today={today} />
      )}
    </>
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
  const completed = today.completed_at !== null;

  return (
    <>
      <PortalCard tone="training">
        {today.week_number !== null && today.day_of_week !== null && (
          <p className="font-display pe-1 text-xl leading-tight">
            {planDayTitle(today.week_number, today.day_of_week)}
          </p>
        )}
        {day.notes !== null && <p className="text-muted-foreground text-sm">{day.notes}</p>}
        <p className="text-sm">{workoutSize(day.exercises)}</p>
        <WorkoutProgress logged={logged} planned={planned} />
      </PortalCard>

      {completed && (
        <p
          role="status"
          className="border-border bg-card flex items-center gap-2 rounded-xl border p-3 text-sm"
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

/** Ligação que tira o `?dia` e repõe a edição do treino de hoje. */
function BackToToday() {
  return (
    <Button asChild variant="outline" className="min-h-11 w-full">
      <Link to="/portal/today">Voltar a hoje</Link>
    </Button>
  );
}

/**
 * Um dia passado do plano (`GET /portal/my-workout/day`), só para ler.
 *
 * Por série mostra o planeado ao lado do registado e a nota da série; no fim, a nota de
 * conclusão. Não tem registar, corrigir, desmarcar nem concluir. Um dia que o servidor recusa
 * (hoje, futuro, fora do plano) mostra uma mensagem e a ligação para hoje.
 */
function PastWorkout({ date }: { date: string }) {
  const query = usePortalWorkoutDayQuery(date);

  if (query.isPending) return <WorkoutSkeleton label="A carregar o treino…" />;

  if (query.data === undefined && isWorkoutDayUnavailable(query.error))
    return (
      <>
        <EmptyState
          titleAs="h1"
          icon={CalendarX}
          title="Dia indisponível"
          description="Só podes consultar os dias passados do teu plano."
        />
        <BackToToday />
      </>
    );

  if (query.data === undefined || isTrainingPlanUnavailable(query.error))
    return <TrainingPlanError error={query.error} onRetry={() => void query.refetch()} />;

  const past = query.data;
  const { planned_sets: planned, logged_sets: logged } = past.progress;

  return (
    <>
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">{longLocalDate(past.local_date)}</p>
        <h1 className="font-display pe-1 text-3xl leading-none">
          Treino de {formatDate(past.local_date)}
        </h1>
        <p className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
          {past.plan_name}
          <Badge variant="secondary">Só leitura</Badge>
        </p>
      </header>

      {past.status === 'workout' && past.day !== null ? (
        <>
          <PortalCard tone="training">
            {past.week_number !== null && past.day_of_week !== null && (
              <p className="font-display pe-1 text-xl leading-tight">
                {planDayTitle(past.week_number, past.day_of_week)}
              </p>
            )}
            {past.day.notes !== null && (
              <p className="text-muted-foreground text-sm">{past.day.notes}</p>
            )}
            <p className="text-sm">{workoutSize(past.day.exercises)}</p>
            <WorkoutProgress logged={logged} planned={planned} />
          </PortalCard>

          <p className="border-border bg-card flex items-center gap-2 rounded-xl border p-3 text-sm">
            {past.completed_at !== null ? (
              <>
                <CheckCircle2 aria-hidden className="text-primary size-5 shrink-0" />
                Treino concluído
              </>
            ) : (
              'Treino não concluído'
            )}
          </p>

          {past.completion_notes !== null && (
            <div className="border-border bg-card space-y-1 rounded-xl border p-3">
              <p className="text-muted-foreground font-mono text-xs">Nota de conclusão</p>
              <p className="text-sm">{past.completion_notes}</p>
            </div>
          )}

          <div className="space-y-3">
            {past.day.exercises.map((exercise) => (
              <PastWorkoutExercise key={exercise.id} exercise={exercise} />
            ))}
          </div>
        </>
      ) : (
        <EmptyState
          icon={Moon}
          title={past.status === 'rest' ? 'Dia de descanso' : 'Fora do período do plano'}
          description={
            past.status === 'rest'
              ? 'Neste dia não tinhas treino.'
              : 'Este dia está fora do período do teu plano.'
          }
        />
      )}

      <BackToToday />
    </>
  );
}
