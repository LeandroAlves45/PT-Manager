import { Pill, UtensilsCrossed } from 'lucide-react';
import { useEffect } from 'react';
import { useLocation } from 'react-router';

import { WithInitialAssessmentNotice } from '@/features/portal/components/InitialAssessmentMissingNotice';
import {
  usePortalPlanSupplementsQuery,
  usePortalSupplementIntakesQuery,
  usePortalSupplementsQuery,
} from '@/features/portal/api/portal';
import { PlanSupplementRow } from '@/features/portal/components/PlanSupplementRow';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { PortalUnavailable } from '@/features/portal/components/PortalUnavailable';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { SupplementIntakeRow } from '@/features/portal/components/SupplementIntakeRow';
import { longLocalDate } from '@/features/portal/lib/schedule';
import { progressPercent } from '@/features/portal/lib/workout';
import { isApiProblem } from '@/shared/api/problem';
import { ErrorState } from '@/shared/components/ErrorState';
import { Button } from '@/shared/components/ui/button';
import { Progress } from '@/shared/components/ui/progress';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatNumber } from '@/shared/lib/format';

/**
 * Secção «Do teu plano alimentar»: suplementos das refeições e de fora delas, com a toma de hoje.
 *
 * Estados pelo código do 404 de `GET /portal/my-plan-supplements/today`:
 * - `portal_nutrition_plan_not_selected`: sem plano escolhido não há tomas, só o convite a
 *   escolher (a app nunca escolhe sozinha);
 * - `portal_nutrition_plan_not_available`: não há plano ativo, nada para tomar.
 * Qualquer outra falha mostra uma frase com «Tentar novamente»; as tomas diretas do personal
 * trainer continuam a funcionar.
 */
function PlanSupplementsSection() {
  const query = usePortalPlanSupplementsQuery();
  const errorCode = isApiProblem(query.error) ? query.error.code : null;

  let body;
  if (query.isPending) {
    body = (
      <Skeleton role="status" aria-label="A carregar os suplementos do plano…" className="h-24" />
    );
  } else if (errorCode === 'portal_nutrition_plan_not_selected') {
    body = (
      <PortalCard
        tone="nutrition"
        icon={UtensilsCrossed}
        title="Escolhe o teu plano"
        action={{ label: 'Escolher plano alimentar', to: '/portal/nutrition' }}
      >
        <p className="text-muted-foreground text-sm">
          Os suplementos do plano alimentar só aparecem depois de escolheres o plano que queres
          seguir.
        </p>
      </PortalCard>
    );
  } else if (errorCode === 'portal_nutrition_plan_not_available') {
    body = <p className="text-muted-foreground text-sm">Não tens um plano alimentar ativo.</p>;
  } else if (query.data === undefined) {
    body = (
      <div
        role="status"
        className="border-border bg-card flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-sm"
      >
        <p>Não foi possível carregar os suplementos do plano alimentar.</p>
        <Button
          type="button"
          variant="outline"
          className="min-h-11"
          onClick={() => void query.refetch()}
        >
          Tentar novamente
        </Button>
      </div>
    );
  } else {
    const items = query.data.items;
    const countable = items.filter((item) => !item.is_unavailable || item.taken_at !== null);
    const taken = countable.filter((item) => item.taken_at !== null).length;

    body =
      items.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          O plano «{query.data.meal_plan_name}» não tem suplementos.
        </p>
      ) : (
        <>
          <p className="text-muted-foreground text-sm tabular-nums">
            {formatNumber(taken)} de {formatNumber(countable.length)} tomadas hoje · plano «
            {query.data.meal_plan_name}»
          </p>
          <ul className="space-y-3">
            {items.map((item) => (
              <PlanSupplementRow key={item.id} item={item} />
            ))}
          </ul>
        </>
      );
  }

  return (
    <section id="plano-alimentar" aria-labelledby="plano-alimentar-title" className="space-y-3">
      <h2 id="plano-alimentar-title" className="font-display pe-1 text-xl leading-tight">
        Do teu plano alimentar
      </h2>
      {body}
    </section>
  );
}

/**
 * Suplementos do cliente (`/portal/supplements`): tomas de hoje em duas secções, «Do teu plano
 * alimentar» e «Do teu personal trainer».
 *
 * Orçamento da secção do personal trainer: dois pedidos ao abrir, nenhum por linha.
 * - `GET /portal/my-supplements/intakes/today` manda no ecrã: lista, contagem e "hoje"
 *   (`local_date`, fuso do personal trainer).
 * - `GET /portal/my-supplements` (uma página de 100) só acrescenta as notas do personal
 *   trainer e a descrição; se falhar, as tomas continuam a poder ser registadas.
 *
 * Não existe "em atraso": a toma tem dia, não tem hora.
 */
function PortalSupplementsPageContent() {
  const { hash } = useLocation();
  const intakes = usePortalSupplementIntakesQuery();
  const details = usePortalSupplementsQuery();

  // O link «Registar tomas» da Alimentação chega aqui com a âncora da secção do plano.
  useEffect(() => {
    if (hash === '#plano-alimentar')
      document.getElementById('plano-alimentar')?.scrollIntoView({ block: 'start' });
  }, [hash, intakes.isPending]);

  if (intakes.isPending)
    return (
      <section className="mx-auto w-full max-w-xl space-y-4">
        <Skeleton className="h-10 w-48" />
        <div role="status" aria-label="A carregar os suplementos…" className="space-y-3">
          <Skeleton className="h-20" />
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
        </div>
      </section>
    );

  // Ficha arquivada ou relação terminada: o backend responde como atribuição inexistente
  // (`ListMyTodaySupplementIntakesHandler`). Tentar de novo não ajudaria, e vale mesmo com
  // dados em cache.
  if (isApiProblem(intakes.error) && intakes.error.code === 'supplement_assignment_not_found')
    return <PortalUnavailable />;

  // Sem dados nenhuns, a falha ocupa o ecrã; com dados em cache, fica só um aviso mais abaixo.
  if (intakes.data === undefined)
    return <ErrorState error={intakes.error} onRetry={() => void intakes.refetch()} titleAs="h1" />;

  const today = intakes.data;
  const detailById = new Map((details.data?.items ?? []).map((detail) => [detail.id, detail]));
  const remaining = today.total_count - today.taken_count;

  return (
    <section className="mx-auto w-full max-w-xl space-y-4">
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">{longLocalDate(today.local_date)}</p>
        <h1 className="font-display pe-1 text-3xl leading-none">Suplementos</h1>
      </header>

      {intakes.isError && (
        <StaleDataNotice onRetry={() => void intakes.refetch()} retrying={intakes.isFetching} />
      )}

      <PlanSupplementsSection />

      <section aria-labelledby="personal-trainer-title" className="space-y-3">
        <h2 id="personal-trainer-title" className="font-display pe-1 text-xl leading-tight">
          Do teu personal trainer
        </h2>
        {today.total_count === 0 ? (
          <PortalCard tone="supplements" icon={Pill} title="Sem suplementos">
            <p className="text-muted-foreground text-sm">
              O teu personal trainer não te atribuiu suplementos diretamente.
            </p>
          </PortalCard>
        ) : (
          <>
            <PortalCard tone="supplements" icon={Pill} title="Tomas de hoje">
              <p className="font-display pe-1 text-xl leading-tight tabular-nums">
                {formatNumber(today.taken_count)} de {formatNumber(today.total_count)} tomadas hoje
              </p>
              <Progress
                value={progressPercent(today.taken_count, today.total_count)}
                aria-label="Tomas de hoje"
              />
              <p className="text-muted-foreground text-sm">
                {remaining === 0 ? 'Tudo tomado.' : `${formatNumber(remaining)} por tomar.`}
              </p>
            </PortalCard>

            {details.isError && (
              <div
                role="status"
                className="border-border bg-card flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-sm"
              >
                <p>Não foi possível carregar as notas do teu personal trainer.</p>
                <Button
                  type="button"
                  variant="outline"
                  className="min-h-11"
                  onClick={() => void details.refetch()}
                >
                  Tentar novamente
                </Button>
              </div>
            )}

            <ul className="space-y-3">
              {today.items.map((item) => (
                <SupplementIntakeRow
                  key={item.assignment_id}
                  item={item}
                  detail={detailById.get(item.assignment_id)}
                />
              ))}
            </ul>
          </>
        )}
      </section>
    </section>
  );
}

/** Suplementos do portal, com o aviso de avaliação inicial em falta acima do conteúdo. */
export function PortalSupplementsPage() {
  return (
    <WithInitialAssessmentNotice>
      <PortalSupplementsPageContent />
    </WithInitialAssessmentNotice>
  );
}
