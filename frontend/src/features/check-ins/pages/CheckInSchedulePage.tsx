import { ArrowLeft, ClipboardCheck } from 'lucide-react';
import { useCallback } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { useCheckInQuery } from '@/features/check-ins/api/queries';
import { ScheduleCheckInForm } from '@/features/check-ins/components/ScheduleCheckInForm';
import { isOpenFuture } from '@/features/check-ins/lib/checkInStatus';
import { todayKey } from '@/features/check-ins/lib/dates';
import { useFixedClientQuery } from '@/features/packs';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Agendar ou reagendar uma avaliação periódica (antes «check-in») numa página própria, sem
 * painel lateral.
 *
 * - `/trainer/check-ins/new`: agendar. `?client_id=` (aberta no separador «Avaliações» da ficha)
 *   fixa o cliente; sem ele escolhe-se na pesquisa.
 * - `/trainer/check-ins/:checkInId/reschedule`: reagendar. Lê a avaliação com
 *   `GET /check-ins/{id}`; só avaliações agendadas e futuras se reagendam.
 *
 * Depois de guardar ou «Voltar»: com `?client_id=` regressa a `/trainer/clients/:id?tab=check-ins`,
 * sem ele à lista de avaliações.
 */
export function CheckInSchedulePage() {
  const { checkInId } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const clientId = params.get('client_id');
  const backPath =
    clientId === null ? '/trainer/check-ins' : `/trainer/clients/${clientId}?tab=check-ins`;
  const onSaved = useCallback(
    () => void navigate(backPath, { replace: true }),
    [navigate, backPath]
  );

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={backPath}>
          <ArrowLeft aria-hidden />{' '}
          {clientId === null ? 'Voltar a Avaliações' : 'Voltar ao cliente'}
        </Link>
      </Button>
      {checkInId === undefined ? (
        <ScheduleNew clientId={clientId} onSaved={onSaved} />
      ) : (
        <RescheduleExisting checkInId={checkInId} onSaved={onSaved} />
      )}
    </section>
  );
}

function ScheduleNew({ clientId, onSaved }: { clientId: string | null; onSaved: () => void }) {
  const client = useFixedClientQuery(clientId);

  return (
    <>
      <PageHeader
        title="Agendar avaliação periódica"
        description={
          clientId === null || client.data === undefined
            ? 'Escolhe o cliente e o dia.'
            : `Com ${client.data.name}. Escolhe o dia da avaliação.`
        }
      />
      {clientId === null ? (
        <div className="max-w-xl">
          <ScheduleCheckInForm client={null} onSaved={onSaved} />
        </div>
      ) : client.isError ? (
        <ErrorState error={client.error} onRetry={() => void client.refetch()} />
      ) : client.data === undefined ? (
        <Skeleton aria-label="A carregar cliente…" role="status" className="h-48 w-full max-w-xl" />
      ) : (
        <div className="max-w-xl">
          <ScheduleCheckInForm
            client={{ id: client.data.id, name: client.data.name }}
            onSaved={onSaved}
          />
        </div>
      )}
    </>
  );
}

function RescheduleExisting({ checkInId, onSaved }: { checkInId: string; onSaved: () => void }) {
  const query = useCheckInQuery(checkInId);

  if (query.isError)
    return isApiProblem(query.error) && query.error.status === 404 ? (
      <EmptyState
        icon={ClipboardCheck}
        title="Avaliação não encontrada"
        description="Esta avaliação já não existe."
      />
    ) : (
      <ErrorState error={query.error} onRetry={() => void query.refetch()} />
    );

  const checkIn = query.data;
  if (checkIn === undefined)
    return (
      <div aria-label="A carregar…" role="status" className="space-y-3">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-48 w-full max-w-xl" />
      </div>
    );

  if (!isOpenFuture(checkIn, todayKey()))
    return (
      <EmptyState
        icon={ClipboardCheck}
        title="Esta avaliação já não pode ser reagendada"
        description="Só é possível reagendar avaliações agendadas, antes do dia marcado."
      />
    );

  return (
    <>
      <PageHeader
        title="Reagendar avaliação periódica"
        description={`Com ${checkIn.client_name}. Escolhe o dia da avaliação.`}
      />
      <div className="max-w-xl">
        <ScheduleCheckInForm client={null} checkIn={checkIn} onSaved={onSaved} />
      </div>
    </>
  );
}
