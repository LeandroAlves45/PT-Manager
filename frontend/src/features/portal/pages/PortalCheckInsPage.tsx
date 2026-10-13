import { CalendarCheck, CheckCircle2, ClipboardCheck } from 'lucide-react';
import { useState } from 'react';

import { usePortalDueCheckInQuery, usePortalNextCheckInQuery } from '@/features/portal/api/portal';
import { CheckInForm } from '@/features/portal/components/CheckInForm';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { longLocalDate } from '@/features/portal/lib/schedule';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Check-ins do cliente (`/portal/check-ins`): responder ao de hoje, ou saber quando é o próximo.
 *
 * Orçamento: um pedido quando há check-in por responder (`GET /portal/my-check-ins/due`); dois
 * quando não há (mais `GET /portal/my-check-ins/next`). O 404 `resource_not_found` dos dois é
 * "não há", não um erro, e já chega aqui como `null`.
 *
 * Não existe histórico: o backend não devolve ao cliente os check-ins respondidos. Depois de
 * enviar, o ecrã confirma e mostra o próximo, sem repetir os valores.
 */
export function PortalCheckInsPage() {
  const due = usePortalDueCheckInQuery();
  const [sent, setSent] = useState(false);
  const checkIn = due.data ?? null;
  const next = usePortalNextCheckInQuery(due.data !== undefined && (checkIn === null || sent));

  if (due.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-40" />
        <div role="status" aria-label="A carregar o check-in…" className="space-y-3">
          <Skeleton className="h-16" />
          <Skeleton className="h-48" />
        </div>
      </section>
    );

  if (due.data === undefined)
    return <ErrorState error={due.error} onRetry={() => void due.refetch()} />;

  return (
    <section className="space-y-4">
      <header className="space-y-1">
        {checkIn !== null && !sent && (
          <p className="text-muted-foreground font-mono text-xs">
            {longLocalDate(checkIn.check_in_date)}
          </p>
        )}
        <h1 className="font-display text-3xl leading-none">Check-ins</h1>
      </header>

      {due.isError && (
        <StaleDataNotice onRetry={() => void due.refetch()} retrying={due.isFetching} />
      )}

      {sent && (
        <p
          role="status"
          className="border-border bg-card flex items-center gap-2 rounded-lg border p-3 text-sm"
        >
          <CheckCircle2 aria-hidden className="text-primary size-5 shrink-0" />
          Check-in enviado. O teu personal trainer já o pode ver.
        </p>
      )}

      {checkIn !== null && !sent ? (
        <>
          <p className="text-sm">Só podes responder hoje.</p>
          <CheckInForm checkInId={checkIn.id} onSent={() => setSent(true)} />
        </>
      ) : next.isLoading ? (
        <div role="status" aria-label="A carregar o próximo check-in…">
          <Skeleton className="h-24" />
        </div>
      ) : next.isError ? (
        <div
          role="status"
          className="border-border bg-card flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-sm"
        >
          <p>Não foi possível saber quando é o teu próximo check-in.</p>
          <Button
            type="button"
            variant="outline"
            className="min-h-11"
            onClick={() => void next.refetch()}
          >
            Tentar novamente
          </Button>
        </div>
      ) : next.data == null ? (
        <EmptyState
          icon={ClipboardCheck}
          title="Sem check-ins agendados"
          description="Quando o teu personal trainer agendar um check-in, aparece aqui."
        />
      ) : (
        <EmptyState
          icon={CalendarCheck}
          title="Próximo check-in"
          description={`${longLocalDate(next.data.check_in_date)}. Poderás responder nesse dia.`}
        />
      )}
    </section>
  );
}
