import { CheckCircle2 } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';

import { usePortalInitialAssessmentStatusQuery } from '@/features/portal/api/portal';
import {
  InitialAssessmentWizard,
  type WizardOutcome,
} from '@/features/portal/components/InitialAssessmentWizard';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { PortalUnavailable } from '@/features/portal/components/PortalUnavailable';
import { isApiProblem } from '@/shared/api/problem';
import { ErrorState } from '@/shared/components/ErrorState';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Avaliação inicial do cliente (`/portal/initial-assessment`): preenchimento em três passos.
 *
 * Depois de guardada, o cliente nunca mais a vê nem edita: abrir a página com a avaliação já
 * entregue (estado `pending: false`, ou 409 ao enviar) mostra só a mensagem e a ligação ao
 * Início. Ao entregar, volta ao Início com um aviso.
 */
export function PortalInitialAssessmentPage() {
  const status = usePortalInitialAssessmentStatusQuery();
  const navigate = useNavigate();
  const [outcome, setOutcome] = useState<WizardOutcome | null>(null);
  const onFinished = useCallback((value: WizardOutcome) => setOutcome(value), []);

  // Depois de entregar, o estado passa a `pending: false`: o ecrã tem de se lembrar de que
  // acabou de enviar para não trocar o formulário pela mensagem antes de navegar.
  useEffect(() => {
    if (outcome === 'sent') void navigate('/portal');
  }, [outcome, navigate]);

  if (status.isPending)
    return (
      <section className="mx-auto w-full max-w-xl space-y-4">
        <Skeleton className="h-10 w-48" />
        <div role="status" aria-label="A carregar a avaliação inicial…" className="space-y-3">
          <Skeleton className="h-16" />
          <Skeleton className="h-64" />
        </div>
      </section>
    );

  if (isApiProblem(status.error) && status.error.code === 'portal_profile_not_available')
    return <PortalUnavailable />;

  if (status.data === undefined)
    return <ErrorState error={status.error} onRetry={() => void status.refetch()} titleAs="h1" />;

  const alreadyDelivered = outcome === 'exists' || (outcome === null && !status.data.pending);

  return (
    <section className="mx-auto w-full max-w-xl space-y-4">
      <header className="space-y-1">
        <h1 className="font-display pe-1 text-3xl leading-none">Avaliação inicial</h1>
        {!alreadyDelivered && (
          <p className="text-muted-foreground text-sm">
            O teu personal trainer usa estes dados para preparar os teus planos. Só a preenches uma
            vez.
          </p>
        )}
      </header>

      {alreadyDelivered ? (
        <PortalCard tone="assessment">
          <p role="status" className="flex items-center gap-2 text-sm">
            <CheckCircle2 aria-hidden className="text-primary size-5 shrink-0" />A tua avaliação
            inicial já foi entregue.
          </p>
          <Button asChild variant="outline" className="mt-3 min-h-11 w-full">
            <Link to="/portal">Voltar ao início</Link>
          </Button>
        </PortalCard>
      ) : (
        <PortalCard tone="assessment">
          <InitialAssessmentWizard onFinished={onFinished} />
        </PortalCard>
      )}
    </section>
  );
}
