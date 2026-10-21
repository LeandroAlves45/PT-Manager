import { ArrowLeft, UserX } from 'lucide-react';
import { useCallback } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { useClientQuery } from '@/features/clients/api/queries';
import { ClientForm } from '@/features/clients/components/ClientForm';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

type ClientDetails = components['schemas']['ClientDetailsResponse'];

/**
 * Criar (`/trainer/clients/new`) ou editar (`/trainer/clients/:clientId/edit`) a ficha do
 * cliente numa página própria, sem painel lateral.
 *
 * Depois de guardar vai sempre para a ficha: a do cliente acabado de criar ou a do editado.
 * O aviso de alterações por guardar vive no `ClientForm`.
 */
export function ClientEditorPage() {
  const { clientId } = useParams();

  return clientId === undefined ? <CreateClient /> : <EditClient clientId={clientId} />;
}

/** Botão «Voltar a …» no topo, como nas páginas do catálogo. */
function BackLink({ to, label }: { to: string; label: string }) {
  return (
    <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
      <Link to={to}>
        <ArrowLeft aria-hidden /> {label}
      </Link>
    </Button>
  );
}

function CreateClient() {
  const navigate = useNavigate();
  const onSaved = useCallback(
    (client: ClientDetails) => void navigate(`/trainer/clients/${client.id}`, { replace: true }),
    [navigate]
  );

  return (
    <section className="space-y-6">
      <BackLink to="/trainer/clients" label="Voltar a Clientes" />
      <PageHeader
        title="Criar cliente"
        description="A ficha fica no teu espaço. O convite para o portal envia-se a seguir, na ficha."
      />
      <div className="max-w-3xl">
        <ClientForm client={null} onSaved={onSaved} />
      </div>
    </section>
  );
}

function EditClient({ clientId }: { clientId: string }) {
  const navigate = useNavigate();
  const query = useClientQuery(clientId);
  const detailPath = `/trainer/clients/${clientId}`;
  const onSaved = useCallback(
    () => void navigate(detailPath, { replace: true }),
    [navigate, detailPath]
  );

  return (
    <section className="space-y-6">
      <BackLink to={detailPath} label="Voltar à ficha" />
      {query.isPending ? (
        <div role="status" aria-label="A carregar ficha…" className="space-y-3">
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-64 w-full max-w-3xl" />
        </div>
      ) : query.isError ? (
        isApiProblem(query.error) && query.error.status === 404 ? (
          <EmptyState
            icon={UserX}
            title="Cliente não encontrado"
            description="Este cliente já não existe ou não é teu."
          />
        ) : (
          <ErrorState error={query.error} onRetry={() => void query.refetch()} />
        )
      ) : (
        <>
          <PageHeader title={`Editar ${query.data.name}`} description="Ficha do cliente." />
          <div className="max-w-3xl">
            <ClientForm client={query.data} onSaved={onSaved} />
          </div>
        </>
      )}
    </section>
  );
}
