import { prescriptionError } from '@/features/prescriptions/lib/errors';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { formatRepsTarget } from '@/shared/lib/reps';

export type TrainingDetails = components['schemas']['TrainingPlanDetailsResponse'];
export type TrainingStructure = components['schemas']['TrainingPlanStructureRequest'];
export type TrainingDayDraft = components['schemas']['TrainingDayRequest'];
export type TrainingExerciseDraft = components['schemas']['DayExerciseRequest'];
export type TrainingSetDraft = components['schemas']['ExerciseSetRequest'];

/** Quilogramas em PT sem casas fixas: 62.5 -> «62,5» e 80 -> «80». */
export function formatKg(value: number): string {
  return new Intl.NumberFormat('pt-PT', { maximumFractionDigits: 2, useGrouping: false }).format(
    value
  );
}

/** Dias da semana pela ordem do contrato (`day_of_week` 0 = segunda). */
export const WEEKDAYS = ['Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado', 'Domingo'];

export const EMPTY_STRUCTURE: TrainingStructure = { days: [] };

/** Estrutura editável a partir do detalhe devolvido pelo servidor (conserva todos os IDs). */
export function structureFromDetails(plan: TrainingDetails): TrainingStructure {
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
          planned_reps_max: set.planned_reps_max,
          planned_weight_kg: set.planned_weight_kg,
          rest_seconds_min: set.rest_seconds_min,
          rest_seconds_max: set.rest_seconds_max,
          planned_rpe: set.planned_rpe,
        })),
      })),
    })),
  };
}

/** Série nova: sem ID, para o servidor a criar. */
export function newSet(number: number): TrainingSetDraft {
  return {
    id: null,
    set_number: number,
    planned_reps: null,
    planned_reps_max: null,
    planned_weight_kg: null,
    rest_seconds_min: null,
    rest_seconds_max: null,
    planned_rpe: null,
  };
}

/** Exercício novo com uma série, sem ID nem escolha do catálogo. */
export function newExercise(number: number): TrainingExerciseDraft {
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

/** Texto de um campo numérico opcional: vazio é `null`, nunca 0. */
export function nullableNumber(value: string): number | null {
  return value.trim() === '' ? null : Number(value);
}

/**
 * Erro das repetições de uma série (regra do servidor, validada também aqui).
 *
 * O máximo é opcional, exige o mínimo e tem de ser maior que ele.
 */
export function repsRangeError(set: {
  planned_reps?: number | null;
  planned_reps_max?: number | null;
}): string | undefined {
  const max = set.planned_reps_max ?? null;
  if (max === null) return undefined;

  const min = set.planned_reps ?? null;
  if (min === null) return 'Indica primeiro as repetições.';
  if (max <= min) return 'Tem de ser maior que as repetições.';

  return undefined;
}

/** Verdadeiro se alguma série tem um intervalo de repetições inválido. */
export function hasRepsRangeError(structure: TrainingStructure): boolean {
  return structure.days.some((day) =>
    day.exercises.some((exercise) => exercise.sets.some((set) => repsRangeError(set) !== undefined))
  );
}

/** Verdadeiro se algum exercício ficou sem escolha do catálogo. */
export function hasEmptyExercise(structure: TrainingStructure): boolean {
  return structure.days.some((day) =>
    day.exercises.some((exercise) => exercise.exercise_id === '')
  );
}

/** Texto do planeado de uma série: «12-10 × 80 kg», «12-10 reps», «80 kg» ou «—». */
export function plannedSetText(set: {
  planned_reps: number | null;
  planned_reps_max?: number | null;
  planned_weight_kg: number | null;
}): string {
  const reps = formatRepsTarget(set.planned_reps, set.planned_reps_max);
  const weight = set.planned_weight_kg === null ? '' : `${formatKg(set.planned_weight_kg)} kg`;

  if (reps !== '' && weight !== '') return `${reps} × ${weight}`;
  if (reps !== '') return `${reps} reps`;
  return weight === '' ? '—' : weight;
}

/** Erros do passo «Detalhes»; um campo ausente está válido. */
export interface DetailsErrors {
  client?: string;
  name?: string;
  startDate?: string;
  endDate?: string;
}

/** Valida o passo «Detalhes». `needsClient` é verdadeiro num plano novo sem cliente fixo. */
export function validateDetails(
  values: { clientId: string | null; name: string; startDate: string; endDate: string },
  needsClient: boolean
): DetailsErrors {
  const errors: DetailsErrors = {};
  if (needsClient && values.clientId === null) errors.client = 'Escolhe o cliente.';
  if (values.name.trim() === '') errors.name = 'Indica o nome do plano.';
  if (values.startDate === '') errors.startDate = 'Indica a data de início.';
  if (values.endDate !== '' && values.startDate !== '' && values.endDate < values.startDate)
    errors.endDate = 'A data de fim não pode ser anterior à de início.';

  return errors;
}

const TRAINING_MESSAGES: Readonly<Record<string, string>> = {
  training_planned_reps_range_invalid: 'O máximo de repetições tem de ser maior que o mínimo.',
  training_weight_invalid: 'O peso tem de estar entre 0 e 1000 kg, com até duas casas decimais.',
  training_reps_done_invalid: 'As repetições têm de estar entre 0 e 100.',
  exercise_set_log_not_found: 'Este registo já não existe.',
  training_set_not_found: 'Esta série já não existe no plano.',
};

/**
 * Mensagem PT de uma falha ao gravar o plano ou uma série realizada.
 *
 * Numa validação o código que interessa vem no primeiro item de `errors[]`; o resto cai
 * nas mensagens gerais de prescrição.
 */
export function trainingError(failure: unknown): string {
  if (isApiProblem(failure)) {
    const code = failure.fieldErrors[0]?.code ?? failure.code;
    const message = TRAINING_MESSAGES[code];
    if (message !== undefined) return message;
  }

  return prescriptionError(failure);
}
