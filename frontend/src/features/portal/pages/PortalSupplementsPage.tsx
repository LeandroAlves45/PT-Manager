import { Pill } from 'lucide-react';

import {
  usePortalSupplementIntakesQuery,
  usePortalSupplementsQuery,
} from '@/features/portal/api/portal';
import { PortalUnavailable } from '@/features/portal/components/PortalUnavailable';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { SupplementIntakeRow } from '@/features/portal/components/SupplementIntakeRow';
import { longLocalDate } from '@/features/portal/lib/schedule';
import { progressPercent } from '@/features/portal/lib/workout';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Button } from '@/shared/components/ui/button';
import { Progress } from '@/shared/components/ui/progress';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatNumber } from '@/shared/lib/format';

/**
 * Suplementos do cliente (`/portal/supplements`): tomas de hoje num só ecrã.
 *
 * Orçamento: dois pedidos ao abrir, nenhum por linha.
 * - `GET /portal/my-supplements/intakes/today` manda no ecrã: lista, contagem e "hoje"
 *   (`local_date`, fuso do personal trainer).
 * - `GET /portal/my-supplements` (uma página de 100) só acrescenta as notas do personal
 *   trainer e a descrição; se falhar, as tomas continuam a poder ser registadas.
 *
 * Não existe "em atraso": a toma tem dia, não tem hora.
 */
export function PortalSupplementsPage() {
  const intakes = usePortalSupplementIntakesQuery();
  const details = usePortalSupplementsQuery();

  if (intakes.isPending)
    return (
      <section className="space-y-4">
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
    return <ErrorState error={intakes.error} onRetry={() => void intakes.refetch()} />;

  const today = intakes.data;
  const detailById = new Map((details.data?.items ?? []).map((detail) => [detail.id, detail]));
  const remaining = today.total_count - today.taken_count;

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        <p className="text-muted-foreground font-mono text-xs">{longLocalDate(today.local_date)}</p>
        <h1 className="font-display text-3xl leading-none">Suplementos</h1>
      </header>

      {intakes.isError && (
        <StaleDataNotice onRetry={() => void intakes.refetch()} retrying={intakes.isFetching} />
      )}

      {today.total_count === 0 ? (
        <EmptyState
          icon={Pill}
          title="Sem suplementos"
          description="Não tens suplementos atribuídos."
        />
      ) : (
        <>
          <div className="border-border bg-card space-y-2 rounded-lg border p-3">
            <p className="font-display text-xl leading-tight tabular-nums">
              {formatNumber(today.taken_count)} de {formatNumber(today.total_count)} tomadas hoje
            </p>
            <Progress
              value={progressPercent(today.taken_count, today.total_count)}
              aria-label="Tomas de hoje"
            />
            <p className="text-muted-foreground text-sm">
              {remaining === 0 ? 'Tudo tomado.' : `${formatNumber(remaining)} por tomar.`}
            </p>
          </div>

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
  );
}
