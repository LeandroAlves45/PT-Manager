import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { useState } from 'react';
import { toast } from 'sonner';

import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { prescriptionError } from '@/features/prescriptions/lib/errors';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';

type Plan = components['schemas']['TrainingPlanDetailsResponse'];
type Log = components['schemas']['ExerciseSetLogResponse'];

const PAGE_SIZE = 25;

/** Registo e correção de séries efetuadas no contexto de um plano de treino. */
export function SetLogPanel({ plan }: { plan: Plan }) {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Log | null>(null);
  const [exerciseId, setExerciseId] = useState('');
  const [setNumber, setSetNumber] = useState(1);
  // Sem valor por omissão: um 0 kg/0 reps acidental seria um registo falso.
  const [weight, setWeight] = useState('');
  const [reps, setReps] = useState('');
  const [rpe, setRpe] = useState('');
  const [notes, setNotes] = useState('');
  const [performedAt, setPerformedAt] = useState(format(new Date(), "yyyy-MM-dd'T'HH:mm"));
  const [error, setError] = useState('');
  const exercises = plan.days.flatMap((day) => day.exercises);

  const logs = useQuery({
    queryKey: prescriptionKeys.logs(plan.client_id, plan.id, page),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/exercise-set-logs', {
          params: {
            query: {
              client_id: plan.client_id,
              training_plan_id: plan.id,
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
      const values = {
        weight_kg: Number(weight),
        reps_done: Number(reps),
        rpe: rpe === '' ? null : Number(rpe),
        notes: notes.trim() || null,
        performed_at: new Date(performedAt).toISOString(),
      };
      if (editing !== null) {
        return unwrap(
          await apiClient.PATCH('/api/v1/exercise-set-logs/{exerciseSetLogId}', {
            params: { path: { exerciseSetLogId: editing.id } },
            body: values,
          })
        );
      }
      return unwrap(
        await apiClient.POST('/api/v1/exercise-set-logs', {
          body: { ...values, training_plan_day_exercise_id: exerciseId, set_number: setNumber },
        })
      );
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: prescriptionKeys.logsAll }),
        queryClient.invalidateQueries({ queryKey: prescriptionKeys.trainingDetail(plan.id) }),
      ]);
      toast.success(editing === null ? 'Série registada.' : 'Série corrigida.');
      setEditing(null);
      setError('');
    },
    onError: (failure) => setError(prescriptionError(failure)),
  });

  function startCorrection(log: Log) {
    setEditing(log);
    setExerciseId(log.training_plan_day_exercise_id);
    setSetNumber(log.set_number);
    setWeight(String(log.weight_kg));
    setReps(String(log.reps_done));
    setRpe(log.rpe === null ? '' : String(log.rpe));
    setNotes(log.notes ?? '');
    setPerformedAt(format(new Date(log.performed_at), "yyyy-MM-dd'T'HH:mm"));
  }

  return (
    <section className="space-y-4">
      <h3 className="font-display text-xl">Séries realizadas</h3>
      {logs.isPending ? (
        <Skeleton role="status" aria-label="A carregar séries…" className="h-32 w-full" />
      ) : logs.isError ? (
        <ErrorState error={logs.error} onRetry={() => void logs.refetch()} />
      ) : logs.data.items.length === 0 ? (
        <p>Nenhuma série registada neste plano.</p>
      ) : (
        <>
          <ul className="space-y-2">
            {logs.data.items.map((log) => (
              <li
                key={log.id}
                className="border-border flex items-center justify-between rounded-md border p-3"
              >
                <span>
                  {log.exercise_name}, série {log.set_number}: {log.reps_done} reps, {log.weight_kg}{' '}
                  kg
                </span>
                {!plan.is_archived && (
                  <Button variant="outline" onClick={() => startCorrection(log)}>
                    Corrigir
                  </Button>
                )}
              </li>
            ))}
          </ul>
          <Pagination
            label="Páginas de séries"
            page={page}
            total={logs.data.total_count}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
          />
        </>
      )}
      {!plan.is_archived && (
        <form
          className="border-border grid gap-3 rounded-xl border p-4 sm:grid-cols-3"
          onSubmit={(event) => {
            event.preventDefault();
            setError('');
            if (
              exerciseId === '' ||
              weight.trim() === '' ||
              reps.trim() === '' ||
              !Number.isFinite(Number(weight)) ||
              !Number.isFinite(Number(reps)) ||
              performedAt === ''
            ) {
              setError('Escolhe o exercício e preenche os valores da série.');
              return;
            }
            void save.mutateAsync().catch(() => undefined);
          }}
        >
          <h4 className="sm:col-span-3">
            {editing === null ? 'Registar série' : 'Corrigir série'}
          </h4>
          {error !== '' && (
            <p role="alert" className="text-destructive sm:col-span-3">
              {error}
            </p>
          )}
          <FormField label="Exercício">
            {(control) => (
              <select
                {...control}
                className="border-input bg-background h-9 rounded-md border px-3"
                value={exerciseId}
                disabled={editing !== null}
                onChange={(event) => setExerciseId(event.target.value)}
                required
              >
                <option value="">Escolhe um exercício</option>
                {exercises.map((exercise) => (
                  <option key={exercise.id} value={exercise.id}>
                    {exercise.exercise_name}
                  </option>
                ))}
              </select>
            )}
          </FormField>
          <FormField label="Número da série">
            {(control) => (
              <Input
                {...control}
                type="number"
                min={1}
                max={15}
                value={setNumber}
                disabled={editing !== null}
                onChange={(event) => setSetNumber(Number(event.target.value))}
                required
              />
            )}
          </FormField>
          <FormField label="Data e hora">
            {(control) => (
              <Input
                {...control}
                type="datetime-local"
                value={performedAt}
                onChange={(event) => setPerformedAt(event.target.value)}
                required
              />
            )}
          </FormField>
          <FormField label="Peso (kg)">
            {(control) => (
              <Input
                {...control}
                type="number"
                min={0}
                step={0.01}
                value={weight}
                onChange={(event) => setWeight(event.target.value)}
                required
              />
            )}
          </FormField>
          <FormField label="Repetições feitas">
            {(control) => (
              <Input
                {...control}
                type="number"
                min={0}
                max={100}
                value={reps}
                onChange={(event) => setReps(event.target.value)}
                required
              />
            )}
          </FormField>
          <FormField label="RPE">
            {(control) => (
              <Input
                {...control}
                type="number"
                min={1}
                max={10}
                step={0.5}
                value={rpe}
                onChange={(event) => setRpe(event.target.value)}
              />
            )}
          </FormField>
          <FormField label="Notas">
            {(control) => (
              <Input
                {...control}
                maxLength={500}
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
              />
            )}
          </FormField>
          <div className="flex gap-2 sm:col-span-3">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? 'A guardar…' : editing === null ? 'Registar' : 'Guardar correção'}
            </Button>
            {editing !== null && (
              <Button type="button" variant="outline" onClick={() => setEditing(null)}>
                Cancelar correção
              </Button>
            )}
          </div>
        </form>
      )}
    </section>
  );
}
