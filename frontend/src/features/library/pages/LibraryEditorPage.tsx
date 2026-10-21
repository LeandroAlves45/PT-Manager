import { ArrowLeft, PackageOpen } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import type { LibraryKind } from '@/features/library/api/keys';
import { useLibraryItemQuery } from '@/features/library/api/queries';
import { ExerciseForm } from '@/features/library/components/ExerciseForm';
import { FoodForm } from '@/features/library/components/FoodForm';
import { LibraryItemView } from '@/features/library/components/LibraryItemView';
import { SupplementForm } from '@/features/library/components/SupplementForm';
import { isEditable } from '@/features/library/lib/items';
import { LIBRARY_TEXTS } from '@/features/library/lib/labels';
import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { consumePendingExerciseVideo, peekPendingExerciseVideo } from '@/shared/lib/pending-video';

/** Lista do separador: exercícios é o separador por omissão e fica sem `?tab`. */
function listPathOf(kind: LibraryKind): string {
  return kind === 'exercises' ? '/trainer/library' : `/trainer/library?tab=${kind}`;
}

const DESCRIPTION = (kind: LibraryKind) =>
  `O ${LIBRARY_TEXTS[kind].singular} fica só na tua biblioteca. Os clientes veem-no nos planos.`;

/**
 * Criar, editar ou ver um item da biblioteca privada numa página própria (sem painel lateral).
 *
 * Sem `itemId` na rota é criação; com `itemId` carrega o item. Um item privado, ativo e não
 * bloqueado edita-se; um global, arquivado ou bloqueado abre só para ver. Depois de guardar
 * volta ao separador da lista, exceto num exercício novo com vídeo: abre a edição, onde o envio
 * começa.
 *
 * @param kind Tipo de item, vindo da rota (`exercises`, `foods` ou `supplements`).
 */
export function LibraryEditorPage({ kind }: { kind: LibraryKind }) {
  const { itemId } = useParams();

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={listPathOf(kind)}>
          <ArrowLeft aria-hidden /> Voltar a {LIBRARY_TEXTS[kind].tab}
        </Link>
      </Button>
      {itemId === undefined ? <CreateItem kind={kind} /> : <EditItem kind={kind} itemId={itemId} />}
    </section>
  );
}

function CreateItem({ kind }: { kind: LibraryKind }) {
  const navigate = useNavigate();
  const onSaved = useCallback(
    (saved: { id: string }, withVideo = false) =>
      void navigate(withVideo ? `/trainer/library/${kind}/${saved.id}` : listPathOf(kind), {
        replace: true,
      }),
    [kind, navigate]
  );

  return (
    <>
      <PageHeader title={`Criar ${LIBRARY_TEXTS[kind].singular}`} description={DESCRIPTION(kind)} />
      <div className="max-w-xl">
        {kind === 'exercises' ? (
          <ExerciseForm exercise={null} onSaved={onSaved} />
        ) : kind === 'foods' ? (
          <FoodForm food={null} onSaved={onSaved} />
        ) : (
          <SupplementForm supplement={null} onSaved={onSaved} />
        )}
      </div>
    </>
  );
}

function EditItem({ kind, itemId }: { kind: LibraryKind; itemId: string }) {
  const navigate = useNavigate();
  const query = useLibraryItemQuery(kind, itemId);
  // O ficheiro escolhido em «Criar exercício» chega por um flash de módulo: lê-se no
  // inicializador (o StrictMode chama-o duas vezes) e esquece-se num efeito.
  const [initialFile] = useState(() =>
    kind === 'exercises' ? peekPendingExerciseVideo(itemId) : null
  );
  useEffect(() => {
    if (kind === 'exercises') consumePendingExerciseVideo(itemId);
  }, [kind, itemId]);
  const onSaved = useCallback(
    () => void navigate(listPathOf(kind), { replace: true }),
    [kind, navigate]
  );

  if (query.isPending) {
    return (
      <div role="status" aria-label="A carregar…" className="space-y-3">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-64 w-full max-w-xl" />
      </div>
    );
  }

  if (query.isError) {
    return isApiProblem(query.error) && query.error.status === 404 ? (
      <EmptyState
        icon={PackageOpen}
        title="Item não encontrado"
        description="Este item já não existe ou foi apagado."
      />
    ) : (
      <ErrorState error={query.error} onRetry={() => void query.refetch()} />
    );
  }

  const data = query.data;
  const editable = isEditable(data.item);

  return (
    <>
      <PageHeader
        title={editable ? `Editar ${data.item.name}` : data.item.name}
        description={editable ? DESCRIPTION(kind) : 'Só de leitura.'}
      />
      <div className="max-w-xl">
        {!editable ? (
          <LibraryItemView data={data} />
        ) : data.kind === 'exercises' ? (
          <ExerciseForm exercise={data.item} onSaved={onSaved} initialFile={initialFile} />
        ) : data.kind === 'foods' ? (
          <FoodForm food={data.item} onSaved={onSaved} />
        ) : (
          <SupplementForm supplement={data.item} onSaved={onSaved} />
        )}
      </div>
    </>
  );
}
