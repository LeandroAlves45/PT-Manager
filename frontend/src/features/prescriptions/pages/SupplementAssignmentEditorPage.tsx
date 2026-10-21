import { ArrowLeft, PackageOpen } from 'lucide-react';
import { useCallback } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';

import { useClientQuery } from '@/features/clients/api/queries';
import { useSupplementAssignmentQuery } from '@/features/prescriptions/api/assignments';
import { SupplementAssignmentForm } from '@/features/prescriptions/components/SupplementAssignmentForm';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

const LIST_PATH = '/trainer/supplement-assignments';

function Loading() {
  return (
    <div role="status" aria-label="A carregar…" className="space-y-3">
      <Skeleton className="h-10 w-72" />
      <Skeleton className="h-64 w-full max-w-xl" />
    </div>
  );
}

/**
 * Criar ou editar uma atribuição de suplemento numa página própria (sem painel lateral).
 *
 * Sem `assignmentId` na rota é criação; com ele carrega a atribuição. `?client_id=` (vindo do
 * separador «Suplementos» da ficha) fixa o cliente e faz a página voltar à ficha; sem ele volta
 * à lista.
 */
export function SupplementAssignmentEditorPage() {
  const { assignmentId } = useParams();
  const [searchParams] = useSearchParams();
  const clientId = searchParams.get('client_id');
  const backPath =
    clientId === null || clientId === ''
      ? LIST_PATH
      : `/trainer/clients/${clientId}?tab=supplements`;
  const backLabel =
    backPath === LIST_PATH ? 'Voltar a Suplementos atribuídos' : 'Voltar ao cliente';

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={backPath}>
          <ArrowLeft aria-hidden /> {backLabel}
        </Link>
      </Button>
      {assignmentId === undefined ? (
        <CreateAssignment clientId={clientId} backPath={backPath} />
      ) : (
        <EditAssignment assignmentId={assignmentId} backPath={backPath} />
      )}
    </section>
  );
}

function CreateAssignment({ clientId, backPath }: { clientId: string | null; backPath: string }) {
  const navigate = useNavigate();
  const onSaved = useCallback(
    () => void navigate(backPath, { replace: true }),
    [backPath, navigate]
  );

  if (clientId === null || clientId === '')
    return (
      <SupplementAssignmentForm
        assignment={null}
        fixedClient={null}
        backPath={backPath}
        onSaved={onSaved}
      />
    );

  return <CreateForClient clientId={clientId} backPath={backPath} onSaved={onSaved} />;
}

/** Criação com o cliente fixo por `?client_id=`: carrega o nome para o mostrar. */
function CreateForClient({
  clientId,
  backPath,
  onSaved,
}: {
  clientId: string;
  backPath: string;
  onSaved: () => void;
}) {
  const client = useClientQuery(clientId);

  if (client.isPending) return <Loading />;
  if (client.isError)
    return <ErrorState error={client.error} onRetry={() => void client.refetch()} />;

  return (
    <SupplementAssignmentForm
      assignment={null}
      fixedClient={{ id: client.data.id, name: client.data.name }}
      backPath={backPath}
      onSaved={onSaved}
    />
  );
}

function EditAssignment({ assignmentId, backPath }: { assignmentId: string; backPath: string }) {
  const navigate = useNavigate();
  const onSaved = useCallback(
    () => void navigate(backPath, { replace: true }),
    [backPath, navigate]
  );
  const query = useSupplementAssignmentQuery(assignmentId);

  if (query.isPending) return <Loading />;

  if (query.isError) {
    return isApiProblem(query.error) && query.error.status === 404 ? (
      <EmptyState
        icon={PackageOpen}
        title="Atribuição não encontrada"
        description="Esta atribuição já não existe ou foi apagada."
      />
    ) : (
      <ErrorState error={query.error} onRetry={() => void query.refetch()} />
    );
  }

  const assignment = query.data;
  // Inativa ou de suplemento arquivado não se edita (o servidor recusa com 409).
  if (!assignment.is_active || assignment.is_supplement_archived) {
    return (
      <>
        <PageHeader title="Editar atribuição" />
        <p role="note" className="text-muted-foreground max-w-xl text-sm">
          {assignment.is_active
            ? 'O suplemento desta atribuição está arquivado. Reativa-o na biblioteca para a editares.'
            : 'Esta atribuição está inativa. Reativa-a na lista para a editares.'}
        </p>
      </>
    );
  }

  return (
    <SupplementAssignmentForm
      assignment={assignment}
      fixedClient={null}
      backPath={backPath}
      onSaved={onSaved}
    />
  );
}
