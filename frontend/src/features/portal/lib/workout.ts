import { formatNumber } from '@/shared/lib/format';

/**
 * Regras de apresentação e de entrada do treino do cliente.
 *
 * Funções puras, partilhadas pelo treino de hoje e pelo plano completo, que leem a mesma
 * prescrição (`planned_*`) em schemas diferentes do contrato.
 */

/** Campos planeados de uma série, comuns a `MyWorkoutSetResponse` e `MyExerciseSetResponse`. */
export interface PlannedSet {
  set_number: number;
  planned_reps: number | null;
  planned_weight_kg: number | null;
  rest_seconds_min: number | null;
  rest_seconds_max: number | null;
  planned_rpe: number | null;
}

/** Carga máxima por série */
export const MAX_WEIGHT_KG = 1000;

/** Repetições máximas por série. */
export const MAX_REPS = 100;

/** "1 série" / "4 séries". */
export function countLabel(value: number, singular: string, plural: string): string {
  return `${formatNumber(value)} ${value === 1 ? singular : plural}`;
}

/** "8" quando o mínimo e o máximo coincidem, senão "8–12". */
function range(min: number, max: number): string {
  return min === max ? formatNumber(min) : `${formatNumber(min)}–${formatNumber(max)}`;
}

/**
 * Resumo da prescrição de um exercício: "4 séries · 8 reps · 90–120 s".
 *
 * Repetições e descanso variam por série, por isso mostram o intervalo; um campo que nenhuma
 * série preenche não aparece (não se inventa um zero).
 */
export function prescriptionSummary(sets: readonly PlannedSet[]): string {
  const parts = [countLabel(sets.length, 'série', 'séries')];

  const reps = sets.flatMap((set) => (set.planned_reps === null ? [] : [set.planned_reps]));
  if (reps.length > 0) parts.push(`${range(Math.min(...reps), Math.max(...reps))} reps`);

  const rest = sets.flatMap((set) =>
    [set.rest_seconds_min, set.rest_seconds_max].filter((value) => value !== null)
  );
  if (rest.length > 0) parts.push(`${range(Math.min(...rest), Math.max(...rest))} s`);

  return parts.join(' · ');
}

/** Tamanho de um dia: "5 exercícios · 18 séries" (sem duração estimada). */
export function workoutSize(exercises: readonly { sets: readonly unknown[] }[]): string {
  const sets = exercises.reduce((total, exercise) => total + exercise.sets.length, 0);
  return `${countLabel(exercises.length, 'exercício', 'exercícios')} · ${countLabel(sets, 'série', 'séries')}`;
}

/** Série planeada numa linha: "Série 1 · 8 reps · 60 kg · RPE 8 · 90–120 s". */
export function plannedSetLabel(set: PlannedSet): string {
  const parts = [`Série ${formatNumber(set.set_number)}`];

  if (set.planned_reps !== null) parts.push(`${formatNumber(set.planned_reps)} reps`);
  if (set.planned_weight_kg !== null) parts.push(`${formatNumber(set.planned_weight_kg)} kg`);
  if (set.planned_rpe !== null) parts.push(`RPE ${formatNumber(set.planned_rpe)}`);

  const rest = [set.rest_seconds_min, set.rest_seconds_max].filter((value) => value !== null);
  if (rest.length > 0) parts.push(`${range(Math.min(...rest), Math.max(...rest))} s`);

  return parts.join(' · ');
}

/**
 * Nome a mostrar. Um exercício bloqueado pela plataforma chega mascarado em inglês
 * (`"Unavailable exercise"`, `PortalContentMasking`); o cliente vê a versão PT.
 */
export function exerciseDisplayName(exercise: {
  exercise_name: string;
  is_unavailable: boolean;
}): string {
  return exercise.is_unavailable ? 'Exercício indisponível' : exercise.exercise_name;
}

/** Percentagem inteira de séries registadas; 0 quando não há séries planeadas. */
export function progressPercent(logged: number, planned: number): number {
  return planned === 0 ? 0 : Math.round((logged / planned) * 100);
}

/** Decimal em PT sem casas fixas nem separador de milhares: 62.5 -> "62,5" */
export function formatDecimal(value: number): string {
  return new Intl.NumberFormat('pt-PT', { minimumFractionDigits: 1, useGrouping: false }).format(
    value
  );
}

/**
 * Lê o peso escrito pelo cliente ("62,5" ou "62.5").
 *
 * Devolve `null` quando o texto não é um número entre 0 e {@link MAX_WEIGHT_KG} com até duas
 * casas decimais: a coluna é `numeric(10,2)` e um valor com três casas seria arredondado em
 * silêncio.
 */
export function parseWeight(text: string): number | null {
  const normalized = text.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) return null;

  const value = Number(normalized);
  return value <= MAX_WEIGHT_KG ? value : null;
}

/** Lê as repetições: inteiro de 0 a {@link MAX_REPS}, senão `null`. */
export function parseReps(text: string): number | null {
  const normalized = text.trim();
  if (!/^\d+$/.test(normalized)) return null;

  const value = Number(normalized);
  return value <= MAX_REPS ? value : null;
}

const WORKOUT_ERROR_MESSAGES: Record<string, string> = {
  training_weight_invalid: `O peso tem de estar entre 0 e ${formatNumber(MAX_WEIGHT_KG)} kg.`,
  training_reps_done_invalid: `As repetições têm de estar entre 0 e ${formatNumber(MAX_REPS)}.`,
  exercise_set_log_not_editable: 'Esta série já não pode ser corrigida.',
  workout_already_completed: 'O treino já foi concluído. Já não podes desmarcar séries.',
  training_plan_inactive: 'O teu plano mudou. Atualiza o treino.',
  training_date_outside_plan: 'Hoje está fora do período do teu plano.',
  training_set_not_found: 'Esta série já não existe no teu plano.',
  exercise_set_log_not_found: 'Este registo já não existe. Atualiza o treino.',
  training_structure_reference_not_found: 'Este treino já não existe no teu plano.',
};

/** Mensagem PT para um código de erro de escrita do treino; genérica quando desconhecido. */
export function workoutErrorMessage(code: string | null): string {
  return (
    (code === null ? undefined : WORKOUT_ERROR_MESSAGES[code]) ??
    'Não foi possível guardar a série. Tenta novamente.'
  );
}
