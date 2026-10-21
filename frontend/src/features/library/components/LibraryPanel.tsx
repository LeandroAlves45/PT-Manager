import type { UseQueryResult } from '@tanstack/react-query';
import {
  Archive,
  Ban,
  Eye,
  Globe,
  LibraryBig,
  Lock,
  Pencil,
  Plus,
  RotateCcw,
  SearchX,
} from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';

import type { LibraryKind } from '@/features/library/api/keys';
import { useLibraryActivityMutation } from '@/features/library/api/mutations';
import { LIBRARY_PAGE_SIZE } from '@/features/library/api/queries';
import { isEditable, type LibraryItem } from '@/features/library/lib/items';
import { ACTIVITY_LABELS, ACTIVITY_OPTIONS, LIBRARY_TEXTS } from '@/features/library/lib/labels';
import type { LibraryControls } from '@/features/library/lib/useLibraryFilters';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Pagination } from '@/shared/components/Pagination';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';

/** Uma coluna da tabela, além do nome (sempre em primeira). */
export interface LibraryColumn<T> {
  readonly header: string;
  readonly cell: (item: T) => ReactNode;
}

/** Origem e estado do item: cor + ícone + texto (design system, badges de estado). */
export function LibraryItemBadges({ item }: { item: LibraryItem }) {
  return (
    <span className="flex flex-wrap gap-1">
      {item.scope === 'global' ? (
        <Badge variant="outline">
          <Globe aria-hidden /> Global
        </Badge>
      ) : (
        <Badge variant="secondary">
          <Lock aria-hidden /> Privado
        </Badge>
      )}
      {!item.is_active && (
        <Badge variant="outline">
          <Archive aria-hidden /> Arquivado
        </Badge>
      )}
      {item.platform_enforcement_status === 'blocked' && (
        <Badge variant="destructive">
          <Ban aria-hidden /> Bloqueado
        </Badge>
      )}
    </span>
  );
}

/**
 * Um separador da biblioteca: pesquisa (servidor), filtro de estado, tabela paginada e
 * confirmação de arquivar/reativar.
 *
 * Criar, editar e ver são páginas próprias (`/trainer/library/:kind/new` e `/:itemId`): aqui só
 * há ligações. As três tabs só diferem nas colunas, que chegam por props.
 *
 * @param filtered Lista filtrada (pesquisa, estado ou página > 1): vazio = "Sem resultados".
 */
export function LibraryPanel<T extends LibraryItem>({
  kind,
  query,
  controls,
  filtered,
  columns,
}: {
  kind: LibraryKind;
  query: UseQueryResult<{ items: T[]; total_count: number }>;
  controls: LibraryControls;
  filtered: boolean;
  columns: readonly LibraryColumn<T>[];
}) {
  const texts = LIBRARY_TEXTS[kind];
  const activityMutation = useLibraryActivityMutation(kind);
  const [confirm, setConfirm] = useState<T | null>(null);

  const items = query.data?.items ?? [];
  const total = query.data?.total_count ?? 0;

  async function toggleActivity() {
    if (confirm === null) return;

    try {
      await activityMutation.mutateAsync({
        id: confirm.id,
        action: confirm.is_active ? 'archive' : 'reactivate',
      });
      toast.success(
        confirm.is_active
          ? `${confirm.name} foi arquivado com sucesso.`
          : `${confirm.name} foi reativado com sucesso.`
      );
      setConfirm(null);
    } catch {
      toast.error('Não foi possível concluir a ação. Tenta novamente.');
    }
  }

  const basePath = `/trainer/library/${kind}`;

  return (
    <section aria-label={texts.tab} className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <Input
          type="search"
          aria-label={`Pesquisar ${texts.plural}`}
          placeholder="Pesquisar por nome ou descrição"
          maxLength={texts.searchMaxLength}
          value={controls.search}
          onChange={(event) => controls.setSearch(event.target.value)}
          className="max-w-xs"
        />
        <div role="group" aria-label="Estado" className="border-border flex rounded-lg border p-1">
          {ACTIVITY_OPTIONS.map((value) => (
            <Button
              key={value}
              variant={controls.activity === value ? 'secondary' : 'ghost'}
              size="sm"
              className="min-h-11 md:min-h-8"
              aria-pressed={controls.activity === value}
              onClick={() => controls.setActivity(value)}
            >
              {ACTIVITY_LABELS[value]}
            </Button>
          ))}
        </div>
        <Button asChild className="ml-auto">
          <Link to={`${basePath}/new`}>
            <Plus aria-hidden /> {texts.newTitle}
          </Link>
        </Button>
      </div>

      {query.isPending ? (
        <div role="status" aria-label={`A carregar ${texts.plural}…`} className="space-y-2">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        filtered ? (
          <EmptyState
            icon={SearchX}
            title="Sem resultados"
            description={`Nenhum ${texts.singular} corresponde a esta pesquisa e filtro.`}
            action={
              <Button variant="outline" onClick={controls.clear}>
                Limpar filtros
              </Button>
            }
          />
        ) : (
          <EmptyState
            icon={LibraryBig}
            title={texts.emptyTitle}
            description={texts.emptyDescription}
            action={
              <Button asChild>
                <Link to={`${basePath}/new`}>{texts.createLabel}</Link>
              </Button>
            }
          />
        )
      ) : (
        <div className="border-border bg-card overflow-x-auto rounded-xl border">
          <table className="w-full min-w-xl text-sm">
            <thead className="bg-muted text-left">
              <tr>
                <th scope="col" className="p-3">
                  Nome
                </th>
                {columns.map((column) => (
                  <th key={column.header} scope="col" className="p-3">
                    {column.header}
                  </th>
                ))}
                <th scope="col" className="p-3">
                  Ações
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id} className="border-border border-t">
                  <th scope="row" className="p-3 text-left font-medium">
                    <span className="block">{item.name}</span>
                    <LibraryItemBadges item={item} />
                  </th>
                  {columns.map((column) => (
                    <td key={column.header} className="text-muted-foreground p-3">
                      {column.cell(item)}
                    </td>
                  ))}
                  <td className="p-2">
                    <div className="flex gap-1">
                      {isEditable(item) ? (
                        <Button
                          asChild
                          size="icon-sm"
                          className="size-11 md:size-8"
                          variant="ghost"
                        >
                          <Link to={`${basePath}/${item.id}`} aria-label={`Editar ${item.name}`}>
                            <Pencil aria-hidden />
                          </Link>
                        </Button>
                      ) : (
                        <Button
                          asChild
                          size="icon-sm"
                          className="size-11 md:size-8"
                          variant="ghost"
                        >
                          <Link to={`${basePath}/${item.id}`} aria-label={`Ver ${item.name}`}>
                            <Eye aria-hidden />
                          </Link>
                        </Button>
                      )}
                      {item.scope === 'private' && (
                        <Button
                          size="icon-sm"
                          className="size-11 md:size-8"
                          variant="ghost"
                          aria-label={`${item.is_active ? 'Arquivar' : 'Reativar'} ${item.name}`}
                          onClick={() => setConfirm(item)}
                        >
                          {item.is_active ? <Archive aria-hidden /> : <RotateCcw aria-hidden />}
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Pagination
        label={`Páginas de ${texts.plural}`}
        page={controls.page}
        total={total}
        pageSize={LIBRARY_PAGE_SIZE}
        onPageChange={controls.setPage}
      />

      <ConfirmDialog
        open={confirm !== null}
        onOpenChange={(open) => {
          if (!open) setConfirm(null);
        }}
        title={confirm?.is_active ? `Arquivar ${texts.singular}?` : `Reativar ${texts.singular}?`}
        description={
          confirm?.is_active
            ? `${confirm.name} deixa de aparecer ao criar planos novos. Os planos que já o usam não mudam.`
            : `${confirm?.name ?? ''} volta a poder ser usado em planos novos.`
        }
        confirmLabel={confirm?.is_active ? 'Arquivar' : 'Reativar'}
        destructive={confirm?.is_active === true}
        pending={activityMutation.isPending}
        onConfirm={() => void toggleActivity()}
      />
    </section>
  );
}
