import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';

import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import {
  CatalogPicker,
  type CatalogChoice,
} from '@/features/prescriptions/components/CatalogPicker';
import { SetLogPanel } from '@/features/prescriptions/components/SetLogPanel';
import { prescriptionError } from '@/features/prescriptions/lib/errors';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';

type Details = components['schemas']['TrainingPlanDetailsResponse'];
type Structure = components['schemas']['TrainingPlanStructureRequest'];
type Day = components['schemas']['TrainingDayRequest'];
type Exercise = components['schemas']['DayExerciseRequest'];
type Set = components['schemas']['ExerciseSetRequest'];

const DAYS = ['Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado', 'Domingo'];
const EMPTY: Structure = { days: [] };

function fromDetails(plan: Details): Structure {
  return {
    days: plan.days.map((day) => ({
      id: day.id,
      week_number: day.week_number,
      day_of_week: day.day_of_week,
      notes: day.notes,
      exercises: day.exercises.map((exercise) => ({
        id: exercise.id,
        exercise_id: exercise.exercise_id,
        order_number: exercise.order_number,
        exercise_group_id: exercise.exercise_group_id,
        group_position: exercise.group_position,
        notes: exercise.notes,
        sets: exercise.sets.map((set) => ({
          id: set.id,
          set_number: set.set_number,
          planned_reps: set.planned_reps,
          planned_weight_kg: set.planned_weight_kg,
          rest_seconds_min: set.rest_seconds_min,
          rest_seconds_max: set.rest_seconds_max,
          planned_rpe: set.planned_rpe,
        })),
      })),
    })),
  };
}

function newSet(number: number): Set {
  return {
    id: null,
    set_number: number,
    planned_reps: null,
    planned_weight_kg: null,
    rest_seconds_min: null,
    rest_seconds_max: null,
    planned_rpe: null,
  };
}

function newExercise(number: number): Exercise {
  return {
    id: null,
    exercise_id: '',
    order_number: number,
    exercise_group_id: null,
    group_position: null,
    notes: null,
    sets: [newSet(1)],
  };
}

function nullableNumber(value: string): number | null {
  return value.trim() === '' ? null : Number(value);
}

/** Editor do plano de treino, IDs antigos mantêm-se na reconcialização. */
export function TrainingPlanEditor({
  planId,
  fixedClient,
  onClose,
}: {
  planId: string | null;
  fixedClient: ClientChoice | null;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const detail = useQuery({
    queryKey: prescriptionKeys.trainingDetail(planId ?? ''),
    enabled: planId !== null,
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/training-plans/{trainingPlanId}', {
          params: { path: { trainingPlanId: planId ?? '' } },
          signal,
        })
        .then(unwrap),
  });
  const [client, setClient] = useState<ClientChoice | null>(fixedClient);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [modality, setModality] = useState('');
  const [notes, setNotes] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [structure, setStructure] = useState<Structure>(EMPTY);
  const [exerciseNames, setExerciseNames] = useState<Record<string, string>>({});
  const [error, setError] = useState('');
  const [historyConflict, setHistoryConflict] = useState(false);
  const initializedFor = useRef<string | null>(null);

  useEffect(() => {
    // O detalhe inicializa o rascunho uma única vez por plano. Um refetch (409 de
    // histórico, série registada, foco da janela) não pode apagar edições por guardar;
    // has_history e is_archived continuam a ler-se do servidor em cada render.
    if (detail.data === undefined || initializedFor.current === detail.data.id) return;

    const plan = detail.data;
    initializedFor.current = plan.id;
    setName(plan.name);
    setDescription(plan.description ?? '');
    setModality(plan.training_modality ?? '');
    setNotes(plan.notes ?? '');
    setStartDate(plan.start_date);
    setEndDate(plan.end_date ?? '');
    setStructure(fromDetails(plan));
    setExerciseNames(
      Object.fromEntries(
        plan.days.flatMap((day) =>
          day.exercises.map((exercise) => [exercise.exercise_id, exercise.exercise_name])
        )
      )
    );
  }, [detail.data]);

  const hasHistory = detail.data?.has_history === true || historyConflict;
  const readOnly = detail.data?.is_archived === true;
  const canChangeStructure = !readOnly && !hasHistory;
  const canChangeDates = !readOnly && !hasHistory;

  function changeStructure(change: (draft: Structure) => void) {
    setStructure((previous) => {
      const next = structuredClone(previous);
      change(next);
      return next;
    });
  }

  const save = useMutation({
    mutationFn: async () => {
      const metadata = {
        name: name.trim(),
        description: description.trim() || null,
        training_modality: modality.trim() || null,
        notes: notes.trim() || null,
        start_date: startDate,
        end_date: endDate || null,
      };

      if (planId === null) {
        if (client === null) throw new Error('client_required');
        return unwrap(
          await apiClient.POST('/api/v1/training-plans', {
            body: { client_id: client.id, ...metadata, structure },
          })
        );
      }
      if (hasHistory)
        return unwrap(
          await apiClient.PATCH('/api/v1/training-plans/{trainingPlanId}', {
            params: { path: { trainingPlanId: planId } },
            body: metadata,
          })
        );

      return unwrap(
        await apiClient.PUT('/api/v1/training-plans/{trainingPlanId}', {
          params: { path: { trainingPlanId: planId } },
          body: { ...metadata, structure },
        })
      );
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.training });
      toast.success('Plano de treino guardado.');
      onClose();
    },
    onError: (failure) => {
      setError(prescriptionError(failure));
      if (isApiProblem(failure) && failure.code === 'training_structure_has_history') {
        setHistoryConflict(true);
        void detail.refetch();
      }
    },
  });

  if (detail.isPending && planId !== null) {
    return (
      <Skeleton role="status" aria-label="A carregar plano de treino…" className="h-80 w-full" />
    );
  }
  if (detail.isError && planId !== null) {
    return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="font-display text-2xl">{planId === null ? 'Novo plano de treino' : name}</h2>
        <Button variant="outline" onClick={onClose}>
          Fechar
        </Button>
      </div>
      {readOnly && <p role="status">Plano arquivado. Só consulta.</p>}
      {hasHistory && (
        <p role="status">Este plano já tem histórico. A estrutura e as datas são só de consulta.</p>
      )}
      {error !== '' && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
      <form
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault();
          setError('');
          if ((planId === null && client === null) || name.trim() === '' || startDate === '') {
            setError('Escolhe o cliente, o nome e a data de início.');
            return;
          }
          void save.mutateAsync().catch(() => undefined);
        }}
      >
        {planId === null && fixedClient === null && (
          <FormField label="Cliente">
            {(control) => <ClientCombobox {...control} value={client} onChange={setClient} />}
          </FormField>
        )}
        <FormField label="Nome">
          {(control) => (
            <Input
              {...control}
              value={name}
              onChange={(event) => setName(event.target.value)}
              maxLength={255}
              required
              disabled={readOnly}
            />
          )}
        </FormField>
        <FormField label="Descrição">
          {(control) => (
            <Input
              {...control}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              disabled={readOnly}
            />
          )}
        </FormField>
        <FormField label="Modalidade">
          {(control) => (
            <Input
              {...control}
              value={modality}
              onChange={(event) => setModality(event.target.value)}
              disabled={readOnly}
            />
          )}
        </FormField>
        <FormField label="Notas">
          {(control) => (
            <Input
              {...control}
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              disabled={readOnly}
            />
          )}
        </FormField>
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Data de início">
            {(control) => (
              <Input
                {...control}
                type="date"
                value={startDate}
                onChange={(event) => setStartDate(event.target.value)}
                required
                disabled={!canChangeDates}
              />
            )}
          </FormField>
          <FormField label="Data de fim">
            {(control) => (
              <Input
                {...control}
                type="date"
                value={endDate}
                onChange={(event) => setEndDate(event.target.value)}
                disabled={!canChangeDates}
              />
            )}
          </FormField>
        </div>
        <section className="space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="font-medium">Semanas e dias</h3>
            {canChangeStructure && (
              <Button
                type="button"
                variant="outline"
                onClick={() =>
                  changeStructure((draft) =>
                    draft.days.push({
                      id: null,
                      week_number: 1,
                      day_of_week: 0,
                      notes: null,
                      exercises: [],
                    })
                  )
                }
              >
                Adicionar dia
              </Button>
            )}
          </div>
          {structure.days.map((day: Day, dayIndex: number) => (
            <div
              key={day.id ?? 'new-day-' + dayIndex}
              className="border-border space-y-4 rounded-xl border p-4"
            >
              <div className="grid gap-3 sm:grid-cols-3">
                <FormField label="Semana">
                  {(control) => (
                    <Input
                      {...control}
                      type="number"
                      min={1}
                      max={52}
                      value={day.week_number}
                      disabled={!canChangeStructure}
                      onChange={(event) =>
                        changeStructure((draft) => {
                          draft.days[dayIndex]!.week_number = Number(event.target.value);
                        })
                      }
                    />
                  )}
                </FormField>
                <FormField label="Dia da semana">
                  {(control) => (
                    <select
                      {...control}
                      className="border-input bg-background h-9 rounded-md border px-3"
                      value={day.day_of_week}
                      disabled={!canChangeStructure}
                      onChange={(event) =>
                        changeStructure((draft) => {
                          draft.days[dayIndex]!.day_of_week = Number(event.target.value);
                        })
                      }
                    >
                      {DAYS.map((label, index) => (
                        <option key={label} value={index}>
                          {label}
                        </option>
                      ))}
                    </select>
                  )}
                </FormField>
                {canChangeStructure && (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() =>
                      changeStructure((draft) => {
                        draft.days.splice(dayIndex, 1);
                      })
                    }
                  >
                    Remover dia
                  </Button>
                )}
              </div>
              <FormField label="Notas do dia">
                {(control) => (
                  <Input
                    {...control}
                    value={day.notes ?? ''}
                    disabled={!canChangeStructure}
                    onChange={(event) =>
                      changeStructure((draft) => {
                        draft.days[dayIndex]!.notes = event.target.value || null;
                      })
                    }
                  />
                )}
              </FormField>
              {day.exercises.map((exercise: Exercise, exerciseIndex: number) => (
                <div
                  key={exercise.id ?? 'new-exercise-' + exerciseIndex}
                  className="bg-muted/30 space-y-3 rounded-lg p-3"
                >
                  <FormField label={'Exercício ' + (exerciseIndex + 1)}>
                    {(control) => (
                      <CatalogPicker
                        id={control.id}
                        kind="exercises"
                        value={
                          exercise.exercise_id === ''
                            ? null
                            : {
                                value: exercise.exercise_id,
                                label:
                                  exerciseNames[exercise.exercise_id] ?? 'Exercício selecionado',
                              }
                        }
                        disabled={!canChangeStructure}
                        onChange={(choice: CatalogChoice) => {
                          setExerciseNames((previous) => ({
                            ...previous,
                            [choice.value]: choice.label,
                          }));
                          changeStructure((draft) => {
                            draft.days[dayIndex]!.exercises[exerciseIndex]!.exercise_id =
                              choice.value;
                          });
                        }}
                      />
                    )}
                  </FormField>
                  <FormField label="Notas do exercício">
                    {(control) => (
                      <Input
                        {...control}
                        value={exercise.notes ?? ''}
                        disabled={!canChangeStructure}
                        onChange={(event) =>
                          changeStructure((draft) => {
                            draft.days[dayIndex]!.exercises[exerciseIndex]!.notes =
                              event.target.value || null;
                          })
                        }
                      />
                    )}
                  </FormField>
                  {exercise.exercise_group_id !== null && (
                    <p>Supersérie, posição {exercise.group_position}</p>
                  )}
                  {canChangeStructure &&
                    exerciseIndex > 0 &&
                    exercise.exercise_group_id === null && (
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() =>
                          changeStructure((draft) => {
                            const items = draft.days[dayIndex]!.exercises;
                            const previous = items[exerciseIndex - 1]!;
                            const current = items[exerciseIndex]!;
                            const groupId = previous.exercise_group_id ?? crypto.randomUUID();
                            previous.exercise_group_id = groupId;
                            previous.group_position = previous.group_position ?? 1;
                            current.exercise_group_id = groupId;
                            current.group_position = previous.group_position + 1;
                          })
                        }
                      >
                        Agrupar com anterior
                      </Button>
                    )}
                  {canChangeStructure && exercise.exercise_group_id !== null && (
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() =>
                        changeStructure((draft) => {
                          const item = draft.days[dayIndex]!.exercises[exerciseIndex]!;
                          item.exercise_group_id = null;
                          item.group_position = null;
                        })
                      }
                    >
                      Retirar da supersérie
                    </Button>
                  )}
                  {exercise.sets.map((set: Set, setIndex: number) => (
                    <div
                      key={set.id ?? 'new-set-' + setIndex}
                      className="grid gap-2 sm:grid-cols-6"
                    >
                      <FormField label="Série">
                        {(control) => (
                          <Input
                            {...control}
                            type="number"
                            min={1}
                            max={15}
                            value={set.set_number}
                            disabled={!canChangeStructure}
                            onChange={(event) =>
                              changeStructure((draft) => {
                                draft.days[dayIndex]!.exercises[exerciseIndex]!.sets[
                                  setIndex
                                ]!.set_number = Number(event.target.value);
                              })
                            }
                          />
                        )}
                      </FormField>
                      {(
                        [
                          ['planned_reps', 'Repetições', 1],
                          ['planned_weight_kg', 'Peso (kg)', 0],
                          ['rest_seconds_min', 'Descanso mín. (s)', 0],
                          ['rest_seconds_max', 'Descanso máx. (s)', 0],
                          ['planned_rpe', 'RPE', 1],
                        ] as const
                      ).map(([field, label, minimum]) => (
                        <FormField key={field} label={label}>
                          {(control) => (
                            <Input
                              {...control}
                              type="number"
                              min={minimum}
                              max={field === 'planned_rpe' ? 10 : undefined}
                              step={
                                field === 'planned_rpe'
                                  ? 0.5
                                  : field === 'planned_weight_kg'
                                    ? 0.01
                                    : 1
                              }
                              value={set[field] ?? ''}
                              disabled={!canChangeStructure}
                              onChange={(event) =>
                                changeStructure((draft) => {
                                  draft.days[dayIndex]!.exercises[exerciseIndex]!.sets[setIndex]![
                                    field
                                  ] = nullableNumber(event.target.value);
                                })
                              }
                            />
                          )}
                        </FormField>
                      ))}
                      {canChangeStructure && (
                        <Button
                          type="button"
                          variant="outline"
                          onClick={() =>
                            changeStructure((draft) => {
                              draft.days[dayIndex]!.exercises[exerciseIndex]!.sets.splice(
                                setIndex,
                                1
                              );
                            })
                          }
                        >
                          Remover série
                        </Button>
                      )}
                    </div>
                  ))}
                  {canChangeStructure && (
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() =>
                          changeStructure((draft) => {
                            const sets = draft.days[dayIndex]!.exercises[exerciseIndex]!.sets;
                            sets.push(newSet(sets.length + 1));
                          })
                        }
                      >
                        Adicionar série
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() =>
                          changeStructure((draft) => {
                            draft.days[dayIndex]!.exercises.splice(exerciseIndex, 1);
                          })
                        }
                      >
                        Remover exercício
                      </Button>
                    </div>
                  )}
                </div>
              ))}
              {canChangeStructure && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() =>
                    changeStructure((draft) => {
                      const exercises = draft.days[dayIndex]!.exercises;
                      exercises.push(newExercise(exercises.length + 1));
                    })
                  }
                >
                  Adicionar exercício
                </Button>
              )}
            </div>
          ))}
        </section>
        {!readOnly && (
          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? 'A guardar…' : 'Guardar plano de treino'}
          </Button>
        )}
      </form>
      {planId !== null && detail.data !== undefined && <SetLogPanel plan={detail.data} />}
    </div>
  );
}
