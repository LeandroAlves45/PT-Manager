import { ArrowLeft } from 'lucide-react';
import { useCallback } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { useClientQuery, useInitialAssessmentQuery } from '@/features/clients/api/queries';
import { InitialAssessmentForm } from '@/features/clients/components/InitialAssessmentForm';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Avaliação inicial do cliente numa página própria (`/trainer/clients/:clientId/initial-assessment`).
 *
 * Cria a avaliação quando o cliente ainda não tem (a API responde 404 e o hook devolve
 * `null`) ou corrige a existente. Depois de guardar volta à ficha.
 */
export function InitialAssessmentPage() {
  const { clientId = '' } = useParams();
  const navigate = useNavigate();
  const client = useClientQuery(clientId);
  const assessment = useInitialAssessmentQuery(clientId, client.isSuccess);
  const detailPath = `/trainer/clients/${clientId}`;
  const onSaved = useCallback(
    () => void navigate(detailPath, { replace: true }),
    [navigate, detailPath]
  );

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={detailPath}>
          <ArrowLeft aria-hidden /> Voltar à ficha
        </Link>
      </Button>
      {client.isError ? (
        <ErrorState error={client.error} onRetry={() => void client.refetch()} />
      ) : assessment.isError ? (
        <ErrorState error={assessment.error} onRetry={() => void assessment.refetch()} />
      ) : !client.isSuccess || !assessment.isSuccess ? (
        <div role="status" aria-label="A carregar avaliação…" className="space-y-3">
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-64 w-full max-w-3xl" />
        </div>
      ) : (
        <>
          <PageHeader
            title={
              assessment.data === null ? 'Criar avaliação inicial' : 'Corrigir avaliação inicial'
            }
            description={client.data.name}
          />
          <div className="max-w-3xl">
            <InitialAssessmentForm
              clientId={clientId}
              assessment={assessment.data}
              onSaved={onSaved}
            />
          </div>
        </>
      )}
    </section>
  );
}
