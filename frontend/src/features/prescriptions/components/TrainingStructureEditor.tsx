import { Plus, Trash2 } from 'lucide-react';

import {
  CatalogPicker,
  type CatalogChoice,
} from '@/features/prescriptions/components/CatalogPicker';
import {
  newExercise,
  newSet,
  nullableNumber,
  repsRangeError,
  WEEKDAYS,
  type TrainingDayDraft,
  type TrainingExerciseDraft,
  type TrainingSetDraft,
  type TrainingStructure,
} from '@/features/prescriptions/lib/training-draft';
import { FormField } from '@/shared/components/FormField';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';

type NumericField =
  | 'planned_reps'
  | 'planned_reps_max'
  | 'planned_weight_kg'
  | 'rest_seconds_min'
  | 'rest_seconds_max'
  | 'planned_rpe';

/** Campos numéricos de uma série, na ordem do ecrã (as repetições vão em par: mínimo e «Repetições até»). */
const SET_FIELDS: ReadonlyArray<{ field: NumericField; label: string; min: number }> = [
  { field: 'planned_reps', label: 'Repetições', min: 1 },
  { field: 'planned_reps_max', label: 'Repetições até', min: 1 },
  { field: 'planned_weight_kg', label: 'Peso (kg)', min: 0 },
  { field: 'rest_seconds_min', label: 'Descanso mín. (s)', min: 0 },
  { field: 'rest_seconds_max', label: 'Descanso máx. (s)', min: 0 },
  { field: 'planned_rpe', label: 'RPE', min: 1 },
];

const touch = 'min-h-11 md:min-h-9';

/**
 * Semanas, dias, exercícios, séries e supersséries de um plano de treino (passo 2).
 *
 * Controlado: não guarda nada, só aplica cada mudança ao rascunho do layout através de
 * `onChange` (que clona a estrutura antes). Com `canChange` falso (arquivado ou com
 * histórico) tudo fica só de consulta.
 *
 * Repetições: «Repetições» é o mínimo/alvo e «até» o topo opcional; o intervalo tem de ter o
 * máximo acima do mínimo. O erro só aparece depois de `showErrors` (tentativa de gravar).
 *
 * @param exerciseNames Nome de cada exercício escolhido, para o seletor o mostrar.
 * @param onExerciseChosen Guarda o nome de um exercício acabado de escolher no catálogo.
 */
export function TrainingStructureEditor({
  structure,
  canChange,
  exerciseNames,
  onExerciseChosen,
  onChange,
  showErrors,
}: {
  structure: TrainingStructure;
  canChange: boolean;
  exerciseNames: Readonly<Record<string, string>>;
  onExerciseChosen: (id: string, label: string) => void;
  onChange: (change: (draft: TrainingStructure) => void) => void;
  showErrors: boolean;
}) {
  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-xl">Semanas e dias</h2>
        {canChange && (
          <Button
            type="button"
            variant="outline"
            className={touch}
            onClick={() =>
              onChange((draft) =>
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
            <Plus aria-hidden /> Adicionar dia
          </Button>
        )}
      </div>
      {structure.days.length === 0 && (
        <p className="text-muted-foreground border-border rounded-xl border border-dashed p-6 text-center text-sm">
          Ainda não há dias. Adiciona o primeiro dia de treino.
        </p>
      )}
      {structure.days.map((day: TrainingDayDraft, dayIndex: number) => (
        <div
          key={day.id ?? 'new-day-' + dayIndex}
          className="border-border border-l-primary space-y-4 rounded-xl border border-l-4 p-4"
        >
          <div className="grid gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
            <FormField label="Semana">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={1}
                  max={52}
                  className={touch}
                  value={day.week_number}
                  disabled={!canChange}
                  onChange={(event) =>
                    onChange((draft) => {
                      draft.days[dayIndex]!.week_number = Number(event.target.value);
                    })
                  }
                />
              )}
            </FormField>
            <FormField label="Dia da semana">
              {(control) => (
                <NativeSelect
                  {...control}
                  className={touch}
                  value={day.day_of_week}
                  disabled={!canChange}
                  onChange={(event) =>
                    onChange((draft) => {
                      draft.days[dayIndex]!.day_of_week = Number(event.target.value);
                    })
                  }
                >
                  {WEEKDAYS.map((label, index) => (
                    <option key={label} value={index}>
                      {label}
                    </option>
                  ))}
                </NativeSelect>
              )}
            </FormField>
            {canChange && (
              <Button
                type="button"
                variant="outline"
                className={touch}
                onClick={() =>
                  onChange((draft) => {
                    draft.days.splice(dayIndex, 1);
                  })
                }
              >
                <Trash2 aria-hidden /> Remover dia
              </Button>
            )}
          </div>
          <FormField label="Notas do dia">
            {(control) => (
              <Input
                {...control}
                className={touch}
                value={day.notes ?? ''}
                disabled={!canChange}
                onChange={(event) =>
                  onChange((draft) => {
                    draft.days[dayIndex]!.notes = event.target.value || null;
                  })
                }
              />
            )}
          </FormField>
          {day.exercises.map((exercise: TrainingExerciseDraft, exerciseIndex: number) => (
            <div
              key={exercise.id ?? 'new-exercise-' + exerciseIndex}
              className="bg-muted/30 space-y-3 rounded-lg p-3"
            >
              {exercise.exercise_group_id !== null && (
                <Badge variant="outline">Supersérie, posição {exercise.group_position}</Badge>
              )}
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
                            label: exerciseNames[exercise.exercise_id] ?? 'Exercício selecionado',
                          }
                    }
                    disabled={!canChange}
                    onChange={(choice: CatalogChoice) => {
                      onExerciseChosen(choice.value, choice.label);
                      onChange((draft) => {
                        draft.days[dayIndex]!.exercises[exerciseIndex]!.exercise_id = choice.value;
                      });
                    }}
                  />
                )}
              </FormField>
              <FormField label="Notas do exercício">
                {(control) => (
                  <Input
                    {...control}
                    className={touch}
                    value={exercise.notes ?? ''}
                    disabled={!canChange}
                    onChange={(event) =>
                      onChange((draft) => {
                        draft.days[dayIndex]!.exercises[exerciseIndex]!.notes =
                          event.target.value || null;
                      })
                    }
                  />
                )}
              </FormField>
              {canChange && (
                <div className="flex flex-wrap gap-2">
                  {exerciseIndex > 0 && exercise.exercise_group_id === null && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className={touch}
                      onClick={() =>
                        onChange((draft) => {
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
                  {exercise.exercise_group_id !== null && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className={touch}
                      onClick={() =>
                        onChange((draft) => {
                          const item = draft.days[dayIndex]!.exercises[exerciseIndex]!;
                          item.exercise_group_id = null;
                          item.group_position = null;
                        })
                      }
                    >
                      Retirar da supersérie
                    </Button>
                  )}
                </div>
              )}
              {exercise.sets.map((set: TrainingSetDraft, setIndex: number) => (
                <div
                  key={set.id ?? 'new-set-' + setIndex}
                  className="border-border bg-background space-y-3 rounded-lg border p-3"
                >
                  <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-7">
                    <FormField label="Série">
                      {(control) => (
                        <Input
                          {...control}
                          type="number"
                          min={1}
                          max={15}
                          className={touch}
                          value={set.set_number}
                          disabled={!canChange}
                          onChange={(event) =>
                            onChange((draft) => {
                              draft.days[dayIndex]!.exercises[exerciseIndex]!.sets[
                                setIndex
                              ]!.set_number = Number(event.target.value);
                            })
                          }
                        />
                      )}
                    </FormField>
                    {SET_FIELDS.map(({ field, label, min }) => (
                      <FormField
                        key={field}
                        label={label}
                        error={
                          field === 'planned_reps_max' && showErrors
                            ? repsRangeError(set)
                            : undefined
                        }
                      >
                        {(control) => (
                          <Input
                            {...control}
                            type="number"
                            min={min}
                            max={field === 'planned_rpe' ? 10 : undefined}
                            step={
                              field === 'planned_rpe'
                                ? 0.5
                                : field === 'planned_weight_kg'
                                  ? 0.01
                                  : 1
                            }
                            className={touch}
                            value={set[field] ?? ''}
                            disabled={!canChange}
                            onChange={(event) =>
                              onChange((draft) => {
                                draft.days[dayIndex]!.exercises[exerciseIndex]!.sets[setIndex]![
                                  field
                                ] = nullableNumber(event.target.value);
                              })
                            }
                          />
                        )}
                      </FormField>
                    ))}
                  </div>
                  {canChange && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className={touch}
                      onClick={() =>
                        onChange((draft) => {
                          draft.days[dayIndex]!.exercises[exerciseIndex]!.sets.splice(setIndex, 1);
                        })
                      }
                    >
                      <Trash2 aria-hidden /> Remover série
                    </Button>
                  )}
                </div>
              ))}
              {canChange && (
                <div className="flex flex-wrap gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    className={touch}
                    onClick={() =>
                      onChange((draft) => {
                        const sets = draft.days[dayIndex]!.exercises[exerciseIndex]!.sets;
                        sets.push(newSet(sets.length + 1));
                      })
                    }
                  >
                    <Plus aria-hidden /> Adicionar série
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    className={touch}
                    onClick={() =>
                      onChange((draft) => {
                        draft.days[dayIndex]!.exercises.splice(exerciseIndex, 1);
                      })
                    }
                  >
                    <Trash2 aria-hidden /> Remover exercício
                  </Button>
                </div>
              )}
            </div>
          ))}
          {canChange && (
            <Button
              type="button"
              variant="outline"
              className={touch}
              onClick={() =>
                onChange((draft) => {
                  const exercises = draft.days[dayIndex]!.exercises;
                  exercises.push(newExercise(exercises.length + 1));
                })
              }
            >
              <Plus aria-hidden /> Adicionar exercício
            </Button>
          )}
        </div>
      ))}
    </section>
  );
}
