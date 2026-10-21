import {
  formatKg,
  plannedSetText,
  type TrainingDetails,
} from '@/features/prescriptions/lib/training-draft';
import type { components } from '@/shared/api/schema';

export type SetLog = components['schemas']['ExerciseSetLogResponse'];
export type WorkoutCompletion = components['schemas']['WorkoutCompletionResponse'];
type PlannedSet = TrainingDetails['days'][number]['exercises'][number]['sets'][number];

/** Uma série realizada com a série planeada que lhe corresponde (`null` se já não existe). */
export interface LogEntry {
  log: SetLog;
  planned: PlannedSet | null;
}

/** Séries de um exercício num dia. */
export interface ExerciseGroup {
  id: string;
  name: string;
  entries: LogEntry[];
}

/** Um dia com séries e/ou conclusão de treino. */
export interface DayGroup {
  /** `yyyy-MM-dd` no fuso do trainer, o mesmo de `local_date` das conclusões. */
  date: string;
  exercises: ExerciseGroup[];
  completions: WorkoutCompletion[];
}

/**
 * Dia local (`yyyy-MM-dd`) de um instante ISO no fuso do trainer. O «dia» de uma série é o do
 * trainer (FL-003), não o do browser: uma série às 23:30 em Lisboa não pode cair no dia seguinte
 * num computador noutro fuso, nem separar-se da conclusão desse dia (`local_date`).
 */
export function localDay(instant: string, timeZone: string): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date(instant));
  const get = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part) => part.type === type)?.value ?? '';
  return `${get('year')}-${get('month')}-${get('day')}`;
}

/**
 * Agrupa as séries realizadas por dia (mais recente primeiro) e por exercício (pela ordem do
 * plano), com as séries por número. Cada série leva a planeada ao lado. As conclusões de treino
 * entram no dia a que correspondem, mesmo sem séries nesse dia.
 */
export function groupSetLogs(
  logs: readonly SetLog[],
  plan: TrainingDetails,
  completions: readonly WorkoutCompletion[],
  timeZone: string,
  /**
   * `true` a partir da 2.ª página de séries: a conclusão só entra nos dias que têm séries nesta
   * página, para não se repetir em todas as páginas. Na 1.ª página entram também os dias só
   * com conclusão.
   */
  onlyDaysWithLogs = false
): DayGroup[] {
  const planned = new Map(
    plan.days.flatMap((day) => day.exercises.map((exercise) => [exercise.id, exercise] as const))
  );
  const days = new Map<string, DayGroup>();
  const dayOf = (date: string): DayGroup => {
    let group = days.get(date);
    if (group === undefined) {
      group = { date, exercises: [], completions: [] };
      days.set(date, group);
    }
    return group;
  };

  for (const log of logs) {
    const group = dayOf(localDay(log.performed_at, timeZone));
    let exercise = group.exercises.find((item) => item.id === log.training_plan_day_exercise_id);
    if (exercise === undefined) {
      exercise = { id: log.training_plan_day_exercise_id, name: log.exercise_name, entries: [] };
      group.exercises.push(exercise);
    }
    const target =
      planned
        .get(log.training_plan_day_exercise_id)
        ?.sets.find((set) => set.set_number === log.set_number) ?? null;
    exercise.entries.push({ log, planned: target });
  }
  for (const completion of completions) {
    if (onlyDaysWithLogs && !days.has(completion.local_date)) continue;
    dayOf(completion.local_date).completions.push(completion);
  }

  const order = (id: string) => planned.get(id)?.order_number ?? Number.MAX_SAFE_INTEGER;
  for (const group of days.values()) {
    group.exercises.sort((a, b) => order(a.id) - order(b.id));
    for (const exercise of group.exercises)
      exercise.entries.sort((a, b) => a.log.set_number - b.log.set_number);
  }

  return [...days.values()].sort((a, b) => b.date.localeCompare(a.date));
}

/** O feito de uma série: «8 × 80 kg». */
export function doneSetText(log: SetLog): string {
  return `${log.reps_done} × ${formatKg(log.weight_kg)} kg`;
}

/** Linha completa: «Série 1 · feito 8 × 80 kg · plano 12-10 × 80 kg». */
export function setLogLine(entry: LogEntry): string {
  const parts = [`Série ${entry.log.set_number}`, `feito ${doneSetText(entry.log)}`];
  if (entry.planned !== null) parts.push(`plano ${plannedSetText(entry.planned)}`);
  if (entry.log.rpe !== null) parts.push(`RPE ${formatKg(entry.log.rpe)}`);

  return parts.join(' · ');
}
