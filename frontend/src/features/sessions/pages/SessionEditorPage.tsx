import { ArrowLeft, CalendarX } from 'lucide-react';
import { useCallback } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { useFixedClientQuery } from '@/features/packs';
import { useSessionQuery } from '@/features/sessions/api/queries';
import { RescheduleForm } from '@/features/sessions/components/RescheduleForm';
import { SessionForm } from '@/features/sessions/components/SessionForm';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/** Dia pré-preenchido vindo da agenda (`?date=yyyy-MM-dd`); qualquer outro formato ignora-se. */
function parseDate(value: string | null): string | undefined {
  return value !== null && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : undefined;
}

/**
 * Marcar ou reagendar uma sessão numa página própria (sem painel lateral).
 *
 * - `/trainer/sessions/new`: marcar. `?client_id=` (aberta na separador «Sessões» da ficha) fixa
 *   o cliente; `?date=` pré-preenche o dia aberto na agenda.
 * - `/trainer/sessions/:sessionId`: reagendar. Lê a sessão com `GET /sessions/{id}` (a
 *   lista só traz uma página, e a página tem de abrir também por um endereço guardado).
 *
 * Depois de guardar ou «Voltar»: com `?client_id=` regressa a `/trainer/clients/:id?tab=sessions`,
 * sem ele à agenda.
 */
export function SessionEditorPage() {
  const { sessionId } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const clientId = params.get('client_id');
  const backPath =
    clientId === null ? '/trainer/sessions' : `/trainer/clients/${clientId}?tab=sessions`;
  const onSaved = useCallback(
    () => void navigate(backPath, { replace: true }),
    [navigate, backPath]
  );

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={backPath}>
          <ArrowLeft aria-hidden />{' '}
          {clientId === null ? 'Voltar a Sessões e packs' : 'Voltar ao cliente'}
        </Link>
      </Button>
      {sessionId === undefined ? (
        <CreateSession
          clientId={clientId}
          defaultDate={parseDate(params.get('date'))}
          onSaved={onSaved}
        />
      ) : (
        <RescheduleSession sessionId={sessionId} onSaved={onSaved} />
      )}
    </section>
  );
}

function CreateSession({
  clientId,
  defaultDate,
  onSaved,
}: {
  clientId: string | null;
  defaultDate: string | undefined;
  onSaved: () => void;
}) {
  const client = useFixedClientQuery(clientId);

  return (
    <>
      <PageHeader
        title="Marcar sessão"
        description={
          clientId === null
            ? 'Escolhe o cliente, o dia e a hora.'
            : client.data === undefined
              ? 'Escolhe o dia e a hora.'
              : `Com ${client.data.name}. Escolhe o dia e a hora.`
        }
      />
      {clientId === null ? (
        <div className="max-w-xl">
          <SessionForm
            client={null}
            {...(defaultDate === undefined ? {} : { defaultDate })}
            onSaved={onSaved}
          />
        </div>
      ) : client.isError ? (
        <ErrorState error={client.error} onRetry={() => void client.refetch()} />
      ) : client.data === undefined ? (
        <Skeleton aria-label="A carregar cliente…" role="status" className="h-64 w-full max-w-xl" />
      ) : (
        <div className="max-w-xl">
          <SessionForm
            client={{ id: client.data.id, name: client.data.name }}
            {...(defaultDate === undefined ? {} : { defaultDate })}
            onSaved={onSaved}
          />
        </div>
      )}
    </>
  );
}

function RescheduleSession({ sessionId, onSaved }: { sessionId: string; onSaved: () => void }) {
  const query = useSessionQuery(sessionId);

  if (query.isError)
    return isApiProblem(query.error) && query.error.status === 404 ? (
      <EmptyState
        icon={CalendarX}
        title="Sessão não encontrada"
        description="Esta sessão já não existe."
      />
    ) : (
      <ErrorState error={query.error} onRetry={() => void query.refetch()} />
    );

  const session = query.data;
  if (session === undefined)
    return (
      <div aria-label="A carregar…" role="status" className="space-y-3">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-64 w-full max-w-xl" />
      </div>
    );
  if (session.status !== 'scheduled')
    return (
      <EmptyState
        icon={CalendarX}
        title="Esta sessão já não pode ser reagendada"
        description="Só é possível reagendar sessões agendadas. Repõe-na como agendada na agenda."
      />
    );

  return (
    <>
      <PageHeader title="Reagendar sessão" description={`Com ${session.client_name}.`} />
      <div className="max-w-xl">
        <RescheduleForm session={session} onSaved={onSaved} />
      </div>
    </>
  );
}
