import { Archive, Package, Pencil, Plus, RotateCcw, SearchX } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import type { PackTypeListFilters } from '@/features/packs/api/keys';
import { usePackTypeActivityMutation } from '@/features/packs/api/mutations';
import { PACK_PAGE_SIZE, usePackTypeListQuery } from '@/features/packs/api/queries';
import { PackTypeForm } from '@/features/packs/components/PackTypeForm';
import { priceLabel } from '@/features/packs/lib/labels';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
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

type PackType = components['schemas']['PackTypeResponse'];

const ACTIVITY = ['active', 'archived', 'all'] as const;
const ACTIVITY_LABELS = { active: 'Ativos', archived: 'Arquivados', all: 'Todos' } as const;

/**
 * Separador "Tipos de pack": o catálogo do que o personal trainer vende (nome, sessões, preço,
 * duração prevista), com criar, editar, arquivar e reativar.
 *
 * Arquivar só impede vendas novas: os packs já vendidos continuam a ser usados.
 */
export function PackTypesPanel() {
  const [search, setSearch] = useState('');
  const [activity, setActivity] = useState<PackTypeListFilters['activity']>('active');
  const [page, setPage] = useState(1);
  const debounced = useDebounce(search.trim(), 300);
  const query = usePackTypeListQuery({ search: debounced, activity, page });
  const activityMutation = usePackTypeActivityMutation();
  const [editing, setEditing] = useState<PackType | null | undefined>(undefined);
  const [confirm, setConfirm] = useState<PackType | null>(null);

  const items = query.data?.items ?? [];
  const total = query.data?.total_count ?? 0;
  const filtered = debounced !== '' || activity !== 'active' || page > 1;

  async function toggleActivity() {
    if (confirm === null) return;

    try {
      await activityMutation.mutateAsync({
        packTypeId: confirm.id,
        action: confirm.is_active ? 'archive' : 'reactivate',
      });
      toast.success(
        confirm.is_active
          ? 'Tipo de pack arquivado com sucesso.'
          : 'Tipo de pack reativado com sucesso.'
      );
      setConfirm(null);
    } catch {
      toast.error('Não foi possível concluir a ação. Tenta novamente.');
    }
  }

  return (
    <section aria-label="Tipos de pack" className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <Input
          aria-label="Pesquisar tipos de pack"
          placeholder="Pesquisar por nome"
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(1);
          }}
          className="max-w-xs"
        />
        <div role="group" aria-label="Estado" className="border-border flex rounded-lg border p-1">
          {ACTIVITY.map((value) => (
            <Button
              key={value}
              variant={activity === value ? 'secondary' : 'ghost'}
              size="sm"
              className="min-h-11 md:min-h-8"
              aria-pressed={activity === value}
              onClick={() => {
                setActivity(value);
                setPage(1);
              }}
            >
              {ACTIVITY_LABELS[value]}
            </Button>
          ))}
        </div>
        <Button className="ml-auto" onClick={() => setEditing(null)}>
          <Plus aria-hidden /> Novo tipo de pack
        </Button>
      </div>

      {query.isPending ? (
        <div role="status" aria-label="A carregar tipos de pack…" className="space-y-2">
          {Array.from({ length: 4 }, (_, index) => (
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
            description="Nenhum tipo de pack corresponde a esta pesquisa e filtro."
            action={
              <Button
                variant="outline"
                onClick={() => {
                  setSearch('');
                  setActivity('active');
                  setPage(1);
                }}
              >
                Limpar filtros
              </Button>
            }
          />
        ) : (
          <EmptyState
            icon={Package}
            title="Ainda não tens tipos de pack"
            description="Cria o primeiro (ex.: 10 sessões por 300 €) para o poderes vender aos clientes."
            action={<Button onClick={() => setEditing(null)}>Criar tipo de pack</Button>}
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
                <th scope="col" className="p-3">
                  Sessões
                </th>
                <th scope="col" className="p-3">
                  Preço
                </th>
                <th scope="col" className="p-3">
                  Duração
                </th>
                <th scope="col" className="p-3">
                  Estado
                </th>
                <th scope="col" className="p-3">
                  Ações
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((packType) => (
                <tr key={packType.id} className="border-border border-t">
                  <th scope="row" className="p-3 text-left font-medium">
                    {packType.name}
                  </th>
                  <td className="p-3 tabular-nums">{packType.session_count}</td>
                  <td className="p-3 tabular-nums">{priceLabel(packType)}</td>
                  <td className="text-muted-foreground p-3">
                    {packType.expected_duration_days === null
                      ? '—'
                      : `${packType.expected_duration_days} dias`}
                  </td>
                  <td className="p-3">{packType.is_active ? 'Ativo' : 'Arquivado'}</td>
                  <td className="flex gap-1 p-2">
                    <Button
                      size="icon-sm"
                      className="size-11 md:size-8"
                      variant="ghost"
                      aria-label={`Editar ${packType.name}`}
                      onClick={() => setEditing(packType)}
                    >
                      <Pencil aria-hidden />
                    </Button>
                    <Button
                      size="icon-sm"
                      className="size-11 md:size-8"
                      variant="ghost"
                      aria-label={`${packType.is_active ? 'Arquivar' : 'Reativar'} ${packType.name}`}
                      onClick={() => setConfirm(packType)}
                    >
                      {packType.is_active ? <Archive aria-hidden /> : <RotateCcw aria-hidden />}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Pagination
        label="Páginas de tipos de pack"
        page={page}
        total={total}
        pageSize={PACK_PAGE_SIZE}
        onPageChange={setPage}
      />

      <Sheet
        open={editing !== undefined}
        onOpenChange={(open) => {
          if (!open) setEditing(undefined);
        }}
      >
        <SheetContent className="w-full sm:max-w-130">
          <SheetHeader>
            <SheetTitle>{editing ? 'Editar tipo de pack' : 'Novo tipo de pack'}</SheetTitle>
            <SheetDescription>
              O que vendes aos clientes: número de sessões, preço e duração prevista.
            </SheetDescription>
          </SheetHeader>
          {editing !== undefined && (
            <div className="min-h-0 flex-1 overflow-y-auto px-4">
              <PackTypeForm packType={editing} onSaved={() => setEditing(undefined)} />
            </div>
          )}
        </SheetContent>
      </Sheet>

      <ConfirmDialog
        open={confirm !== null}
        onOpenChange={(open) => {
          if (!open) setConfirm(null);
        }}
        title={confirm?.is_active ? 'Arquivar tipo de pack?' : 'Reativar tipo de pack?'}
        description={
          confirm?.is_active
            ? `${confirm.name} deixa de poder ser vendido. Os packs já vendidos continuam válidos.`
            : `${confirm?.name ?? ''} volta a poder ser vendido.`
        }
        confirmLabel={confirm?.is_active ? 'Arquivar' : 'Reativar'}
        destructive={confirm?.is_active === true}
        pending={activityMutation.isPending}
        onConfirm={() => void toggleActivity()}
      />
    </section>
  );
}
