import { ArrowLeft, ClipboardCheck } from 'lucide-react';
import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';

import { useCheckInActionMutation } from '@/features/check-ins/api/mutations';
import { useCheckInQuery } from '@/features/check-ins/api/queries';
import { CorrectCheckInForm } from '@/features/check-ins/components/CorrectCheckInForm';
import { answerLines, type AnswerLine } from '@/features/check-ins/lib/answers';
import { checkInErrorMessage, isAwaitingReview } from '@/features/check-ins/lib/checkInStatus';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate, formatDateTime } from '@/shared/lib/format';

type CheckIn = components['schemas']['CheckInResponse'];

/**
 * Ver a resposta de uma avaliação periódica (antes «check-in»), corrigir os valores e marcá-la
 * como revista, tudo numa página (`/trainer/check-ins/:checkInId`).
 *
 * Abre sempre em leitura; «Corrigir valores» troca para o formulário (que pede confirmação ao
 * sair com alterações por guardar) e, depois de guardar ou cancelar, volta à leitura.
 * `?client_id=` (aberta na ficha) faz «Voltar» regressar a `/trainer/clients/:id?tab=check-ins`.
 */
export function CheckInDetailPage() {
  const { checkInId } = useParams();
  const [params] = useSearchParams();
  const clientId = params.get('client_id');
  const backPath =
    clientId === null ? '/trainer/check-ins' : `/trainer/clients/${clientId}?tab=check-ins`;
  const query = useCheckInQuery(checkInId);

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={backPath}>
          <ArrowLeft aria-hidden />{' '}
          {clientId === null ? 'Voltar a Avaliações' : 'Voltar ao cliente'}
        </Link>
      </Button>
      {query.isError ? (
        isApiProblem(query.error) && query.error.status === 404 ? (
          <EmptyState
            icon={ClipboardCheck}
            title="Avaliação não encontrada"
            description="Esta avaliação já não existe."
          />
        ) : (
          <ErrorState error={query.error} onRetry={() => void query.refetch()} />
        )
      ) : query.data === undefined ? (
        <div aria-label="A carregar…" role="status" className="space-y-3">
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-64 w-full max-w-2xl" />
        </div>
      ) : (
        <CheckInDetail checkIn={query.data} />
      )}
    </section>
  );
}

function CheckInDetail({ checkIn }: { checkIn: CheckIn }) {
  const [correcting, setCorrecting] = useState(false);
  const action = useCheckInActionMutation();
  const awaiting = isAwaitingReview(checkIn);

  async function review() {
    try {
      await action.mutateAsync({ checkInId: checkIn.id, action: 'review' });
      toast.success(`Avaliação periódica de ${checkIn.client_name} marcada como revista.`);
    } catch (error) {
      toast.error(
        checkInErrorMessage(
          isApiProblem(error) ? error.code : '',
          'Não foi possível marcar a avaliação como revista. Tenta novamente.'
        )
      );
    }
  }

  const description = [
    `Dia ${formatDate(checkIn.check_in_date)}`,
    checkIn.responded_at !== null && `respondida a ${formatDateTime(checkIn.responded_at)}`,
    checkIn.reviewed_at !== null && `revista a ${formatDateTime(checkIn.reviewed_at)}`,
  ]
    .filter((part): part is string => part !== false)
    .join(' · ');

  return (
    <>
      <PageHeader
        title={`${correcting ? 'Corrigir avaliação periódica' : 'Avaliação periódica'} de ${checkIn.client_name}`}
        description={description}
        action={
          awaiting && !correcting ? (
            <Button
              className="min-h-11 md:min-h-8"
              disabled={action.isPending}
              onClick={() => void review()}
            >
              {action.isPending ? 'A marcar…' : 'Marcar como revista'}
            </Button>
          ) : undefined
        }
      />
      <div className="max-w-2xl">
        {checkIn.status !== 'answered' ? (
          <EmptyState
            icon={ClipboardCheck}
            title="Sem resposta"
            description="Esta avaliação ainda não foi respondida pelo cliente."
          />
        ) : correcting ? (
          <CorrectCheckInForm
            checkIn={checkIn}
            onSaved={() => setCorrecting(false)}
            onCancel={() => setCorrecting(false)}
          />
        ) : (
          <AnswerView checkIn={checkIn} onCorrect={() => setCorrecting(true)} />
        )}
      </div>
    </>
  );
}

function AnswerView({ checkIn, onCorrect }: { checkIn: CheckIn; onCorrect: () => void }) {
  const { metrics, measurements, feedback } = answerLines(checkIn);

  return (
    <div className="space-y-6 pb-4">
      <AnswerGroup title="Valores" lines={metrics} columns />
      {measurements.length > 0 && <AnswerGroup title="Medidas (cm)" lines={measurements} columns />}
      {feedback.length > 0 && <AnswerGroup title="Respostas" lines={feedback} />}
      {checkIn.target_date !== null && (
        <p className="text-muted-foreground text-sm">
          Data-alvo: {formatDate(checkIn.target_date)}
        </p>
      )}
      <div className="border-border flex justify-end border-t pt-4">
        <Button variant="outline" className="min-h-11 md:min-h-8" onClick={onCorrect}>
          Corrigir valores
        </Button>
      </div>
    </div>
  );
}

function AnswerGroup({
  title,
  lines,
  columns = false,
}: {
  title: string;
  lines: readonly AnswerLine[];
  columns?: boolean;
}) {
  return (
    <section className="space-y-2">
      <h2 className="font-medium">{title}</h2>
      <dl className={columns ? 'grid gap-3 sm:grid-cols-2' : 'space-y-3'}>
        {lines.map((line) => (
          <div key={line.label}>
            <dt className="text-muted-foreground text-xs">{line.label}</dt>
            <dd className="text-sm whitespace-pre-line">{line.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
