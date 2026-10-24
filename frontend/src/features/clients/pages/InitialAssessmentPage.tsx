import { ArrowLeft } from 'lucide-react';
import { useCallback } from 'react';
import { toast } from 'sonner';
import { Link, useNavigate, useParams } from 'react-router';

import { useReviewInitialAssessmentMutation } from '@/features/clients/api/mutations';
import { useClientQuery, useInitialAssessmentQuery } from '@/features/clients/api/queries';
import { InitialAssessmentForm } from '@/features/clients/components/InitialAssessmentForm';
import { isApiProblem } from '@/shared/api/problem';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate } from '@/shared/lib/format';

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
  const current = assessment.data ?? null;
  const review = useReviewInitialAssessmentMutation(clientId);
  const detailPath = `/trainer/clients/${clientId}`;
  const onSaved = useCallback(
    () => void navigate(detailPath, { replace: true }),
    [navigate, detailPath]
  );

  async function markReviewed(assessmentId: string) {
    try {
      await review.mutateAsync(assessmentId);
      toast.success('Avaliação inicial marcada como revista.');
    } catch (error) {
      toast.error(
        isApiProblem(error) && error.code === 'initial_assessment_not_reviewable'
          ? 'Esta avaliação foi criada por ti e não precisa de revisão.'
          : 'Não foi possível marcar a avaliação como revista. Tenta novamente.'
      );
    }
  }

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
          {current?.submitted_by_client === true && (
            <div className="border-border bg-card flex max-w-3xl flex-wrap items-center justify-between gap-3 rounded-lg border p-3 text-sm">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="secondary">Preenchida pelo cliente</Badge>
                {current.reviewed_at !== null && (
                  <span className="text-muted-foreground">
                    Revista a {formatDate(current.reviewed_at)}
                  </span>
                )}
              </div>
              {current.reviewed_at === null && (
                <Button
                  type="button"
                  className="min-h-11 md:min-h-9"
                  disabled={review.isPending}
                  onClick={() => void markReviewed(current.id)}
                >
                  {review.isPending ? 'A marcar…' : 'Marcar como revista'}
                </Button>
              )}
            </div>
          )}
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
