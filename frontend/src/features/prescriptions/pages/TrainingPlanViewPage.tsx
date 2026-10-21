import { ArrowLeft, Dumbbell, Pencil } from 'lucide-react';
import { Link, useParams, useSearchParams } from 'react-router';

import { useFixedClientQuery, useTrainingPlanQuery } from '@/features/prescriptions/api/training';
import { PerformedSetsSection } from '@/features/prescriptions/components/PerformedSetsSection';
import {
  plannedSetText,
  WEEKDAYS,
  type TrainingDetails,
} from '@/features/prescriptions/lib/training-draft';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate } from '@/shared/lib/format';

function statusOf(plan: TrainingDetails): string {
  if (plan.is_archived) return 'Arquivado';
  return plan.is_active ? 'Ativo' : 'Inativo';
}

/**
 * Página de um plano de treino (`/trainer/training-plans/:planId`), só de consulta.
 *
 * Mostra cabeçalho, datas, estado e o resumo da estrutura por dia com as séries planeadas;
 * «Editar detalhes» e «Editar estrutura» abrem os dois passos do editor. A secção «Séries
 * realizadas» lê o que o cliente fez, com o planeado ao lado. Plano arquivado: sem edição nem
 * correção. Com `?client_id=` (aberto a partir da ficha) o regresso é à ficha.
 */
export function TrainingPlanViewPage() {
  const { planId = '' } = useParams();
  const [searchParams] = useSearchParams();
  const queryClientId = searchParams.get('client_id');
  const clientSearch =
    queryClientId === null ? '' : `?client_id=${encodeURIComponent(queryClientId)}`;
  const plan = useTrainingPlanQuery(planId);
  const client = useFixedClientQuery(plan.data?.client_id ?? null);
  const backTo =
    queryClientId === null
      ? '/trainer/training-plans'
      : `/trainer/clients/${queryClientId}?tab=training`;

  const back = (
    <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
      <Link to={backTo}>
        <ArrowLeft aria-hidden />{' '}
        {queryClientId === null ? 'Voltar a planos de treino' : 'Voltar à ficha do cliente'}
      </Link>
    </Button>
  );

  if (plan.isPending)
    return (
      <section className="space-y-6">
        {back}
        <div role="status" aria-label="A carregar plano de treino…" className="space-y-3">
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-64 w-full" />
        </div>
      </section>
    );

  if (plan.isError)
    return (
      <section className="space-y-6">
        {back}
        {isApiProblem(plan.error) && plan.error.status === 404 ? (
          <EmptyState
            icon={Dumbbell}
            title="Plano não encontrado"
            description="Este plano já não existe."
          />
        ) : (
          <ErrorState error={plan.error} onRetry={() => void plan.refetch()} />
        )}
      </section>
    );

  const data = plan.data;
  const days = [...data.days].sort(
    (a, b) => a.week_number - b.week_number || a.day_of_week - b.day_of_week
  );

  return (
    <section className="space-y-8">
      {back}
      <PageHeader
        title={data.name}
        description={client.data?.name}
        action={
          !data.is_archived && (
            <div className="flex flex-wrap gap-2">
              <Button asChild variant="outline" className="min-h-11 md:min-h-9">
                <Link to={`/trainer/training-plans/${data.id}/edit${clientSearch}`}>
                  <Pencil aria-hidden /> Editar detalhes
                </Link>
              </Button>
              <Button asChild className="min-h-11 md:min-h-9">
                <Link to={`/trainer/training-plans/${data.id}/edit/structure${clientSearch}`}>
                  Editar estrutura
                </Link>
              </Button>
            </div>
          )
        }
      />

      <div className="border-border space-y-3 rounded-xl border p-4 sm:p-5">
        <div className="flex flex-wrap items-center gap-2">
          <Badge
            variant={data.is_archived ? 'outline' : 'secondary'}
            className={data.is_archived ? '' : 'bg-primary/10 text-primary'}
          >
            {statusOf(data)}
          </Badge>
          {data.training_modality !== null && data.training_modality !== '' && (
            <Badge variant="outline">{data.training_modality}</Badge>
          )}
        </div>
        <p className="text-sm">
          Início: {formatDate(data.start_date)}
          {data.end_date !== null && ` · Fim: ${formatDate(data.end_date)}`}
        </p>
        {data.description !== null && data.description !== '' && (
          <p className="whitespace-pre-line">{data.description}</p>
        )}
        {data.notes !== null && data.notes !== '' && (
          <p className="text-muted-foreground text-sm whitespace-pre-line">Notas: {data.notes}</p>
        )}
      </div>

      <section aria-labelledby="plan-structure" className="space-y-4">
        <h2 id="plan-structure" className="font-display text-xl">
          Estrutura
        </h2>
        {days.length === 0 ? (
          <p className="text-muted-foreground">Este plano ainda não tem dias.</p>
        ) : (
          <div className="grid gap-4 lg:grid-cols-2">
            {days.map((day) => (
              <article
                key={day.id}
                className="border-border border-l-primary space-y-3 rounded-xl border border-l-4 p-4"
              >
                <h3 className="font-medium">
                  Semana {day.week_number} · {WEEKDAYS[day.day_of_week] ?? ''}
                </h3>
                {day.notes !== null && day.notes !== '' && (
                  <p className="text-muted-foreground text-sm">{day.notes}</p>
                )}
                {day.exercises.length === 0 && (
                  <p className="text-muted-foreground text-sm">Sem exercícios.</p>
                )}
                {day.exercises.map((exercise) => (
                  <div key={exercise.id} className="space-y-1">
                    <p className="text-sm font-medium">
                      {exercise.exercise_name}
                      {exercise.exercise_group_id !== null && (
                        <Badge variant="outline" className="ml-2">
                          Supersérie
                        </Badge>
                      )}
                    </p>
                    <ul className="text-muted-foreground space-y-0.5 text-sm">
                      {exercise.sets.map((set) => (
                        <li key={set.id}>
                          Série {set.set_number} · {plannedSetText(set)}
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </article>
            ))}
          </div>
        )}
      </section>

      <PerformedSetsSection plan={data} clientSearch={clientSearch} />
    </section>
  );
}
