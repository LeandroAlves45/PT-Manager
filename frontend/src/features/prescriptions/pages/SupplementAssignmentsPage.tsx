import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pill } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';

import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { prescriptionError } from '@/features/prescriptions/lib/errors';
import { apiClient, unwrap } from '@/shared/api/client';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { PageHeader } from '@/shared/components/PageHeader';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDoseAndTiming } from '@/shared/lib/format';

const PAGE_SIZE = 25;
const NEW_PATH = '/trainer/supplement-assignments/new';

/**
 * Atribuições diretas de suplementos: página global com filtro de cliente, ou separador
 * de um cliente (`client`).
 *
 * Só lista: «Nova atribuição» e «Editar» são ligações para a página de criar/editar
 * (`SupplementAssignmentEditorPage`). Dentro da ficha de um cliente as ligações levam
 * `?client_id=`, para a página fixar o cliente e voltar à ficha. Desativar e reativar ficam aqui.
 * Atribuições inativas ou de suplemento arquivado não se editam.
 */
export function SupplementAssignmentsPage({
  client: fixedClient = null,
}: {
  client?: ClientChoice | null;
}) {
  const queryClient = useQueryClient();
  const [filterClient, setFilterClient] = useState<ClientChoice | null>(fixedClient);
  const [activity, setActivity] = useState<'active' | 'inactive' | 'all'>('active');
  const [page, setPage] = useState(1);
  const [confirm, setConfirm] = useState<{
    id: string;
    action: 'deactivate' | 'reactivate';
  } | null>(null);
  const [error, setError] = useState('');
  const clientQuery = fixedClient === null ? '' : `?client_id=${fixedClient.id}`;

  const list = useQuery({
    queryKey: prescriptionKeys.assignmentList(filterClient?.id ?? null, activity, page),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/supplement-assignments', {
          params: {
            query: {
              client_id: filterClient?.id,
              activity,
              page_number: page,
              page_size: PAGE_SIZE,
            },
          },
          signal,
        })
      ),
  });

  const changeActivity = useMutation({
    mutationFn: async ({ id, action }: { id: string; action: 'deactivate' | 'reactivate' }) =>
      action === 'deactivate'
        ? unwrap(
            await apiClient.POST('/api/v1/supplement-assignments/{assignmentId}/deactivate', {
              params: { path: { assignmentId: id } },
            })
          )
        : unwrap(
            await apiClient.POST('/api/v1/supplement-assignments/{assignmentId}/reactivate', {
              params: { path: { assignmentId: id } },
            })
          ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.assignments });
      setConfirm(null);
      setError('');
      toast.success('Estado da atribuição atualizado com sucesso.');
    },
    onError: (failure) => {
      setConfirm(null);
      setError(prescriptionError(failure));
    },
  });

  const newAction = (
    <Button asChild>
      <Link to={`${NEW_PATH}${clientQuery}`}>Nova atribuição</Link>
    </Button>
  );

  return (
    <section className="space-y-5">
      {fixedClient === null ? (
        <div className="space-y-2">
          <PageHeader
            title="Suplementos atribuídos"
            description="Gere as instruções de suplementos por cliente."
            action={newAction}
          />
          <p className="text-muted-foreground max-w-prose text-sm">
            Os suplementos que juntas a uma refeição ficam no plano alimentar desse cliente.
          </p>
        </div>
      ) : (
        <div className="flex items-center justify-between">
          <h2 className="font-display text-xl">Suplementos</h2>
          {newAction}
        </div>
      )}
      {error !== '' && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
      {fixedClient === null && (
        <div className="flex items-end gap-2">
          <FormField label="Filtrar por cliente">
            {(control) => (
              <ClientCombobox
                {...control}
                value={filterClient}
                onChange={(choice) => {
                  setFilterClient(choice);
                  setPage(1);
                }}
              />
            )}
          </FormField>
          {filterClient !== null && (
            <Button
              variant="outline"
              onClick={() => {
                setFilterClient(null);
                setPage(1);
              }}
            >
              Todos os clientes
            </Button>
          )}
        </div>
      )}
      <select
        aria-label="Estado das atribuições"
        className="border-input bg-background h-9 rounded-md border px-3"
        value={activity}
        onChange={(event) => {
          setActivity(event.target.value as typeof activity);
          setPage(1);
        }}
      >
        <option value="active">Ativas</option>
        <option value="inactive">Inativas</option>
        <option value="all">Todas</option>
      </select>
      {list.isPending ? (
        <Skeleton role="status" aria-label="A carregar atribuições…" className="h-48 w-full" />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data.items.length === 0 ? (
        <EmptyState
          icon={Pill}
          title="Sem suplementos atribuídos"
          description={
            activity !== 'active' || page > 1 || filterClient !== null
              ? 'Não há resultados para estes filtros.'
              : 'Atribui o primeiro suplemento.'
          }
        />
      ) : (
        <>
          <ul className="space-y-2">
            {list.data.items.map((item) => (
              <li
                key={item.id}
                className="border-border flex flex-wrap items-center justify-between gap-3 rounded-xl border p-4"
              >
                <div>
                  <h3 className="font-medium">{item.supplement_name}</h3>
                  {fixedClient === null && (
                    <p className="text-muted-foreground text-sm">{item.client_name ?? 'Cliente'}</p>
                  )}
                  <p className="text-muted-foreground text-sm">
                    {formatDoseAndTiming(item.serving_size, item.timing)}
                    {!item.is_active && ' · Inativa'}
                    {item.is_supplement_archived && ' · Suplemento arquivado'}
                  </p>
                </div>
                <div className="flex gap-2">
                  {item.is_active && !item.is_supplement_archived && (
                    <Button asChild variant="outline" className="min-h-11 md:min-h-8">
                      <Link
                        to={`/trainer/supplement-assignments/${item.id}${clientQuery}`}
                        aria-label={`Editar ${item.supplement_name}${
                          item.client_name === null ? '' : ` de ${item.client_name}`
                        }`}
                      >
                        Editar
                      </Link>
                    </Button>
                  )}
                  <Button
                    variant="outline"
                    className="min-h-11 md:min-h-8"
                    onClick={() =>
                      setConfirm({
                        id: item.id,
                        action: item.is_active ? 'deactivate' : 'reactivate',
                      })
                    }
                  >
                    {item.is_active ? 'Desativar' : 'Reativar'}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
          <Pagination
            label="Páginas de suplementos atribuídos"
            page={page}
            total={list.data.total_count}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
          />
        </>
      )}
      <ConfirmDialog
        open={confirm !== null}
        onOpenChange={(open) => {
          if (!open) setConfirm(null);
        }}
        title={confirm?.action === 'deactivate' ? 'Desativar atribuição?' : 'Reativar atribuição?'}
        description="O estado do suplemento atribuído ao cliente será atualizado."
        confirmLabel={confirm?.action === 'deactivate' ? 'Desativar' : 'Reativar'}
        destructive={confirm?.action === 'deactivate'}
        pending={changeActivity.isPending}
        onConfirm={() => {
          if (confirm !== null) changeActivity.mutate(confirm);
        }}
      />
    </section>
  );
}
