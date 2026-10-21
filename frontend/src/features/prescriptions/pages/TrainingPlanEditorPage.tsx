import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import {
  Link,
  Navigate,
  Outlet,
  useLocation,
  useNavigate,
  useParams,
  useSearchParams,
} from 'react-router';
import { toast } from 'sonner';

import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { useFixedClientQuery, useTrainingPlanQuery } from '@/features/prescriptions/api/training';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { StepIndicator } from '@/features/prescriptions/components/StepIndicator';
import { TrainingStructureEditor } from '@/features/prescriptions/components/TrainingStructureEditor';
import {
  EMPTY_STRUCTURE,
  hasEmptyExercise,
  hasRepsRangeError,
  structureFromDetails,
  trainingError,
  validateDetails,
  type TrainingDetails,
  type TrainingStructure,
} from '@/features/prescriptions/lib/training-draft';
import {
  useTrainingEditor,
  type TrainingDraft,
  type TrainingEditorContext,
} from '@/features/prescriptions/lib/training-editor';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { Textarea } from '@/shared/components/ui/textarea';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';

const touch = 'min-h-11 md:min-h-9';
const LIST_PATH = '/trainer/training-plans';

function draftFromPlan(plan: TrainingDetails, client: ClientChoice | null): TrainingDraft {
  return {
    client,
    name: plan.name,
    description: plan.description ?? '',
    modality: plan.training_modality ?? '',
    notes: plan.notes ?? '',
    startDate: plan.start_date,
    endDate: plan.end_date ?? '',
    structure: structureFromDetails(plan),
  };
}

function namesFromPlan(plan: TrainingDetails): Record<string, string> {
  return Object.fromEntries(
    plan.days.flatMap((day) =>
      day.exercises.map((exercise) => [exercise.exercise_id, exercise.exercise_name])
    )
  );
}

/** O destino faz parte do editor (um dos dois passos): navegar aí não perde o rascunho. */
function isInsideEditor(basePath: string, pathname: string): boolean {
  return pathname === basePath || pathname.startsWith(basePath + '/');
}

/**
 * Layout dos dois passos de um plano de treino (U57): `.../new` ou `.../:planId/edit`.
 *
 * Carrega o plano (edição) ou o cliente da ficha (`?client_id=`), e só então monta o
 * `TrainingPlanEditorShell`, que guarda o rascunho em estado e o passa aos passos por
 * `useOutletContext`. Como o layout continua montado entre os passos, «Anterior» e «Seguinte»
 * não perdem nada.
 */
export function TrainingPlanEditorLayout() {
  const { planId } = useParams();
  const [searchParams] = useSearchParams();
  const queryClientId = searchParams.get('client_id');
  const detail = useTrainingPlanQuery(planId ?? null);
  // Em edição o nome do cliente vem da ficha do cliente do plano (o detalhe só traz o ID).
  const fixed = useFixedClientQuery(
    planId === undefined ? queryClientId : (detail.data?.client_id ?? null)
  );

  if (planId !== undefined) {
    if (detail.isError)
      return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />;
    if (detail.data === undefined) return <EditorSkeleton />;
  }
  if (planId !== undefined || queryClientId !== null) {
    if (fixed.isError)
      return <ErrorState error={fixed.error} onRetry={() => void fixed.refetch()} />;
    if (fixed.data === undefined) return <EditorSkeleton />;
  }

  return (
    <TrainingPlanEditorShell
      key={planId ?? 'new'}
      planId={planId}
      plan={detail.data ?? null}
      fixedClient={fixed.data === undefined ? null : { id: fixed.data.id, name: fixed.data.name }}
      queryClientId={queryClientId}
    />
  );
}

function EditorSkeleton() {
  return (
    <div role="status" aria-label="A carregar plano de treino…" className="space-y-4">
      <Skeleton className="h-10 w-72" />
      <Skeleton className="h-80 w-full max-w-3xl" />
    </div>
  );
}

/**
 * Estado do rascunho, gravação única e aviso de alterações por guardar.
 *
 * Gravação: plano novo faz `POST` (cabeçalho e estrutura num pedido); plano sem histórico faz
 * `PUT` com a estrutura inteira, conservando os IDs; plano com histórico faz `PATCH` só dos
 * metadados. Um 409 `training_structure_has_history` ou `has_history` no servidor torna
 * estrutura e datas só de consulta e repõe-nas a partir do servidor (para o `PATCH` seguinte
 * não reenviar a data alterada); nome, descrição, modalidade e notas por gravar mantêm-se.
 *
 * O aviso (`useUnsavedChangesGuard`) fica ativo com o rascunho alterado, mas navegar para um
 * dos dois passos prossegue sozinho. Depois de gravar, `saved` desativa-o antes de navegar.
 */
function TrainingPlanEditorShell({
  planId,
  plan,
  fixedClient,
  queryClientId,
}: {
  planId: string | undefined;
  plan: TrainingDetails | null;
  fixedClient: ClientChoice | null;
  queryClientId: string | null;
}) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const mode = plan === null ? 'create' : 'edit';
  const search = queryClientId === null ? '' : `?client_id=${encodeURIComponent(queryClientId)}`;
  const basePath = plan === null ? `${LIST_PATH}/new` : `${LIST_PATH}/${plan.id}/edit`;
  const backTo =
    plan !== null
      ? `${LIST_PATH}/${plan.id}${search}`
      : queryClientId !== null
        ? `/trainer/clients/${queryClientId}?tab=training`
        : LIST_PATH;
  const backLabel =
    plan !== null
      ? 'Voltar ao plano'
      : fixedClient !== null
        ? `Voltar à ficha de ${fixedClient.name}`
        : 'Voltar a planos de treino';
  const clientLocked = mode === 'edit' || fixedClient !== null;

  const [draft, setDraft] = useState<TrainingDraft>(() =>
    plan !== null
      ? draftFromPlan(plan, fixedClient)
      : {
          client: fixedClient,
          name: '',
          description: '',
          modality: '',
          notes: '',
          startDate: '',
          endDate: '',
          structure: EMPTY_STRUCTURE,
        }
  );
  const [exerciseNames, setExerciseNames] = useState<Record<string, string>>(() =>
    plan !== null ? namesFromPlan(plan) : {}
  );
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState('');
  const [historyConflict, setHistoryConflict] = useState(false);
  const lockedFor = useRef<string | null>(plan?.has_history === true ? plan.id : null);

  useEffect(() => {
    // Quando o histórico torna estrutura e datas só de consulta a meio da edição (409, série
    // registada), repõe-nas a partir do servidor; o resto do rascunho fica como está.
    if (plan === null || !plan.has_history || lockedFor.current === plan.id) return;
    lockedFor.current = plan.id;
    setDraft((previous) => ({
      ...previous,
      startDate: plan.start_date,
      endDate: plan.end_date ?? '',
      structure: structureFromDetails(plan),
    }));
    setExerciseNames((previous) => ({ ...previous, ...namesFromPlan(plan) }));
  }, [plan]);

  const hasHistory = plan?.has_history === true || historyConflict;
  const readOnly = plan?.is_archived === true;
  const canChangeStructure = !readOnly && !hasHistory;
  const canChangeDates = !readOnly && !hasHistory;

  const save = useMutation({
    mutationFn: async () => {
      const metadata = {
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        training_modality: draft.modality.trim() || null,
        notes: draft.notes.trim() || null,
        start_date: draft.startDate,
        end_date: draft.endDate || null,
      };

      if (plan === null) {
        if (draft.client === null) throw new Error('client_required');
        return unwrap(
          await apiClient.POST('/api/v1/training-plans', {
            body: { client_id: draft.client.id, ...metadata, structure: draft.structure },
          })
        );
      }
      if (hasHistory) {
        return unwrap(
          await apiClient.PATCH('/api/v1/training-plans/{trainingPlanId}', {
            params: { path: { trainingPlanId: plan.id } },
            body: metadata,
          })
        );
      }

      return unwrap(
        await apiClient.PUT('/api/v1/training-plans/{trainingPlanId}', {
          params: { path: { trainingPlanId: plan.id } },
          body: { ...metadata, structure: draft.structure },
        })
      );
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.training });
      toast.success('Plano de treino guardado.');
      setSaved(true);
    },
    onError: (failure) => {
      setError(trainingError(failure));
      if (isApiProblem(failure) && failure.code === 'training_structure_has_history') {
        setHistoryConflict(true);
        void queryClient.invalidateQueries({
          queryKey: prescriptionKeys.trainingDetail(planId ?? ''),
        });
      }
    },
  });

  const blocker = useUnsavedChangesGuard(dirty && !saved);
  useEffect(() => {
    if (blocker.state === 'blocked' && isInsideEditor(basePath, blocker.location.pathname))
      blocker.proceed();
  }, [blocker, basePath]);
  useEffect(() => {
    // Só depois do render sem «alterações por guardar» é que se sai.
    if (saved) void navigate(backTo, { replace: true });
  }, [saved, navigate, backTo]);

  const detailsErrors = validateDetails(
    {
      clientId: draft.client?.id ?? null,
      name: draft.name,
      startDate: draft.startDate,
      endDate: draft.endDate,
    },
    mode === 'create' && !clientLocked
  );

  const context: TrainingEditorContext = {
    mode,
    basePath,
    search,
    clientLocked,
    draft,
    detailsErrors,
    patch: (change) => {
      setDirty(true);
      setDraft((previous) => ({ ...previous, ...change }));
    },
    changeStructure: (change) => {
      setDirty(true);
      setDraft((previous) => {
        const next: TrainingStructure = structuredClone(previous.structure);
        change(next);
        return { ...previous, structure: next };
      });
    },
    exerciseNames,
    rememberExercise: (id, label) => setExerciseNames((previous) => ({ ...previous, [id]: label })),
    readOnly,
    hasHistory,
    canChangeStructure,
    canChangeDates,
    save: () => {
      setError('');
      if (
        Object.keys(detailsErrors).length > 0 ||
        (canChangeStructure &&
          (hasRepsRangeError(draft.structure) || hasEmptyExercise(draft.structure)))
      ) {
        setError('Revê os detalhes, os exercícios e as repetições antes de guardar.');
        return;
      }
      void save.mutateAsync().catch(() => undefined);
    },
    saving: save.isPending,
    error,
    setError,
  };

  const step = location.pathname.endsWith('/structure') ? 2 : 1;

  return (
    <section className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
        <Link to={backTo}>
          <ArrowLeft aria-hidden /> {backLabel}
        </Link>
      </Button>
      <PageHeader
        title={mode === 'create' ? 'Criar plano de treino' : 'Editar plano de treino'}
        description={
          plan === null
            ? 'Prescreve semanas, dias, exercícios e séries em dois passos.'
            : `${plan.name} · ${fixedClient?.name ?? ''}`
        }
      />
      <StepIndicator step={step} steps={['Detalhes', 'Estrutura']} />
      {readOnly && <p role="status">Plano arquivado. Só consulta.</p>}
      {hasHistory && !readOnly && (
        <p role="status">Este plano já tem histórico. A estrutura e as datas são só de consulta.</p>
      )}
      <Outlet context={context} />
      <ConfirmDialog
        open={blocker.state === 'blocked' && !isInsideEditor(basePath, blocker.location.pathname)}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title="Sair sem guardar?"
        description="As alterações a este plano perdem-se."
        confirmLabel="Sair sem guardar"
        cancelLabel="Continuar a editar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </section>
  );
}

/**
 * Passo 1 · Detalhes: cliente, nome, descrição, modalidade, notas e datas.
 *
 * «Seguinte» valida e navega para `structure`. Num plano existente há também «Guardar», para
 * mudar só os detalhes sem passar pela estrutura.
 */
export function TrainingPlanDetailsStep() {
  const editor = useTrainingEditor();
  const navigate = useNavigate();
  const [attempted, setAttempted] = useState(false);
  const errors = attempted ? editor.detailsErrors : {};
  const valid = Object.keys(editor.detailsErrors).length === 0;
  const { draft, readOnly } = editor;

  return (
    <form
      className="space-y-6"
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        setAttempted(true);
        if (valid)
          void navigate({ pathname: `${editor.basePath}/structure`, search: editor.search });
      }}
    >
      <div className="border-border space-y-4 rounded-xl border p-4 sm:p-5">
        <FormField label="Cliente" error={errors.client}>
          {(control) =>
            editor.clientLocked ? (
              <Input {...control} className={touch} value={draft.client?.name ?? ''} disabled />
            ) : (
              <ClientCombobox
                {...control}
                value={draft.client}
                onChange={(client) => editor.patch({ client })}
              />
            )
          }
        </FormField>
        <FormField label="Nome" error={errors.name}>
          {(control) => (
            <Input
              {...control}
              className={touch}
              value={draft.name}
              onChange={(event) => editor.patch({ name: event.target.value })}
              maxLength={255}
              disabled={readOnly}
            />
          )}
        </FormField>
        <FormField label="Descrição">
          {(control) => (
            <Textarea
              {...control}
              rows={3}
              value={draft.description}
              onChange={(event) => editor.patch({ description: event.target.value })}
              disabled={readOnly}
            />
          )}
        </FormField>
        <FormField label="Modalidade">
          {(control) => (
            <Input
              {...control}
              className={touch}
              value={draft.modality}
              onChange={(event) => editor.patch({ modality: event.target.value })}
              disabled={readOnly}
            />
          )}
        </FormField>
        <FormField label="Notas">
          {(control) => (
            <Textarea
              {...control}
              rows={3}
              value={draft.notes}
              onChange={(event) => editor.patch({ notes: event.target.value })}
              disabled={readOnly}
            />
          )}
        </FormField>
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Data de início" error={errors.startDate}>
            {(control) => (
              <Input
                {...control}
                type="date"
                className={touch}
                value={draft.startDate}
                onChange={(event) => editor.patch({ startDate: event.target.value })}
                disabled={!editor.canChangeDates}
              />
            )}
          </FormField>
          <FormField label="Data de fim" error={errors.endDate}>
            {(control) => (
              <Input
                {...control}
                type="date"
                className={touch}
                value={draft.endDate}
                onChange={(event) => editor.patch({ endDate: event.target.value })}
                disabled={!editor.canChangeDates}
              />
            )}
          </FormField>
        </div>
      </div>
      <SaveError message={editor.error} />
      <div className="flex flex-wrap justify-end gap-2">
        {editor.mode === 'edit' && !readOnly && (
          <Button
            type="button"
            variant="outline"
            className={touch}
            disabled={editor.saving}
            onClick={() => {
              setAttempted(true);
              if (valid) editor.save();
            }}
          >
            {editor.saving ? 'A guardar…' : 'Guardar'}
          </Button>
        )}
        <Button type="submit" className={touch}>
          Seguinte
        </Button>
      </div>
    </form>
  );
}

/**
 * Passo 2 · Estrutura: semanas, dias, exercícios, séries e supersséries.
 *
 * Num plano novo sem o passo 1 válido (por exemplo, URL aberto diretamente) volta ao passo 1.
 * «Criar»/«Guardar» faz a única gravação; «Anterior» volta sem perder o rascunho.
 */
export function TrainingPlanStructureStep() {
  const editor = useTrainingEditor();
  const navigate = useNavigate();
  const [attempted, setAttempted] = useState(false);

  if (Object.keys(editor.detailsErrors).length > 0)
    return <Navigate to={{ pathname: editor.basePath, search: editor.search }} replace />;

  return (
    <form
      className="space-y-6"
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        setAttempted(true);
        if (hasRepsRangeError(editor.draft.structure) || hasEmptyExercise(editor.draft.structure)) {
          editor.setError(
            hasRepsRangeError(editor.draft.structure)
              ? 'Corrige as repetições: o máximo tem de ser maior que o mínimo.'
              : 'Escolhe o exercício em todas as linhas.'
          );
          return;
        }
        editor.save();
      }}
    >
      <TrainingStructureEditor
        structure={editor.draft.structure}
        canChange={editor.canChangeStructure}
        exerciseNames={editor.exerciseNames}
        onExerciseChosen={editor.rememberExercise}
        onChange={editor.changeStructure}
        showErrors={attempted}
      />
      <SaveError message={editor.error} />
      <div className="flex flex-wrap justify-between gap-2">
        <Button
          type="button"
          variant="outline"
          className={touch}
          onClick={() => void navigate({ pathname: editor.basePath, search: editor.search })}
        >
          Anterior
        </Button>
        {editor.canChangeStructure && (
          <Button type="submit" className={touch} disabled={editor.saving}>
            {editor.saving ? 'A guardar…' : editor.mode === 'create' ? 'Criar' : 'Guardar'}
          </Button>
        )}
      </div>
    </form>
  );
}

/**
 * Erro de guardar junto aos botões do passo, e não no topo da página: numa estrutura longa quem
 * carrega em «Criar» tem de o ver. Ao aparecer, a página desliza até ele.
 */
function SaveError({ message }: { message: string }) {
  const ref = useRef<HTMLParagraphElement>(null);

  useEffect(() => {
    if (message !== '') ref.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  }, [message]);

  if (message === '') return null;

  return (
    <p ref={ref} role="alert" className="text-destructive text-sm">
      {message}
    </p>
  );
}
