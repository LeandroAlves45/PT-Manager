import { CalendarCheck, CheckCircle2, ClipboardCheck, TriangleAlert } from 'lucide-react';
import { useState } from 'react';

import { usePortalDueCheckInQuery, usePortalNextCheckInQuery } from '@/features/portal/api/portal';
import { CheckInForm } from '@/features/portal/components/CheckInForm';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { longLocalDate } from '@/features/portal/lib/schedule';
import { ErrorState } from '@/shared/components/ErrorState';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Avaliação periódica do cliente (`/portal/check-ins`): responder à de hoje, ou saber quando
 * é a próxima. A rota e a API mantêm o nome «check-in»; só o texto visível mudou.
 *
 * Orçamento: um pedido quando há avaliação por responder (`GET /portal/my-check-ins/due`); dois
 * quando não há (mais `GET /portal/my-check-ins/next`). O 404 `resource_not_found` dos dois é
 * "não há", não um erro, e já chega aqui como `null`.
 *
 * Se a avaliação deixa de estar por responder enquanto o cliente a preenchia (o dia passou, ou
 * o personal trainer a cancelou), o ecrã diz-lhe isso em vez de a fazer desaparecer sem
 * explicação.
 *
 * Não existe histórico: o backend não devolve ao cliente as avaliações respondidas. Depois de
 * enviar, o ecrã confirma e mostra a próxima, sem repetir os valores.
 */
export function PortalCheckInsPage() {
  const due = usePortalDueCheckInQuery();
  const [sent, setSent] = useState(false);
  // O formulário avisa quando tem algo escrito; o valor sobrevive à saída do formulário.
  const [draftDirty, setDraftDirty] = useState(false);
  const checkIn = due.data ?? null;
  const next = usePortalNextCheckInQuery(due.data !== undefined && (checkIn === null || sent));

  if (due.isPending)
    return (
      <section className="mx-auto w-full max-w-xl space-y-4">
        <Skeleton className="h-10 w-40" />
        <div role="status" aria-label="A carregar a avaliação…" className="space-y-3">
          <Skeleton className="h-16" />
          <Skeleton className="h-48" />
        </div>
      </section>
    );

  if (due.data === undefined)
    return <ErrorState error={due.error} onRetry={() => void due.refetch()} titleAs="h1" />;

  const unavailable = checkIn === null && !sent && draftDirty;

  return (
    <section className="mx-auto w-full max-w-xl space-y-4">
      <header className="space-y-1">
        {checkIn !== null && !sent && (
          <p className="text-muted-foreground font-mono text-xs">
            {longLocalDate(checkIn.check_in_date)}
          </p>
        )}
        <h1 className="font-display pe-1 text-3xl leading-none">Avaliação periódica</h1>
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
          Avaliação enviada. O teu personal trainer já a pode ver.
        </p>
      )}

      {unavailable && (
        <div
          role="alert"
          className="border-warning bg-card flex items-start gap-2 rounded-lg border p-3 text-sm"
        >
          <TriangleAlert aria-hidden className="text-warning mt-0.5 size-5 shrink-0" />
          <p>
            Esta avaliação já não está disponível para responder. O dia pode ter passado. O que
            escreveste não foi enviado.
          </p>
        </div>
      )}

      {checkIn !== null && !sent ? (
        <PortalCard tone="assessment">
          <p className="pb-2 text-sm">Só podes responder hoje.</p>
          <CheckInForm
            checkInId={checkIn.id}
            onSent={() => setSent(true)}
            onDirtyChange={setDraftDirty}
          />
        </PortalCard>
      ) : next.isLoading ? (
        <div role="status" aria-label="A carregar a próxima avaliação…">
          <Skeleton className="h-24" />
        </div>
      ) : next.isError ? (
        <div
          role="status"
          className="border-border bg-card flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-sm"
        >
          <p>Não foi possível saber quando é a tua próxima avaliação.</p>
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
        <PortalCard tone="assessment" icon={ClipboardCheck} title="Sem avaliações agendadas">
          <p className="text-muted-foreground text-sm">
            Quando o teu personal trainer agendar uma avaliação, aparece aqui.
          </p>
        </PortalCard>
      ) : (
        <PortalCard tone="assessment" icon={CalendarCheck} title="Próxima avaliação">
          <p className="text-sm">
            {longLocalDate(next.data.check_in_date)}. Poderás responder nesse dia.
          </p>
        </PortalCard>
      )}
    </section>
  );
}
