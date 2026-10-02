import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { UtensilsCrossed } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import type { ClientChoice } from '@/features/clients';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { MealPlanEditor } from '@/features/prescriptions/components/MealPlanEditor';
import { prescriptionError } from '@/features/prescriptions/lib/errors';
import { apiClient, unwrap } from '@/shared/api/client';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { useDebounce } from '@/shared/hooks/useDebounce';

const PAGE_SIZE = 25;

/** Planos alimentares do tenant ou do cliente selecionado. */
export function MealPlansPage({ client = null }: { client?: ClientChoice | null }) {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search.trim(), 300);
  const term = search.trim() === '' ? '' : debounced;
  const [activity, setActivity] = useState<'active' | 'archived' | 'all'>('active');
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<string | null | undefined>(undefined);
  const [actionId, setActionId] = useState<string | null>(null);
  const [actionKind, setActionKind] = useState<'archive' | 'reactivate'>('archive');
  const [error, setError] = useState('');
  const list = useQuery({
    queryKey: prescriptionKeys.mealsList(client?.id ?? null, activity, term, page),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/meal-plans', {
          params: {
            query: {
              client_id: client?.id,
              activity,
              search: term || undefined,
              page_number: page,
              page_size: PAGE_SIZE,
            },
          },
          signal,
        })
      ),
  });

  const changeActivity = useMutation({
    mutationFn: async ({ id, kind }: { id: string; kind: 'archive' | 'reactivate' }) =>
      kind === 'archive'
        ? unwrap(
            await apiClient.POST('/api/v1/meal-plans/{mealPlanId}/archive', {
              params: { path: { mealPlanId: id } },
            })
          )
        : unwrap(
            await apiClient.POST('/api/v1/meal-plans/{mealPlanId}/reactivate', {
              params: { path: { mealPlanId: id } },
            })
          ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.meals });
      setActionId(null);
      toast.success(
        actionKind === 'archive'
          ? 'Plano alimentar arquivado com sucesso.'
          : 'Plano alimentar reativado com sucesso.'
      );
    },
    onError: (failure) => setError(prescriptionError(failure)),
  });

  if (editor !== undefined) {
    return (
      <MealPlanEditor
        key={editor ?? 'new'}
        planId={editor}
        fixedClient={client}
        onClose={() => setEditor(undefined)}
      />
    );
  }

  return (
    <section className="space-y-5">
      {client === null ? (
        <PageHeader
          title="Planos alimentares"
          description="Define refeições e calcula os alvos nutricionais."
          action={<Button onClick={() => setEditor(null)}>Novo plano</Button>}
        />
      ) : (
        <div className="flex items-center justify-between">
          <h2 className="font-display text-xl">Nutrição</h2>
          <Button onClick={() => setEditor(null)}>Novo plano</Button>
        </div>
      )}
      {error !== '' && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
      <div className="flex flex-wrap gap-3">
        <Input
          aria-label="Pesquisar planos alimentares"
          placeholder="Pesquisar plano"
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(1);
          }}
          className="max-w-xs"
        />
        <select
          aria-label="Estado dos planos alimentares"
          className="border-input bg-background h-9 rounded-md border px-3"
          value={activity}
          onChange={(event) => {
            setActivity(event.target.value as typeof activity);
            setPage(1);
          }}
        >
          <option value="active">Ativos</option>
          <option value="archived">Arquivados</option>
          <option value="all">Todos</option>
        </select>
      </div>
      {list.isPending ? (
        <Skeleton
          role="status"
          aria-label="A carregar planos alimentares…"
          className="h-48 w-full"
        />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data.items.length === 0 ? (
        <EmptyState
          icon={UtensilsCrossed}
          title="Sem planos alimentares"
          description={
            term !== '' || activity !== 'active' || page > 1
              ? 'Não há resultados para estes filtros.'
              : 'Cria o primeiro plano alimentar.'
          }
        />
      ) : (
        <>
          <ul className="space-y-2">
            {list.data.items.map((plan) => (
              <li
                key={plan.id}
                className="border-border flex flex-wrap items-center justify-between gap-3 rounded-xl border p-4"
              >
                <div>
                  <h3 className="font-medium">{plan.name}</h3>
                  {client === null && (
                    <p className="text-muted-foreground text-sm">{plan.client_name}</p>
                  )}
                  <p className="text-muted-foreground text-sm">
                    {plan.kcal_target} kcal
                    {plan.is_archived && ' · Arquivado'}
                  </p>
                </div>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setEditor(plan.id)}>
                    Ver plano
                  </Button>
                  <Button
                    variant="outline"
                    onClick={() => {
                      setActionId(plan.id);
                      setActionKind(plan.is_archived ? 'reactivate' : 'archive');
                    }}
                  >
                    {plan.is_archived ? 'Reativar' : 'Arquivar'}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
          <Pagination
            label="Páginas de planos alimentares"
            page={page}
            total={list.data.total_count}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
          />
        </>
      )}
      <ConfirmDialog
        open={actionId !== null}
        onOpenChange={(open) => {
          if (!open) setActionId(null);
        }}
        title={actionKind === 'archive' ? 'Arquivar plano alimentar?' : 'Reativar plano alimentar?'}
        description={
          actionKind === 'archive'
            ? 'O plano deixa de estar ativo.'
            : 'O plano volta a estar ativo se não existir conflito.'
        }
        confirmLabel={actionKind === 'archive' ? 'Arquivar' : 'Reativar'}
        destructive={actionKind === 'archive'}
        pending={changeActivity.isPending}
        onConfirm={() => {
          if (actionId !== null) changeActivity.mutate({ id: actionId, kind: actionKind });
        }}
      />
    </section>
  );
}
