import { Archive, PackageOpen, Plus, RotateCcw, Trash2 } from 'lucide-react';
import { parseAsInteger, parseAsString, useQueryStates } from 'nuqs';
import { useState } from 'react';
import { toast } from 'sonner';

import {
  useCatalogAction,
  useCatalogQuery,
  type CatalogItem,
  type CatalogKind,
} from '@/features/admin-catalog/api/catalog';
import { ExerciseForm } from '@/features/admin-catalog/components/ExerciseForm';
import { FoodForm } from '@/features/admin-catalog/components/FoodForm';
import { SupplementForm } from '@/features/admin-catalog/components/SupplementForm';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { Input } from '@/shared/components/ui/input';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { formatNumber } from '@/shared/lib/format';

const TITLES: Record<CatalogKind, string> = {
  foods: 'Alimentos',
  exercises: 'Exercícios',
  supplements: 'Suplementos',
};
const ACTIVITY = ['active', 'archived', 'all'] as const;
const ACTIVITY_LABELS = { active: 'Ativos', archived: 'Arquivados', all: 'Todos' };
const VIDEO_STATUS_LABELS: Record<string, string> = {
  pending: 'pendente',
  processing: 'em processamento',
  ready: 'pronto',
  rejected: 'recusado',
  failed: 'falhado',
};

/** Estrutura comum dos três catálogos; campos e formulários permanecem específicos. */
export function CatalogPage({ kind }: { kind: CatalogKind }) {
  const [url, setUrl] = useQueryStates({
    search: parseAsString.withDefault(''),
    activity: parseAsString.withDefault('active'),
    page: parseAsInteger.withDefault(1),
  });
  const search = useDebounce(url.search.trim(), 300);
  const activity = ACTIVITY.find((value) => value === url.activity) ?? 'active';
  const page = Math.max(1, url.page);
  const filters = { search, activity, page };
  const query = useCatalogQuery(kind, filters);
  const action = useCatalogAction(kind);
  const [editing, setEditing] = useState<CatalogItem | 'new' | null>(null);
  const [confirm, setConfirm] = useState<{
    item: CatalogItem;
    action: 'archive' | 'reactivate' | 'delete';
  } | null>(null);
  const title = TITLES[kind];
  const items = query.data?.items ?? [];
  const total = query.data?.total_count ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / 25));

  function updateSearch(value: string) {
    void setUrl({ search: value, page: 1 });
  }

  async function confirmAction() {
    if (confirm === null) return;

    try {
      await action.mutateAsync({ id: confirm.item.id, action: confirm.action });
      toast.success('Catálogo atualizado com sucesso.');
      setConfirm(null);
    } catch (error) {
      if (isApiProblem(error) && error.code === 'global_exercise_has_video') {
        toast.error('Este exercício tem um vídeo associado. Remove o vídeo antes de o apagar.');
      } else if (isApiProblem(error) && error.status === 409) {
        toast.error(
          confirm.action === 'delete'
            ? 'O item está em uso. Arquiva-o em vez de o apagar.'
            : 'O estado do item mudou. Atualiza a lista e tenta novamente.'
        );
      } else {
        toast.error('Não foi possível concluir a ação. Tenta novamente.');
      }
    }
  }

  return (
    <section className="space-y-6">
      <PageHeader
        title={`Catálogo global de ${title.toLowerCase()}`}
        description={`${formatNumber(total)} ${title.toLowerCase()}`}
        action={
          <Button onClick={() => setEditing('new')}>
            <Plus aria-hidden /> Novo{' '}
            {kind === 'foods' ? 'alimento' : kind === 'exercises' ? 'exercício' : 'suplemento'}
          </Button>
        }
      />
      <div className="flex flex-wrap items-center gap-3">
        <Input
          aria-label={`Pesquisar ${title.toLowerCase()}`}
          placeholder="Pesquisar por nome"
          value={url.search}
          onChange={(event) => updateSearch(event.target.value)}
          className="max-w-xs"
        />
        <div
          role="group"
          aria-label="Disponibilidade"
          className="border-border flex rounded-lg border p-1"
        >
          {ACTIVITY.map((value) => (
            <Button
              key={value}
              variant={activity === value ? 'secondary' : 'ghost'}
              size="sm"
              className="min-h-11 md:min-h-8"
              aria-pressed={activity === value}
              onClick={() => void setUrl({ activity: value, page: 1 })}
            >
              {ACTIVITY_LABELS[value]}
            </Button>
          ))}
        </div>
      </div>
      {query.isPending ? (
        <div aria-label="A carregar catálogo..." className="space-y-2">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          icon={PackageOpen}
          title="Sem resultados"
          description="Não há itens para esta pesquisa e filtro."
          action={
            <Button
              variant="outline"
              onClick={() => void setUrl({ search: '', activity: 'active', page: 1 })}
            >
              Limpar filtros
            </Button>
          }
        />
      ) : (
        <div className="border-border bg-card overflow-x-auto rounded-xl border">
          <table className="w-full min-w-2xl text-sm">
            <thead className="bg-muted text-left">
              <tr>
                <th scope="col" className="p-3">
                  Nome
                </th>
                {kind === 'foods' ? (
                  <>
                    <th scope="col" className="p-3">
                      kcal
                    </th>
                    <th scope="col" className="p-3">
                      P
                    </th>
                    <th scope="col" className="p-3">
                      HC
                    </th>
                    <th scope="col" className="p-3">
                      G
                    </th>
                    <th scope="col" className="p-3">
                      Fibra
                    </th>
                  </>
                ) : (
                  <th scope="col" className="p-3">
                    Detalhes
                  </th>
                )}
                <th scope="col" className="p-3">
                  Estado
                </th>
                <th scope="col" className="p-3">
                  Ações
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id} className="border-border border-t">
                  <th scope="row" className="p-3 text-left font-medium">
                    {item.name}
                  </th>
                  {kind === 'foods' ? (
                    <FoodCells food={item as components['schemas']['GlobalFoodResponse']} />
                  ) : (
                    <td className="text-muted-foreground p-3">{detailFor(kind, item)}</td>
                  )}
                  <td className="p-3">{item.is_active ? 'Ativo' : 'Arquivado'}</td>
                  <td className="flex gap-1 p-2">
                    <Button
                      size="sm"
                      variant="outline"
                      className="min-h-11 md:min-h-8"
                      onClick={() => setEditing(item)}
                    >
                      Editar {item.name}
                    </Button>
                    <Button
                      size="icon-sm"
                      className="size-11 md:size-8"
                      variant="ghost"
                      aria-label={`${item.is_active ? 'Arquivar' : 'Reativar'} ${item.name}`}
                      onClick={() =>
                        setConfirm({ item, action: item.is_active ? 'archive' : 'reactivate' })
                      }
                    >
                      {item.is_active ? <Archive aria-hidden /> : <RotateCcw aria-hidden />}
                    </Button>
                    <Button
                      size="icon-sm"
                      className="size-11 md:size-8"
                      variant="ghost"
                      aria-label={`Apagar ${item.name}`}
                      onClick={() => setConfirm({ item, action: 'delete' })}
                    >
                      <Trash2 aria-hidden />
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {total > 25 && (
        <nav aria-label="Páginas do catálogo" className="flex items-center justify-end gap-3">
          <Button
            variant="outline"
            disabled={page <= 1}
            onClick={() => void setUrl({ page: page - 1 })}
          >
            Anterior
          </Button>
          <span>
            Página {page} de {pageCount}
          </span>
          <Button
            variant="outline"
            disabled={page >= pageCount}
            onClick={() => void setUrl({ page: page + 1 })}
          >
            Seguinte
          </Button>
        </nav>
      )}
      <Sheet
        open={editing !== null}
        onOpenChange={(open) => {
          if (!open) setEditing(null);
        }}
      >
        <SheetContent className="w-full sm:max-w-110">
          <SheetHeader>
            <SheetTitle>
              {editing === 'new' ? 'Criar' : 'Editar'} {title.toLowerCase()}
            </SheetTitle>
            <SheetDescription>Os dados ficam disponíveis no catálogo global.</SheetDescription>
          </SheetHeader>
          {editing !== null && (
            <div className="min-h-0 flex-1 overflow-y-auto px-4">
              {kind === 'foods' ? (
                <FoodForm
                  item={
                    editing === 'new'
                      ? null
                      : (editing as components['schemas']['GlobalFoodResponse'])
                  }
                  onSaved={() => setEditing(null)}
                />
              ) : kind === 'exercises' ? (
                <ExerciseForm
                  item={
                    editing === 'new'
                      ? null
                      : (editing as components['schemas']['GlobalExerciseResponse'])
                  }
                  onSaved={() => setEditing(null)}
                />
              ) : (
                <SupplementForm
                  item={
                    editing === 'new'
                      ? null
                      : (editing as components['schemas']['GlobalSupplementResponse'])
                  }
                  onSaved={() => setEditing(null)}
                />
              )}
            </div>
          )}
        </SheetContent>
      </Sheet>
      <Dialog
        open={confirm !== null}
        onOpenChange={(open) => {
          if (!open) setConfirm(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {confirm?.action === 'delete'
                ? 'Apagar definitivamente?'
                : confirm?.action === 'archive'
                  ? 'Arquivar item?'
                  : 'Reativar item?'}
            </DialogTitle>
            <DialogDescription>
              {confirm?.action === 'delete'
                ? 'Esta ação não pode ser desfeita. O item só pode ser apagado se não estiver em uso.'
                : confirm?.action === 'archive'
                  ? 'O item deixa de estar disponível para os personal trainers.'
                  : 'O item volta a estar disponível para os personal trainers.'}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirm(null)}>
              Cancelar
            </Button>
            <Button
              variant={confirm?.action === 'delete' ? 'destructive' : 'default'}
              disabled={action.isPending}
              onClick={() => void confirmAction()}
            >
              {action.isPending ? 'A guardar…' : 'Confirmar'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
}

function FoodCells({ food }: { food: components['schemas']['GlobalFoodResponse'] }) {
  return (
    <>
      <td className="p-3">{formatNumber(food.kcal)}</td>
      <td className="p-3">{formatNumber(food.protein)}</td>
      <td className="p-3">{formatNumber(food.carbs)}</td>
      <td className="p-3">{formatNumber(food.fats)}</td>
      <td className="p-3">{food.fiber === null ? '-' : formatNumber(food.fiber)}</td>
    </>
  );
}

function detailFor(kind: CatalogKind, item: CatalogItem): string {
  if (kind === 'exercises') {
    const exercise = item as components['schemas']['GlobalExerciseResponse'];
    if (exercise.managed_video_status !== null) {
      const status = VIDEO_STATUS_LABELS[exercise.managed_video_status] ?? 'estado indisponível';
      if (
        exercise.has_ready_video &&
        ['failed', 'rejected'].includes(exercise.managed_video_status)
      )
        return `Vídeo disponível · última substituição ${status}`;
      return `Vídeo ${status}`;
    }
    return exercise.video_url ? 'Ligação de vídeo externa' : 'Sem vídeo';
  }

  const supplement = item as components['schemas']['GlobalSupplementResponse'];
  return `${supplement.serving_size} · ${supplement.timing}`;
}
