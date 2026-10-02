import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Dumbbell } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import type { ClientChoice } from '@/features/clients';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { TrainingPlanEditor } from '@/features/prescriptions/components/TrainingPlanEditor';
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

/**
 * Lista de planos de treino: página de todos os clientes, ou separador de um cliente.
 *
 * `client === null` é a lista global (cada cartão mostra `client_name`).
 * Com um cliente, o pedido leva `client_id` e o título passa a «Treino».
 * Nos dois casos há «Novo plano». Ter ou não ter planos vê-se na lista, não neste `null`.
 *
 * `editor`: `undefined` mostra a lista, `null` abre um plano novo, `string` abre esse plano.
 * Pesquisa e filtro de estado voltam à página 1. Enquanto a página nova chega,
 * ficam visíveis os cartões anteriores. Arquivar confirma no ecrã e só depois faz POST.
 * O sucesso invalida todas as queries cuja chave começa por `training-plans`.
 */
export function TrainingPlansPage({ client = null }: { client?: ClientChoice | null }) {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search.trim(), 300);
  const term = search.trim() === '' ? '' : debounced;
  const [activity, setActivity] = useState<'active' | 'archived' | 'all'>('active');
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<string | null | undefined>(undefined);
  const [archiveId, setArchiveId] = useState<string | null>(null);
  const [error, setError] = useState('');
  const list = useQuery({
    queryKey: prescriptionKeys.trainingList(client?.id ?? null, activity, term, page),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/training-plans', {
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
  const archive = useMutation({
    mutationFn: (id: string) =>
      apiClient
        .POST('/api/v1/training-plans/{trainingPlanId}/archive', {
          params: { path: { trainingPlanId: id } },
        })
        .then(unwrap),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.training });
      setArchiveId(null);
      toast.success('Plano de treino arquivado.');
    },
    onError: (failure) => setError(prescriptionError(failure)),
  });

  if (editor !== undefined) {
    return (
      <TrainingPlanEditor
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
          title="Planos de treino"
          description="Prescreve semanas, dias, exercícios e séries."
          action={<Button onClick={() => setEditor(null)}>Novo plano</Button>}
        />
      ) : (
        <div className="flex items-center justify-between">
          <h2 className="font-display text-xl">Treino</h2>
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
          aria-label="Pesquisar planos de treino"
          placeholder="Pesquisar plano"
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(1);
          }}
          className="max-w-xs"
        />
        <select
          aria-label="Estado dos planos de treino"
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
        <Skeleton role="status" aria-label="A carregar planos de treino…" className="h-48 w-full" />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data.items.length === 0 ? (
        <EmptyState
          icon={Dumbbell}
          title="Sem planos de treino"
          description={
            term !== '' || activity !== 'active' || page > 1
              ? 'Não há resultados para estes filtros.'
              : 'Cria o primeiro plano de treino.'
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
                    Início: {plan.start_date}
                    {plan.end_date !== null && ' · Fim: ' + plan.end_date}
                    {plan.is_archived && ' · Arquivado'}
                  </p>
                </div>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setEditor(plan.id)}>
                    Ver plano
                  </Button>
                  {!plan.is_archived && (
                    <Button variant="outline" onClick={() => setArchiveId(plan.id)}>
                      Arquivar
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
          <Pagination
            label="Páginas de planos de treino"
            page={page}
            total={list.data.total_count}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
          />
        </>
      )}
      <ConfirmDialog
        open={archiveId !== null}
        onOpenChange={(open) => {
          if (!open) setArchiveId(null);
        }}
        title="Arquivar plano de treino?"
        description="O plano deixa de estar ativo."
        confirmLabel="Arquivar"
        destructive
        pending={archive.isPending}
        onConfirm={() => {
          if (archiveId !== null) archive.mutate(archiveId);
        }}
      />
    </section>
  );
}
