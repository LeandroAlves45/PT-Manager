import { ArrowLeft, Package } from 'lucide-react';
import { useCallback } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { usePackTypeQuery } from '@/features/packs/api/queries';
import { PackTypeForm } from '@/features/packs/components/PackTypeForm';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/** Para onde se volta: o separador «Tipos de pack» da página Sessões e packs. */
const BACK_PATH = '/trainer/sessions?tab=types';

/**
 * Criar ou editar um tipo de pack numa página própria (sem painel lateral).
 *
 * Sem `packTypeId` na rota é criação; com `packTypeId` carrega o tipo e edita-o. Depois de
 * guardar volta ao separador «Tipos de pack».
 */
export function PackTypeEditorPage() {
  const { packTypeId } = useParams();
  const navigate = useNavigate();
  const query = usePackTypeQuery(packTypeId);
  const onSaved = useCallback(() => void navigate(BACK_PATH, { replace: true }), [navigate]);

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={BACK_PATH}>
          <ArrowLeft aria-hidden /> Voltar a Tipos de pack
        </Link>
      </Button>
      {packTypeId === undefined ? (
        <>
          <PageHeader
            title="Criar tipo de pack"
            description="O que vendes aos clientes: número de sessões, preço e duração prevista."
          />
          <div className="max-w-xl">
            <PackTypeForm packType={null} onSaved={onSaved} />
          </div>
        </>
      ) : query.isError ? (
        isApiProblem(query.error) && query.error.status === 404 ? (
          <EmptyState
            icon={Package}
            title="Tipo de pack não encontrado"
            description="Este tipo de pack já não existe."
          />
        ) : (
          <ErrorState error={query.error} onRetry={() => void query.refetch()} />
        )
      ) : query.data === undefined ? (
        <div aria-label="A carregar…" role="status" className="space-y-3">
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-64 w-full max-w-xl" />
        </div>
      ) : (
        <>
          <PageHeader
            title={`Editar ${query.data.name}`}
            description="Altera o nome, as sessões, o preço ou a duração prevista."
          />
          <div className="max-w-xl">
            <PackTypeForm packType={query.data} onSaved={onSaved} />
          </div>
        </>
      )}
    </section>
  );
}
