import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pill } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import {
  CatalogPicker,
  type CatalogChoice,
} from '@/features/prescriptions/components/CatalogPicker';
import { prescriptionError } from '@/features/prescriptions/lib/errors';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { PageHeader } from '@/shared/components/PageHeader';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';

type Assignment = components['schemas']['ClientSupplementAssignmentResponse'];
const PAGE_SIZE = 25;

/**
 * Procura a atribuição que causou o 409 `supplement_assignment_already_exists`, em páginas
 * de 100 (ativas e inativas) do cliente, até a encontrar ou esgotar o total.
 */
async function findExisting(clientId: string, supplementId: string): Promise<Assignment | null> {
  let page = 1;
  while (true) {
    const result = unwrap(
      await apiClient.GET('/api/v1/supplement-assignments', {
        params: {
          query: {
            client_id: clientId,
            activity: 'all',
            page_number: page,
            page_size: 100,
          },
        },
      })
    );
    const found = result.items.find((item) => item.supplement_id === supplementId);
    if (found !== undefined) return found;
    if (page * 100 >= result.total_count) return null;
    page++;
  }
}

/**
 * Atribuições diretas de suplementos: página global com filtro de cliente, ou separador
 * de um cliente (`client`).
 *
 * Um 409 de duplicado mostra a atribuição existente: se ativa, só informa; se inativa,
 * oferece reativação, e o formulário passa a editar a atribuição reativada com a dose e o
 * momento escritos. Atribuições inativas ou de suplemento arquivado não se editam.
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
  const [editing, setEditing] = useState<Assignment | null | undefined>(undefined);
  const [chosenClient, setChosenClient] = useState<ClientChoice | null>(fixedClient);
  const [supplement, setSupplement] = useState<CatalogChoice | null>(null);
  const [servingSize, setServingSize] = useState('');
  const [timing, setTiming] = useState('');
  const [notes, setNotes] = useState('');
  const [existingAssignment, setExistingAssignment] = useState<Assignment | null>(null);
  const [confirm, setConfirm] = useState<{
    id: string;
    action: 'deactivate' | 'reactivate';
  } | null>(null);
  const [error, setError] = useState('');

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

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        serving_size: servingSize.trim(),
        timing: timing.trim(),
        trainer_notes: notes.trim() || null,
      };
      if (editing !== null && editing !== undefined) {
        return unwrap(
          await apiClient.PATCH('/api/v1/supplement-assignments/{assignmentId}', {
            params: { path: { assignmentId: editing.id } },
            body,
          })
        );
      }

      if (chosenClient === null || supplement === null) throw new Error('selection_required');

      return unwrap(
        await apiClient.POST('/api/v1/supplement-assignments', {
          body: { client_id: chosenClient.id, supplement_id: supplement.value, ...body },
        })
      );
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.assignments });
      toast.success(
        editing === null
          ? 'Suplemento atribuído com sucesso.'
          : 'Atribuição atualizada com sucesso.'
      );
      setEditing(undefined);
      setExistingAssignment(null);
    },
    onError: async (failure) => {
      setError(prescriptionError(failure));
      if (
        isApiProblem(failure) &&
        failure.code === 'supplement_assignment_already_exists' &&
        chosenClient !== null &&
        supplement !== null
      ) {
        try {
          setExistingAssignment(await findExisting(chosenClient.id, supplement.value));
        } catch {
          setExistingAssignment(null);
        }
      }
    },
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
    onSuccess: async (updated, { action }) => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.assignments });
      // Reativação vinda do 409: o formulário passa a editar a atribuição reativada, com a
      // dose e o momento já escritos. Repetir o POST daria outro conflito.
      if (action === 'reactivate' && editing === null && existingAssignment?.id === updated.id)
        setEditing(updated);
      setConfirm(null);
      setExistingAssignment(null);
      setError('');
      toast.success('Estado da atribuição atualizado com sucesso.');
    },
    onError: (failure) => setError(prescriptionError(failure)),
  });

  function openNew() {
    setEditing(null);
    setChosenClient(fixedClient);
    setSupplement(null);
    setServingSize('');
    setTiming('');
    setNotes('');
    setExistingAssignment(null);
    setError('');
  }

  function openEdit(item: Assignment) {
    setEditing(item);
    setServingSize(item.serving_size);
    setTiming(item.timing);
    setNotes(item.trainer_notes ?? '');
    setExistingAssignment(null);
    setError('');
  }

  return (
    <section className="space-y-5">
      {fixedClient === null ? (
        <PageHeader
          title="Suplementos atribuídos"
          description="Gere as instruções de suplementos por cliente."
          action={<Button onClick={openNew}>Nova atribuição</Button>}
        />
      ) : (
        <div className="flex items-center justify-between">
          <h2 className="font-display text-xl">Suplementos</h2>
          <Button onClick={openNew}>Nova atribuição</Button>
        </div>
      )}
      {error !== '' && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
      {existingAssignment !== null && (
        <div role="status" className="border-border rounded-xl border p-4">
          <p>
            {existingAssignment.supplement_name} ·{' '}
            {existingAssignment.client_name ?? chosenClient?.name}
            {' · '}
            {existingAssignment.serving_size} · {existingAssignment.timing}
          </p>
          <p>
            {existingAssignment.is_active
              ? 'Esta atribuição já está ativa.'
              : 'Esta atribuição está inativa. Podes reativá-la.'}
          </p>
          {!existingAssignment.is_active && (
            <Button onClick={() => setConfirm({ id: existingAssignment.id, action: 'reactivate' })}>
              Reativar atribuição existente
            </Button>
          )}
        </div>
      )}
      {editing !== undefined && (
        <form
          className="border-border grid gap-3 rounded-xl border p-4 sm:grid-cols-2"
          onSubmit={(event) => {
            event.preventDefault();
            setError('');
            if (editing === null && (chosenClient === null || supplement === null)) {
              setError('Escolhe o cliente e o suplemento.');
              return;
            }
            if (servingSize.trim() === '' || timing.trim() === '') {
              setError('Indica a dose e o momento.');
              return;
            }
            void save.mutateAsync().catch(() => undefined);
          }}
        >
          <h3 className="font-display text-xl sm:col-span-2">
            {editing === null ? 'Nova atribuição' : 'Editar atribuição'}
          </h3>
          {editing === null && fixedClient === null && (
            <FormField label="Cliente">
              {(control) => (
                <ClientCombobox {...control} value={chosenClient} onChange={setChosenClient} />
              )}
            </FormField>
          )}
          {editing === null && (
            <FormField label="Suplemento">
              {(control) => (
                <CatalogPicker
                  id={control.id}
                  kind="supplements"
                  value={supplement}
                  onChange={(choice) => {
                    setSupplement(choice);
                    setServingSize(choice.servingSize ?? '');
                    setTiming(choice.timing ?? '');
                    setExistingAssignment(null);
                  }}
                />
              )}
            </FormField>
          )}
          <FormField label="Dose">
            {(control) => (
              <Input
                {...control}
                value={servingSize}
                maxLength={100}
                required
                onChange={(event) => setServingSize(event.target.value)}
              />
            )}
          </FormField>
          <FormField label="Momento">
            {(control) => (
              <Input
                {...control}
                value={timing}
                maxLength={255}
                required
                onChange={(event) => setTiming(event.target.value)}
              />
            )}
          </FormField>
          <FormField label="Notas para o cliente">
            {(control) => (
              <Input
                {...control}
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
              />
            )}
          </FormField>
          <div className="flex gap-2 sm:col-span-2">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? 'A guardar…' : 'Guardar atribuição'}
            </Button>
            <Button type="button" variant="outline" onClick={() => setEditing(undefined)}>
              Cancelar
            </Button>
          </div>
        </form>
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
                    {item.serving_size} · {item.timing}
                    {!item.is_active && ' · Inativa'}
                    {item.is_supplement_archived && ' · Suplemento arquivado'}
                  </p>
                </div>
                <div className="flex gap-2">
                  {item.is_active && !item.is_supplement_archived && (
                    <Button variant="outline" onClick={() => openEdit(item)}>
                      Editar
                    </Button>
                  )}
                  <Button
                    variant="outline"
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
