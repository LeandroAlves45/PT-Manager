import { ClipboardCheck, Dumbbell, Pill, UserX, UtensilsCrossed } from 'lucide-react';

import { usePortalHomeQuery } from '@/features/portal/api/portal';
import { HomeCard } from '@/features/portal/components/HomeCard';
import { longLocalDate, planDayTitle, shortLocalDate } from '@/features/portal/lib/schedule';
import type { components } from '@/shared/api/schema';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Progress } from '@/shared/components/ui/progress';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatNumber } from '@/shared/lib/format';

type PortalHome = components['schemas']['MyPortalHomeResponse'];
type HomeWorkout = components['schemas']['MyHomeWorkoutResponse'];

/** "1 exercício" / "3 exercícios": as contagens do backend podem ser 0 ou 1. */
function count(value: number, singular: string, plural: string): string {
  return `${formatNumber(value)} ${value === 1 ? singular : plural}`;
}

/**
 * Início do portal: quatro cartões a partir de `GET /portal/home`.
 *
 * Cada cartão tem o seu estado vazio, porque o backend devolve `null` por bloco (sem plano
 * de treino, sem plano alimentar, sem check-in), e um só pedido alimenta o ecrã todo. A
 * data de referência é sempre `local_date` (fuso do personal trainer), nunca o relógio do browser.
 */
export function PortalHomePage() {
  const query = usePortalHomeQuery();

  if (query.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-40" />
        <div role="status" aria-label="A carregar o início…" className="grid gap-4 sm:grid-cols-2">
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      </section>
    );

  if (query.isError) {
    // Ficha arquivada ou relação terminada: não é falha técnica, tentar de novo não ajudará.
    if (isApiProblem(query.error) && query.error.code === 'portal_profile_not_available')
      return (
        <EmptyState
          icon={UserX}
          title="Portal indisponível"
          description="A tua ficha não está ativa. Fala com o teu personal trainer."
        />
      );

    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  }

  const home = query.data;

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">{longLocalDate(home.local_date)}</p>
        <h1 className="font-display text-3xl leading-none">Hoje</h1>
      </header>

      <div className="grid gap-4 sm:grid-cols-2">
        <WorkoutCard workout={home.workout} />
        <NutritionCard nutrition={home.nutrition} />
        <SupplementsCard supplements={home.supplements} />
        <CheckInCard checkIn={home.next_check_in} />
      </div>
    </section>
  );
}

function WorkoutCard({ workout }: { workout: HomeWorkout | null }) {
  if (workout === null)
    return (
      <HomeCard title="O meu treino de hoje" icon={Dumbbell}>
        <p className="text-sm">O teu personal trainer ainda não atribuiu um plano de treino.</p>
      </HomeCard>
    );

  const next = workout.next_workout;
  const nextLine =
    next === null ? null : (
      <p className="text-muted-foreground text-sm">
        Próximo treino: {shortLocalDate(next.date)} ·{' '}
        {planDayTitle(next.week_number, next.day_of_week)}
      </p>
    );

  if (workout.status !== 'workout')
    return (
      <HomeCard
        title="O meu treino de hoje"
        icon={Dumbbell}
        action={{ label: 'Ver treino', to: '/portal/today' }}
      >
        <p className="text-sm">
          {workout.status === 'rest'
            ? 'Hoje é dia de descanso. Aproveita para descansar e relaxar.'
            : 'Hoje está fora do período do teu plano.'}
        </p>
        {nextLine}
      </HomeCard>
    );

  const percent =
    workout.planned_sets === 0 ? 0 : Math.round((workout.logged_sets / workout.planned_sets) * 100);

  return (
    <HomeCard
      title="O meu treino de hoje"
      icon={Dumbbell}
      action={{
        label: workout.is_completed ? 'Ver treino' : 'Abrir treino',
        to: '/portal/today',
      }}
    >
      {workout.week_number !== null && workout.day_of_week !== null && (
        <p className="font-display text-xl leading-tight">
          {planDayTitle(workout.week_number, workout.day_of_week)}
        </p>
      )}
      {workout.day_notes !== null && (
        <p className="text-muted-foreground text-sm">{workout.day_notes}</p>
      )}
      <p className="text-sm">
        {count(workout.exercise_count, 'exercício', 'exercícios')} ·{' '}
        {count(workout.planned_sets, 'série', 'séries')}
      </p>
      <div className="space-y-1 pt-1">
        <Progress value={percent} aria-label="Séries registadas" />
        <p className="text-muted-foreground font-mono text-xs tabular-nums">
          {workout.is_completed
            ? `Treino concluído · ${formatNumber(workout.logged_sets)} de ${formatNumber(workout.planned_sets)} séries`
            : `${formatNumber(workout.logged_sets)} de ${formatNumber(workout.planned_sets)} séries · ${formatNumber(percent)} %`}
        </p>
      </div>
    </HomeCard>
  );
}

function NutritionCard({ nutrition }: { nutrition: PortalHome['nutrition'] }) {
  if (nutrition === null)
    return (
      <HomeCard title="Plano alimentar" icon={UtensilsCrossed}>
        <p className="text-sm">O teu personal trainer ainda não te atribuiu um plano alimentar.</p>
      </HomeCard>
    );

  return (
    <HomeCard
      title="Plano alimentar"
      icon={UtensilsCrossed}
      action={{ label: 'Ver plano', to: '/portal/nutrition' }}
    >
      <p className="font-display text-xl leading-tight">{nutrition.name}</p>
      <p className="text-sm tabular-nums">
        {formatNumber(nutrition.target_kcal)} kcal ·{' '}
        {count(nutrition.meal_count, 'refeição', 'refeições')}
      </p>
    </HomeCard>
  );
}

/**
 * Tomas de hoje: "X de Y tomadas" e quantas faltam. Não existe "em atraso":
 * a toma não tem hora, só dia.
 */
function SupplementsCard({ supplements }: { supplements: PortalHome['supplements'] }) {
  if (supplements.total_count === 0)
    return (
      <HomeCard title="Suplementos" icon={Pill}>
        <p className="text-sm">Não tens suplementos atribuídos.</p>
      </HomeCard>
    );

  const remaining = supplements.total_count - supplements.taken_count;

  return (
    <HomeCard
      title="Suplementos"
      icon={Pill}
      action={{ label: 'Registar tomas', to: '/portal/supplements' }}
    >
      <p className="font-display text-xl leading-tight tabular-nums">
        {formatNumber(supplements.taken_count)} de {formatNumber(supplements.total_count)} tomadas
        hoje
      </p>
      <p className="text-muted-foreground text-sm">
        {remaining === 0 ? 'Tudo tomado.' : `${formatNumber(remaining)} por tomar.`}
      </p>
    </HomeCard>
  );
}

/**
 * Check-in: o cliente só pode responder no próprio dia (`check_in_wrong_day`), por isso o
 * cartão só oferece "Responder" quando `is_today`. Um check-in futuro mostra a data, sem
 * "responder até": a data-alvo não é prazo.
 */
function CheckInCard({ checkIn }: { checkIn: PortalHome['next_check_in'] }) {
  if (checkIn === null)
    return (
      <HomeCard title="Check-in" icon={ClipboardCheck}>
        <p className="text-sm">Não tens check-ins agendados.</p>
      </HomeCard>
    );

  if (checkIn.is_today)
    return (
      <HomeCard
        title="Check-in"
        icon={ClipboardCheck}
        action={{ label: 'Responder', to: '/portal/check-ins' }}
      >
        <p className="font-display text-xl leading-tight">Tens um check-in para hoje.</p>
        <p className="text-muted-foreground text-sm">Só podes responder hoje.</p>
      </HomeCard>
    );

  return (
    <HomeCard title="Check-in" icon={ClipboardCheck}>
      <p className="text-sm">Próximo check-in: {shortLocalDate(checkIn.check_in_date)}.</p>
      <p className="text-muted-foreground text-sm">Poderás responder nesse dia.</p>
    </HomeCard>
  );
}
