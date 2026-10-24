import { ClipboardCheck, Dumbbell, Pill, UtensilsCrossed } from 'lucide-react';

import { usePortalHomeQuery } from '@/features/portal/api/portal';
import { AccessEndNotice } from '@/features/portal/components/AccessEndNotice';
import { InitialAssessmentCard } from '@/features/portal/components/InitialAssessmentCard';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { PortalUnavailable } from '@/features/portal/components/PortalUnavailable';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { longLocalDate, planDayTitle, shortLocalDate } from '@/features/portal/lib/schedule';
import { progressPercent } from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { isApiProblem } from '@/shared/api/problem';
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
 * Início do portal: quatro cartões a partir de `GET /portal/home`, precedidos do aviso de fim de
 * acesso (quando existe) e do cartão da avaliação inicial (enquanto ela falta).
 *
 * Cada cartão tem o seu estado vazio, porque o backend devolve `null` por bloco (sem plano
 * de treino, sem plano alimentar escolhido, sem avaliação), e um só pedido alimenta o ecrã
 * todo. Cada cartão tem a cor da sua secção. A
 * data de referência é sempre `local_date` (fuso do personal trainer), nunca o relógio do browser.
 */
export function PortalHomePage() {
  const query = usePortalHomeQuery();

  if (query.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-40" />
        <div
          role="status"
          aria-label="A carregar o início…"
          className="grid grid-cols-1 gap-4 md:grid-cols-2"
        >
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      </section>
    );

  // Ficha arquivada ou relação terminada: não é falha técnica, tentar de novo não ajudará.
  // Vale mesmo com dados em cache.
  if (isApiProblem(query.error) && query.error.code === 'portal_profile_not_available')
    return <PortalUnavailable />;

  if (query.data === undefined)
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} titleAs="h1" />;

  const home = query.data;

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">{longLocalDate(home.local_date)}</p>
        <h1 className="font-display pe-1 text-3xl leading-none">Hoje</h1>
      </header>

      {query.isError && (
        <StaleDataNotice onRetry={() => void query.refetch()} retrying={query.isFetching} />
      )}

      {home.access_notice !== null && <AccessEndNotice notice={home.access_notice} />}
      {home.initial_assessment_pending && <InitialAssessmentCard />}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <WorkoutCard workout={home.workout} />
        <NutritionCard
          nutrition={home.nutrition}
          activeMealPlanCount={home.active_meal_plan_count}
        />
        <SupplementsCard supplements={home.supplements} />
        <CheckInCard checkIn={home.next_check_in} />
      </div>
    </section>
  );
}

function WorkoutCard({ workout }: { workout: HomeWorkout | null }) {
  if (workout === null)
    return (
      <PortalCard title="O meu treino de hoje" icon={Dumbbell} tone="training">
        <p className="text-sm">O teu personal trainer ainda não atribuiu um plano de treino.</p>
      </PortalCard>
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
      <PortalCard
        title="O meu treino de hoje"
        icon={Dumbbell}
        tone="training"
        action={{ label: 'Ver treino', to: '/portal/today' }}
      >
        <p className="text-sm">
          {workout.status === 'rest'
            ? 'Hoje é dia de descanso. Aproveita para descansar e relaxar.'
            : 'Hoje está fora do período do teu plano.'}
        </p>
        {nextLine}
      </PortalCard>
    );

  const percent = progressPercent(workout.logged_sets, workout.planned_sets);

  return (
    <PortalCard
      title="O meu treino de hoje"
      icon={Dumbbell}
      tone="training"
      action={{
        label: workout.is_completed ? 'Ver treino' : 'Abrir treino',
        to: '/portal/today',
      }}
    >
      {workout.week_number !== null && workout.day_of_week !== null && (
        <p className="font-display pe-1 text-xl leading-tight">
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
        <Progress
          value={percent}
          aria-label="Séries registadas"
          aria-valuetext={`${formatNumber(workout.logged_sets)} de ${formatNumber(workout.planned_sets)} séries`}
        />
        <p className="text-muted-foreground font-mono text-xs tabular-nums">
          {workout.is_completed
            ? `Treino concluído · ${formatNumber(workout.logged_sets)} de ${formatNumber(workout.planned_sets)} séries`
            : `${formatNumber(workout.logged_sets)} de ${formatNumber(workout.planned_sets)} séries · ${formatNumber(percent)} %`}
        </p>
      </div>
    </PortalCard>
  );
}

/**
 * Plano alimentar escolhido. Sem escolha mas com planos ativos, convida a escolher: a app nunca
 * escolhe pelo cliente, mesmo que só haja um plano.
 */
function NutritionCard({
  nutrition,
  activeMealPlanCount,
}: {
  nutrition: PortalHome['nutrition'];
  activeMealPlanCount: number;
}) {
  if (nutrition === null && activeMealPlanCount > 0)
    return (
      <PortalCard
        title="Plano alimentar"
        icon={UtensilsCrossed}
        tone="nutrition"
        action={{ label: 'Escolher plano', to: '/portal/nutrition/planos' }}
      >
        <p className="font-display pe-1 text-xl leading-tight">Escolhe o teu plano</p>
        <p className="text-muted-foreground text-sm">
          {activeMealPlanCount === 1
            ? 'O teu personal trainer deu-te um plano alimentar.'
            : `O teu personal trainer deu-te ${formatNumber(activeMealPlanCount)} planos alimentares.`}
        </p>
      </PortalCard>
    );

  if (nutrition === null)
    return (
      <PortalCard title="Plano alimentar" icon={UtensilsCrossed} tone="nutrition">
        <p className="text-sm">O teu personal trainer ainda não te atribuiu um plano alimentar.</p>
      </PortalCard>
    );

  return (
    <PortalCard
      title="Plano alimentar"
      icon={UtensilsCrossed}
      tone="nutrition"
      action={{ label: 'Ver plano', to: '/portal/nutrition' }}
    >
      <p className="font-display pe-1 text-xl leading-tight">{nutrition.name}</p>
      <p className="text-sm tabular-nums">
        {formatNumber(nutrition.target_kcal)} kcal ·{' '}
        {count(nutrition.meal_count, 'refeição', 'refeições')}
      </p>
    </PortalCard>
  );
}

/**
 * Tomas de hoje: "X de Y tomadas" e quantas faltam. Não existe "em atraso":
 * a toma não tem hora, só dia.
 */
function SupplementsCard({ supplements }: { supplements: PortalHome['supplements'] }) {
  if (supplements.total_count === 0)
    return (
      <PortalCard title="Suplementos" icon={Pill} tone="supplements">
        <p className="text-sm">Não tens suplementos atribuídos.</p>
      </PortalCard>
    );

  const remaining = supplements.total_count - supplements.taken_count;

  return (
    <PortalCard
      title="Suplementos"
      icon={Pill}
      tone="supplements"
      action={{ label: 'Registar tomas', to: '/portal/supplements' }}
    >
      <p className="font-display pe-1 text-xl leading-tight tabular-nums">
        {formatNumber(supplements.taken_count)} de {formatNumber(supplements.total_count)} tomadas
        hoje
      </p>
      <p className="text-muted-foreground text-sm">
        {remaining === 0 ? 'Tudo tomado.' : `${formatNumber(remaining)} por tomar.`}
      </p>
    </PortalCard>
  );
}

/**
 * Avaliação periódica: o cliente só pode responder no próprio dia (`check_in_wrong_day`), por
 * isso o cartão só oferece "Responder" quando `is_today`. Uma avaliação futura mostra a data,
 * sem "responder até": a data-alvo não é prazo.
 */
function CheckInCard({ checkIn }: { checkIn: PortalHome['next_check_in'] }) {
  if (checkIn === null)
    return (
      <PortalCard title="Avaliação periódica" icon={ClipboardCheck} tone="assessment">
        <p className="text-sm">Não tens avaliações agendadas.</p>
      </PortalCard>
    );

  if (checkIn.is_today)
    return (
      <PortalCard
        title="Avaliação periódica"
        icon={ClipboardCheck}
        tone="assessment"
        action={{ label: 'Responder', to: '/portal/check-ins' }}
      >
        <p className="font-display pe-1 text-xl leading-tight">Tens uma avaliação para hoje.</p>
        <p className="text-muted-foreground text-sm">Só podes responder hoje.</p>
      </PortalCard>
    );

  return (
    <PortalCard title="Avaliação periódica" icon={ClipboardCheck} tone="assessment">
      <p className="text-sm">Próxima avaliação: {shortLocalDate(checkIn.check_in_date)}.</p>
      <p className="text-muted-foreground text-sm">Poderás responder nesse dia.</p>
    </PortalCard>
  );
}
