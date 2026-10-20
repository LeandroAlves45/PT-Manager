import { ArrowLeft, PackageOpen } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';

import { useCatalogItemQuery, type CatalogKind } from '@/features/admin-catalog/api/catalog';
import { ExerciseForm } from '@/features/admin-catalog/components/ExerciseForm';
import { FoodForm } from '@/features/admin-catalog/components/FoodForm';
import { SupplementForm } from '@/features/admin-catalog/components/SupplementForm';
import {
  consumePendingExerciseVideo,
  peekPendingExerciseVideo,
} from '@/features/admin-catalog/lib/pending-video';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

const PLURAL: Record<CatalogKind, string> = {
  foods: 'Alimentos',
  exercises: 'Exercícios',
  supplements: 'Suplementos',
};
const SINGULAR: Record<CatalogKind, string> = {
  foods: 'alimento',
  exercises: 'exercício',
  supplements: 'suplemento',
};

/**
 * Criar ou editar um item do catálogo global numa página própria (sem painel lateral).
 *
 * Sem `itemId` na rota é criação; com `itemId` carrega o item e edita-o. Depois de guardar
 * volta à lista, exceto num exercício novo com vídeo: abre a edição, onde o envio começa.
 */
export function CatalogEditorPage({ kind }: { kind: CatalogKind }) {
  const { itemId } = useParams();
  const listPath = `/admin/catalog/${kind}`;
  const back = (
    <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
      <Link to={listPath}>
        <ArrowLeft aria-hidden /> Voltar a {PLURAL[kind]}
      </Link>
    </Button>
  );

  return (
    <section className="space-y-6">
      {back}
      {itemId === undefined ? <CreateItem kind={kind} /> : <EditItem kind={kind} itemId={itemId} />}
    </section>
  );
}

function CreateItem({ kind }: { kind: CatalogKind }) {
  const navigate = useNavigate();
  const onSaved = useCallback(
    (saved: { id: string }, withVideo = false) =>
      void navigate(withVideo ? `/admin/catalog/${kind}/${saved.id}` : `/admin/catalog/${kind}`, {
        replace: true,
      }),
    [kind, navigate]
  );

  return (
    <>
      <PageHeader
        title={`Criar ${SINGULAR[kind]}`}
        description="Os dados ficam disponíveis no catálogo global."
      />
      <KindForm kind={kind} item={null} onSaved={onSaved} />
    </>
  );
}

function EditItem({ kind, itemId }: { kind: CatalogKind; itemId: string }) {
  const navigate = useNavigate();
  const query = useCatalogItemQuery(kind, itemId);
  // O ficheiro escolhido em «Criar exercício» chega por um flash de módulo: lê-se no
  // inicializador (o StrictMode chama-o duas vezes) e esquece-se num efeito.
  const [initialFile] = useState(() =>
    kind === 'exercises' ? peekPendingExerciseVideo(itemId) : null
  );
  useEffect(() => {
    if (kind === 'exercises') consumePendingExerciseVideo(itemId);
  }, [kind, itemId]);
  const onSaved = useCallback(
    () => void navigate(`/admin/catalog/${kind}`, { replace: true }),
    [kind, navigate]
  );

  if (query.isPending) {
    return (
      <div aria-label="A carregar…" className="space-y-3">
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

  return (
    <>
      <PageHeader
        title={`Editar ${query.data.name}`}
        description="Os dados ficam disponíveis no catálogo global."
      />
      <KindForm kind={kind} item={query.data} onSaved={onSaved} initialFile={initialFile} />
    </>
  );
}

/** Escolhe o formulário do tipo; `item` é `null` ao criar. */
function KindForm({
  kind,
  item,
  onSaved,
  initialFile = null,
}: {
  kind: CatalogKind;
  item:
    | components['schemas']['GlobalFoodResponse']
    | components['schemas']['GlobalExerciseResponse']
    | components['schemas']['GlobalSupplementResponse']
    | null;
  onSaved: (saved: { id: string }, withVideo?: boolean) => void;
  initialFile?: File | null;
}) {
  if (kind === 'foods')
    return (
      <FoodForm
        item={item as components['schemas']['GlobalFoodResponse'] | null}
        onSaved={onSaved}
      />
    );
  if (kind === 'supplements')
    return (
      <SupplementForm
        item={item as components['schemas']['GlobalSupplementResponse'] | null}
        onSaved={onSaved}
      />
    );

  return (
    <ExerciseForm
      item={item as components['schemas']['GlobalExerciseResponse'] | null}
      onSaved={onSaved}
      initialFile={initialFile}
    />
  );
}
