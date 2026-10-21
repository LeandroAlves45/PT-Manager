import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';

import { ClientCombobox, type ClientChoice } from '@/features/clients';
import {
  findExistingAssignment,
  type SupplementAssignment,
} from '@/features/prescriptions/api/assignments';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import {
  CatalogPicker,
  type CatalogChoice,
} from '@/features/prescriptions/components/CatalogPicker';
import { prescriptionError } from '@/features/prescriptions/lib/errors';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { FormField } from '@/shared/components/FormField';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';
import { formatDoseAndTiming } from '@/shared/lib/format';

/**
 * Página de criar ou editar uma atribuição de suplemento a um cliente.
 *
 * Cabeçalho e formulário num só componente porque o título muda a meio: depois de reativar a
 * atribuição que causou um 409, o formulário passa a editá-la (`target`) com a dose e o momento
 * já escritos, em vez de repetir o POST (daria outro conflito).
 *
 * A dose é texto livre: o servidor normaliza «gr» para «g» e a lista mostra o valor devolvido.
 * Nada se normaliza aqui.
 *
 * @param assignment Atribuição a editar (ativa e de suplemento não arquivado), ou `null` para criar.
 * @param fixedClient Cliente pré-escolhido e fixo (`?client_id=`, vindo da ficha), ou `null`.
 * @param backPath Para onde voltar ao guardar ou cancelar.
 * @param onSaved Chamado depois de guardar, já sem o aviso de saída ativo.
 */
export function SupplementAssignmentForm({
  assignment,
  fixedClient,
  backPath,
  onSaved,
}: {
  assignment: SupplementAssignment | null;
  fixedClient: ClientChoice | null;
  backPath: string;
  onSaved: () => void;
}) {
  const queryClient = useQueryClient();
  const [initial] = useState(() => ({
    servingSize: assignment?.serving_size ?? '',
    timing: assignment?.timing ?? '',
    notes: assignment?.trainer_notes ?? '',
  }));
  const [target, setTarget] = useState<SupplementAssignment | null>(assignment);
  const [chosenClient, setChosenClient] = useState<ClientChoice | null>(fixedClient);
  const [supplement, setSupplement] = useState<CatalogChoice | null>(null);
  const [servingSize, setServingSize] = useState(initial.servingSize);
  const [timing, setTiming] = useState(initial.timing);
  const [notes, setNotes] = useState(initial.notes);
  const [existing, setExisting] = useState<SupplementAssignment | null>(null);
  const [confirmReactivate, setConfirmReactivate] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);

  const dirty =
    chosenClient?.id !== fixedClient?.id ||
    supplement !== null ||
    servingSize !== initial.servingSize ||
    timing !== initial.timing ||
    notes !== initial.notes;
  const blocker = useUnsavedChangesGuard(dirty && !saved);
  // Só depois de o render sem «alterações por guardar» é que se sai: o guard não bloqueia a
  // navegação que o próprio guardar provoca.
  useEffect(() => {
    if (saved) onSaved();
  }, [saved, onSaved]);

  /** Refresca já as listas em cache, mesmo as que não estão visíveis: a lista que se abre a seguir mostra a nova. */
  const refreshLists = () =>
    queryClient.invalidateQueries({
      queryKey: prescriptionKeys.assignments,
      refetchType: 'all',
    });

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        serving_size: servingSize.trim(),
        // Vazio (e não null) diz ao servidor que o Momento fica vazio: null herdaria o do catálogo.
        timing: timing.trim(),
        trainer_notes: notes.trim() || null,
      };
      if (target !== null) {
        return unwrap(
          await apiClient.PATCH('/api/v1/supplement-assignments/{assignmentId}', {
            params: { path: { assignmentId: target.id } },
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
      await refreshLists();
      toast.success(
        target === null ? 'Suplemento atribuído com sucesso.' : 'Atribuição atualizada com sucesso.'
      );
      setExisting(null);
      setSaved(true);
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
          setExisting(await findExistingAssignment(chosenClient.id, supplement.value));
        } catch {
          setExisting(null);
        }
      }
    },
  });

  const reactivate = useMutation({
    mutationFn: async (id: string) =>
      unwrap(
        await apiClient.POST('/api/v1/supplement-assignments/{assignmentId}/reactivate', {
          params: { path: { assignmentId: id } },
        })
      ),
    onSuccess: async (updated) => {
      await refreshLists();
      // Reativação vinda do 409: o formulário passa a editar a atribuição reativada, com a
      // dose e o momento já escritos. Repetir o POST daria outro conflito.
      if (existing?.id === updated.id) setTarget({ ...existing, ...updated });
      setConfirmReactivate(false);
      setExisting(null);
      setError('');
      toast.success('Estado da atribuição atualizado com sucesso.');
    },
    onError: (failure) => {
      setConfirmReactivate(false);
      setError(prescriptionError(failure));
    },
  });

  return (
    <>
      <PageHeader
        title={target === null ? 'Criar atribuição' : 'Editar atribuição'}
        description="Dose e momento que o cliente vê no portal."
      />
      {error !== '' && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
      {existing !== null && (
        <div role="status" className="border-border max-w-xl rounded-xl border p-4">
          <p>
            {existing.supplement_name} · {existing.client_name ?? chosenClient?.name}
            {' · '}
            {formatDoseAndTiming(existing.serving_size, existing.timing)}
          </p>
          <p>
            {existing.is_active
              ? 'Esta atribuição já está ativa.'
              : 'Esta atribuição está inativa. Podes reativá-la.'}
          </p>
          {!existing.is_active && (
            <Button className="mt-2 min-h-11 md:min-h-8" onClick={() => setConfirmReactivate(true)}>
              Reativar atribuição existente
            </Button>
          )}
        </div>
      )}
      <form
        className="grid max-w-xl gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          setError('');
          if (target === null && (chosenClient === null || supplement === null)) {
            setError('Escolhe o cliente e o suplemento.');
            return;
          }
          if (servingSize.trim() === '') {
            setError('Indica a dose.');
            return;
          }
          void save.mutateAsync().catch(() => undefined);
        }}
      >
        {target !== null ? (
          <dl className="grid grid-cols-[minmax(0,6rem)_1fr] gap-x-4 gap-y-2 text-sm">
            <dt className="text-muted-foreground">Cliente</dt>
            <dd>{target.client_name ?? 'Cliente'}</dd>
            <dt className="text-muted-foreground">Suplemento</dt>
            <dd>{target.supplement_name}</dd>
          </dl>
        ) : (
          <>
            {fixedClient === null ? (
              <FormField label="Cliente">
                {(control) => (
                  <ClientCombobox
                    {...control}
                    value={chosenClient}
                    onChange={(choice) => {
                      setChosenClient(choice);
                      setExisting(null);
                    }}
                  />
                )}
              </FormField>
            ) : (
              <dl className="grid grid-cols-[minmax(0,6rem)_1fr] gap-x-4 text-sm">
                <dt className="text-muted-foreground">Cliente</dt>
                <dd>{fixedClient.name}</dd>
              </dl>
            )}
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
                    setExisting(null);
                  }}
                />
              )}
            </FormField>
          </>
        )}
        <FormField label="Dose">
          {(control) => (
            <Input
              {...control}
              value={servingSize}
              maxLength={100}
              placeholder="ex.: 5 g"
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
              onChange={(event) => setTiming(event.target.value)}
            />
          )}
        </FormField>
        <FormField label="Notas para o cliente">
          {(control) => (
            <Input {...control} value={notes} onChange={(event) => setNotes(event.target.value)} />
          )}
        </FormField>
        <div className="flex gap-2">
          <Button type="submit" className="min-h-11 md:min-h-8" disabled={save.isPending}>
            {save.isPending
              ? target === null
                ? 'A criar…'
                : 'A guardar…'
              : target === null
                ? 'Criar'
                : 'Guardar'}
          </Button>
          <Button asChild variant="outline" className="min-h-11 md:min-h-8">
            <Link to={backPath}>Cancelar</Link>
          </Button>
        </div>
      </form>
      <ConfirmDialog
        open={confirmReactivate}
        onOpenChange={(open) => {
          if (!open) setConfirmReactivate(false);
        }}
        title="Reativar atribuição?"
        description="O estado do suplemento atribuído ao cliente será atualizado."
        confirmLabel="Reativar"
        pending={reactivate.isPending}
        onConfirm={() => {
          if (existing !== null) reactivate.mutate(existing.id);
        }}
      />
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title="Sair sem guardar?"
        description="As alterações a esta atribuição perdem-se."
        confirmLabel="Sair sem guardar"
        cancelLabel="Continuar a editar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </>
  );
}
