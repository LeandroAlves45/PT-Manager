import { useMutation, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { ArrowLeft, Dumbbell } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';

import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { useSetLogsQuery, useTrainingPlanQuery } from '@/features/prescriptions/api/training';
import {
  plannedSetText,
  trainingError,
  WEEKDAYS,
  type TrainingDetails,
} from '@/features/prescriptions/lib/training-draft';
import { doneSetText, type SetLog } from '@/features/prescriptions/lib/training-logs';
import { apiClient, unwrap } from '@/shared/api/client';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';

const touch = 'min-h-11 md:min-h-9';
const MAX_WEIGHT_KG = 1000;
const MAX_REPS = 100;

/** Peso em kg (0 a 1000, até duas casas; vírgula ou ponto), ou `null` se for inválido. */
function parseWeight(text: string): number | null {
  const normalized = text.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) return null;

  const value = Number(normalized);
  return value <= MAX_WEIGHT_KG ? value : null;
}

/** Repetições feitas (inteiro de 0 a 100), ou `null` se forem inválidas. */
function parseRepsDone(text: string): number | null {
  if (!/^\d+$/.test(text.trim())) return null;

  const value = Number(text.trim());
  return value <= MAX_REPS ? value : null;
}

/** RPE opcional: vazio é `null`; senão 1 a 10 em passos de 0,5. `undefined` se for inválido. */
function parseRpe(text: string): number | null | undefined {
  if (text.trim() === '') return null;

  const value = Number(text.trim().replace(',', '.'));
  return Number.isFinite(value) && value >= 1 && value <= 10 && value * 2 === Math.round(value * 2)
    ? value
    : undefined;
}

const inputDate = (value: Date) => format(value, "yyyy-MM-dd'T'HH:mm");

/**
 * Corrige (ou regista) uma série realizada: `.../training-plans/:planId/set-logs/:logId` (U53).
 *
 * O trainer corrige o registo do cliente, também de dias anteriores, mas não o apaga
 * (RN-007/FL-003). Com `:logId` igual a `new` regista uma série que o cliente apagou:
 * `?set=<exerciseSetId>` pré-escolhe o exercício e o número da série. `?client_id=` e
 * `?page=` (a página da lista de onde se veio) conservam-se no regresso e na procura do
 * registo. Plano arquivado: só consulta.
 */
export function SetLogCorrectionPage() {
  const { planId = '', logId = '' } = useParams();
  const [searchParams] = useSearchParams();
  const queryClientId = searchParams.get('client_id');
  const clientSearch =
    queryClientId === null ? '' : `?client_id=${encodeURIComponent(queryClientId)}`;
  const backTo = `/trainer/training-plans/${planId}${clientSearch}`;
  const plan = useTrainingPlanQuery(planId);
  const creating = logId === 'new';
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  const logs = useSetLogsQuery(creating ? undefined : plan.data?.client_id, planId, page);

  const back = (
    <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
      <Link to={backTo}>
        <ArrowLeft aria-hidden /> Voltar ao plano
      </Link>
    </Button>
  );
  const loading = (
    <div role="status" aria-label="A carregar série…" className="space-y-3">
      <Skeleton className="h-10 w-72" />
      <Skeleton className="h-64 w-full max-w-xl" />
    </div>
  );

  let content;
  if (plan.isError) {
    content = <ErrorState error={plan.error} onRetry={() => void plan.refetch()} />;
  } else if (plan.data === undefined) {
    content = loading;
  } else if (plan.data.is_archived) {
    content = (
      <EmptyState
        icon={Dumbbell}
        title="Plano arquivado"
        description="Um plano arquivado é só de consulta: as séries já não se corrigem."
      />
    );
  } else if (creating) {
    content = (
      <LogForm plan={plan.data} log={null} presetSetId={searchParams.get('set')} backTo={backTo} />
    );
  } else if (logs.isError) {
    content = <ErrorState error={logs.error} onRetry={() => void logs.refetch()} />;
  } else if (logs.data === undefined) {
    content = loading;
  } else {
    const log = logs.data.items.find((item) => item.id === logId);
    content =
      log === undefined ? (
        <EmptyState
          icon={Dumbbell}
          title="Registo não encontrado"
          description="Este registo já não existe ou está noutra página da lista."
        />
      ) : (
        <LogForm key={log.id} plan={plan.data} log={log} presetSetId={null} backTo={backTo} />
      );
  }

  return (
    <section className="space-y-6">
      {back}
      {content}
    </section>
  );
}

/**
 * Formulário de correção (`log`) ou de registo (`log === null`).
 *
 * Peso, repetições e data começam vazios no registo, para não gravar um 0 acidental. Depois
 * de gravar, `saved` desativa o aviso de alterações por guardar antes de voltar ao plano.
 */
function LogForm({
  plan,
  log,
  presetSetId,
  backTo,
}: {
  plan: TrainingDetails;
  log: SetLog | null;
  presetSetId: string | null;
  backTo: string;
}) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const exercises = plan.days.flatMap((day) =>
    day.exercises.map((exercise) => ({
      exercise,
      label: `Semana ${day.week_number} · ${WEEKDAYS[day.day_of_week] ?? ''} · ${exercise.exercise_name}`,
    }))
  );
  const preset =
    presetSetId === null
      ? undefined
      : exercises.find(({ exercise }) => exercise.sets.some((set) => set.id === presetSetId));
  const presetSet = preset?.exercise.sets.find((set) => set.id === presetSetId);

  const [exerciseId, setExerciseId] = useState(preset?.exercise.id ?? '');
  const [setNumber, setSetNumber] = useState(String(presetSet?.set_number ?? 1));
  const [weight, setWeight] = useState(log === null ? '' : String(log.weight_kg).replace('.', ','));
  const [reps, setReps] = useState(log === null ? '' : String(log.reps_done));
  const [rpe, setRpe] = useState(log?.rpe == null ? '' : String(log.rpe).replace('.', ','));
  const [notes, setNotes] = useState(log?.notes ?? '');
  const [performedAt, setPerformedAt] = useState(
    log === null ? inputDate(new Date()) : inputDate(new Date(log.performed_at))
  );
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);
  const [attempted, setAttempted] = useState(false);
  const [error, setError] = useState('');

  const weightValue = parseWeight(weight);
  const repsValue = parseRepsDone(reps);
  const rpeValue = parseRpe(rpe);
  const setNumberValue = Number(setNumber);
  const errors = {
    exercise: log === null && exerciseId === '' ? 'Escolhe o exercício.' : undefined,
    setNumber:
      log === null &&
      !(Number.isInteger(setNumberValue) && setNumberValue >= 1 && setNumberValue <= 15)
        ? 'Indica a série (1 a 15).'
        : undefined,
    weight: weightValue === null ? 'Indica o peso (0 a 1000 kg).' : undefined,
    reps: repsValue === null ? 'Indica as repetições (0 a 100).' : undefined,
    rpe: rpeValue === undefined ? 'O RPE vai de 1 a 10, de 0,5 em 0,5.' : undefined,
    performedAt: performedAt === '' ? 'Indica a data e a hora.' : undefined,
  };
  const valid = Object.values(errors).every((message) => message === undefined);
  const shown = (key: keyof typeof errors) => (attempted ? errors[key] : undefined);
  const edit =
    <T,>(setter: (value: T) => void) =>
    (value: T) => {
      setDirty(true);
      setter(value);
    };

  const save = useMutation({
    mutationFn: async () => {
      const values = {
        weight_kg: weightValue ?? 0,
        reps_done: repsValue ?? 0,
        rpe: rpeValue ?? null,
        notes: notes.trim() || null,
        performed_at: new Date(performedAt).toISOString(),
      };
      if (log !== null) {
        return unwrap(
          await apiClient.PATCH('/api/v1/exercise-set-logs/{exerciseSetLogId}', {
            params: { path: { exerciseSetLogId: log.id } },
            body: values,
          })
        );
      }
      return unwrap(
        await apiClient.POST('/api/v1/exercise-set-logs', {
          body: {
            ...values,
            training_plan_day_exercise_id: exerciseId,
            set_number: setNumberValue,
          },
        })
      );
    },
    onSuccess: async () => {
      // Primeiro `saved` (desliga o aviso e navega), depois a invalidação: se a correção mudar
      // o registo de página, o formulário deixaria de existir antes de navegar.
      setSaved(true);
      toast.success(log === null ? 'Série registada.' : 'Série corrigida.');
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: prescriptionKeys.logsAll }),
        queryClient.invalidateQueries({ queryKey: prescriptionKeys.trainingDetail(plan.id) }),
      ]);
    },
    onError: (failure) => setError(trainingError(failure)),
  });

  const blocker = useUnsavedChangesGuard(dirty && !saved);
  useEffect(() => {
    if (saved) void navigate(backTo, { replace: true });
  }, [saved, navigate, backTo]);

  const planned =
    log === null
      ? undefined
      : exercises
          .find(({ exercise }) => exercise.id === log.training_plan_day_exercise_id)
          ?.exercise.sets.find((set) => set.set_number === log.set_number);

  return (
    <>
      <PageHeader
        title={log === null ? 'Registar série' : 'Corrigir série'}
        description={
          log === null
            ? 'Regista uma série que o cliente não tem registada.'
            : `${log.exercise_name} · Série ${log.set_number} · feito ${doneSetText(log)}${
                planned === undefined ? '' : ` · plano ${plannedSetText(planned)}`
              }`
        }
      />
      <form
        className="border-border max-w-xl space-y-4 rounded-xl border p-4 sm:p-5"
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          setError('');
          setAttempted(true);
          if (valid) void save.mutateAsync().catch(() => undefined);
        }}
      >
        {error !== '' && (
          <p role="alert" className="text-destructive">
            {error}
          </p>
        )}
        {log === null && (
          <>
            <FormField label="Exercício" error={shown('exercise')}>
              {(control) => (
                <NativeSelect
                  {...control}
                  className={touch}
                  value={exerciseId}
                  onChange={(event) => edit(setExerciseId)(event.target.value)}
                >
                  <option value="">Escolhe um exercício</option>
                  {exercises.map(({ exercise, label }) => (
                    <option key={exercise.id} value={exercise.id}>
                      {label}
                    </option>
                  ))}
                </NativeSelect>
              )}
            </FormField>
            <FormField label="Número da série" error={shown('setNumber')}>
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={1}
                  max={15}
                  className={touch}
                  value={setNumber}
                  onChange={(event) => edit(setSetNumber)(event.target.value)}
                />
              )}
            </FormField>
          </>
        )}
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Peso (kg)" error={shown('weight')}>
            {(control) => (
              <Input
                {...control}
                inputMode="decimal"
                className={touch}
                value={weight}
                onChange={(event) => edit(setWeight)(event.target.value)}
              />
            )}
          </FormField>
          <FormField label="Repetições feitas" error={shown('reps')}>
            {(control) => (
              <Input
                {...control}
                inputMode="numeric"
                className={touch}
                value={reps}
                onChange={(event) => edit(setReps)(event.target.value)}
              />
            )}
          </FormField>
          <FormField label="RPE" error={shown('rpe')}>
            {(control) => (
              <Input
                {...control}
                inputMode="decimal"
                className={touch}
                value={rpe}
                onChange={(event) => edit(setRpe)(event.target.value)}
              />
            )}
          </FormField>
          <FormField label="Data e hora" error={shown('performedAt')}>
            {(control) => (
              <Input
                {...control}
                type="datetime-local"
                className={touch}
                value={performedAt}
                onChange={(event) => edit(setPerformedAt)(event.target.value)}
              />
            )}
          </FormField>
        </div>
        <FormField label="Notas">
          {(control) => (
            <Input
              {...control}
              maxLength={500}
              className={touch}
              value={notes}
              onChange={(event) => edit(setNotes)(event.target.value)}
            />
          )}
        </FormField>
        <div className="flex flex-wrap justify-end gap-2">
          <Button asChild variant="outline" className={touch}>
            <Link to={backTo}>Cancelar</Link>
          </Button>
          <Button type="submit" className={touch} disabled={save.isPending}>
            {save.isPending ? 'A guardar…' : log === null ? 'Registar' : 'Guardar'}
          </Button>
        </div>
      </form>
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title="Sair sem guardar?"
        description="As alterações a esta série perdem-se."
        confirmLabel="Sair sem guardar"
        cancelLabel="Continuar a editar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </>
  );
}
