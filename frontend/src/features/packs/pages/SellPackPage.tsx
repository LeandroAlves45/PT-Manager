import { ArrowLeft } from 'lucide-react';
import { useCallback } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { useFixedClientQuery } from '@/features/packs/api/queries';
import { AssignPackForm } from '@/features/packs/components/AssignPackForm';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Vender um pack numa página própria (sem painel lateral).
 *
 * Duas rotas, a mesma página:
 * - `/trainer/clients/:clientId/sell-pack` (aberta na ficha): o cliente está fixo e, depois de
 *   vender ou «Voltar», regressa a `/trainer/clients/:clientId?tab=sessions`, o separador que
 *   mostra os packs na ficha;
 * - `/trainer/sessions/sell-pack` (separador «Packs dos clientes»): escolhe-se o cliente e
 *   regressa-se a `/trainer/sessions?tab=packs`.
 */
export function SellPackPage() {
  const { clientId } = useParams();
  const navigate = useNavigate();
  const client = useFixedClientQuery(clientId);
  const backPath =
    clientId === undefined
      ? '/trainer/sessions?tab=packs'
      : `/trainer/clients/${clientId}?tab=sessions`;
  const onSaved = useCallback(
    () => void navigate(backPath, { replace: true }),
    [navigate, backPath]
  );

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={backPath}>
          <ArrowLeft aria-hidden />{' '}
          {clientId === undefined ? 'Voltar a Packs dos clientes' : 'Voltar ao cliente'}
        </Link>
      </Button>
      <PageHeader
        title="Vender pack"
        description={
          client.data === undefined
            ? 'As sessões marcadas depois descontam deste pack.'
            : `Para ${client.data.name}. As sessões marcadas depois descontam deste pack.`
        }
      />
      {clientId === undefined ? (
        <div className="max-w-xl">
          <AssignPackForm client={null} onSaved={onSaved} />
        </div>
      ) : client.isError ? (
        <ErrorState error={client.error} onRetry={() => void client.refetch()} />
      ) : client.data === undefined ? (
        <Skeleton aria-label="A carregar cliente…" role="status" className="h-64 w-full max-w-xl" />
      ) : (
        <div className="max-w-xl">
          <AssignPackForm
            client={{ id: client.data.id, name: client.data.name }}
            onSaved={onSaved}
          />
        </div>
      )}
    </section>
  );
}
