import { MAX_WEIGHT_KG, parseWeight } from '@/features/portal/lib/workout';
import { isApiProblem, type ApiFieldError } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { formatNumber } from '@/shared/lib/format';

/**
 * Regras do formulário de resposta à avaliação periódica do cliente.
 *
 * Funções puras: o que cada passo valida, como o texto escrito vira o pedido e como os erros
 * do servidor voltam ao passo e ao campo certos. Os limites são os de
 * `SubmitCheckInResponseCommandValidator`.
 */

type CheckInAnswer = components['schemas']['CheckInAnswerRequest'];
type Measurements = components['schemas']['BodyMeasurementsPayload'];
type Feedback = components['schemas']['CheckInFeedbackPayload'];

/**
 * Medidas pela ordem do contrato. A ordem conta: o backend devolve o erro de uma medida como
 * `BodyMeasurements[2]` (o índice nesta lista), não pelo nome do campo.
 */
export const MEASUREMENT_KEYS = [
  'waist_cm',
  'hip_cm',
  'chest_cm',
  'right_arm_cm',
  'left_arm_cm',
  'right_thigh_cm',
  'left_thigh_cm',
  'right_calf_cm',
  'left_calf_cm',
] as const satisfies readonly (keyof Measurements)[];

/** Respostas de texto pela ordem do contrato (`Feedback[0]` é o apetite). */
export const FEEDBACK_KEYS = [
  'appetite',
  'digestion',
  'training_load',
  'recovery_sleep',
  'energy_levels',
  'body_response',
] as const satisfies readonly (keyof Feedback)[];

export type MeasurementKey = (typeof MEASUREMENT_KEYS)[number];
export type FeedbackKey = (typeof FEEDBACK_KEYS)[number];

/** Valores do formulário: tudo texto, tal como o cliente escreveu. */
export interface CheckInFormValues {
  weight_kg: string;
  body_fat_percentage: string;
  measurements: Record<MeasurementKey, string>;
  feedback: Record<FeedbackKey, string>;
  training_adherence_score: string;
  nutrition_adherence_score: string;
  notes: string;
}

/** Caminho de um campo do formulário, no formato do react-hook-form. */
export type CheckInField =
  | 'weight_kg'
  | 'body_fat_percentage'
  | `measurements.${MeasurementKey}`
  | `feedback.${FeedbackKey}`
  | 'training_adherence_score'
  | 'nutrition_adherence_score'
  | 'notes';

/** Erro de um campo, com o passo onde ele vive. */
export interface CheckInIssue {
  readonly field: CheckInField;
  readonly step: number;
  readonly message: string;
}

/** Os três passos, pela ordem em que aparecem. */
export const CHECK_IN_STEPS = ['Peso', 'Medidas', 'Como correu'] as const;

/**
 * Medida máxima aceite pelo formulário, em cm. O backend só exige "maior que 0"; o teto existe
 * para apanhar enganos de escrita (uma cintura de 9000 cm).
 */
export const MAX_MEASUREMENT_CM = 1000;

/** Tamanho máximo de cada resposta de texto e das notas. */
export const MAX_TEXT_LENGTH = 2000;

const MESSAGES = {
  weightRequired: 'Indica o peso.',
  weight: `Indica um peso maior que 0 e até ${formatNumber(MAX_WEIGHT_KG)} kg.`,
  bodyFat: 'Indica uma percentagem maior que 0 e menor que 100.',
  measurement: `Indica uma medida maior que 0 e até ${formatNumber(MAX_MEASUREMENT_CM)} cm.`,
  adherence: 'Indica um valor inteiro de 0 a 100.',
  text: `O texto não pode exceder ${formatNumber(MAX_TEXT_LENGTH)} caracteres.`,
} as const;

/** Formulário vazio: nenhum campo vem pré-preenchido, nem o peso. */
export function emptyCheckInValues(): CheckInFormValues {
  return {
    weight_kg: '',
    body_fat_percentage: '',
    measurements: Object.fromEntries(MEASUREMENT_KEYS.map((key) => [key, ''])) as Record<
      MeasurementKey,
      string
    >,
    feedback: Object.fromEntries(FEEDBACK_KEYS.map((key) => [key, ''])) as Record<
      FeedbackKey,
      string
    >,
    training_adherence_score: '',
    nutrition_adherence_score: '',
    notes: '',
  };
}

/**
 * Lê um decimal escrito pelo cliente ("17,5" ou "17.5"), com duas casas no máximo.
 *
 * Duas casas porque as colunas são `numeric(10,2)`: com três, o servidor arredondava ao gravar
 * e um reenvio igual deixava de ser reconhecido como o mesmo (409 em vez de 200).
 */
export function parseDecimal(text: string): number | null {
  const normalized = text.trim().replace(',', '.');
  return /^\d+(\.\d{1,2})?$/.test(normalized) ? Number(normalized) : null;
}

/** Peso corporal: maior que 0 e até {@link MAX_WEIGHT_KG}. */
export function parseBodyWeight(text: string): number | null {
  const value = parseWeight(text);
  return value !== null && value > 0 ? value : null;
}

/** Massa gorda: estritamente entre 0 e 100. */
export function parseBodyFat(text: string): number | null {
  const value = parseDecimal(text);
  return value !== null && value > 0 && value < 100 ? value : null;
}

/** Medida em cm: maior que 0 e até {@link MAX_MEASUREMENT_CM}. */
export function parseMeasurement(text: string): number | null {
  const value = parseDecimal(text);
  return value !== null && value > 0 && value <= MAX_MEASUREMENT_CM ? value : null;
}

/** Adesão: inteiro de 0 a 100. */
export function parseAdherence(text: string): number | null {
  const normalized = text.trim();
  if (!/^\d+$/.test(normalized)) return null;

  const value = Number(normalized);
  return value <= 100 ? value : null;
}

const isBlank = (text: string) => text.trim() === '';

/** Erros de um campo opcional: vazio é válido, preenchido tem de ser lido pelo parser. */
function optional(
  field: CheckInField,
  step: number,
  text: string,
  parse: (text: string) => number | null,
  message: string
): CheckInIssue[] {
  return isBlank(text) || parse(text) !== null ? [] : [{ field, step, message }];
}

function tooLong(field: CheckInField, step: number, text: string): CheckInIssue[] {
  return text.trim().length > MAX_TEXT_LENGTH ? [{ field, step, message: MESSAGES.text }] : [];
}

/**
 * Todos os erros do formulário, com o passo de cada um.
 *
 * O ecrã filtra pelo passo atual ao tocar em "Seguinte" e usa a lista inteira ao enviar.
 */
export function checkInIssues(values: CheckInFormValues): CheckInIssue[] {
  const weight: CheckInIssue[] = isBlank(values.weight_kg)
    ? [{ field: 'weight_kg', step: 0, message: MESSAGES.weightRequired }]
    : parseBodyWeight(values.weight_kg) === null
      ? [{ field: 'weight_kg', step: 0, message: MESSAGES.weight }]
      : [];

  return [
    ...weight,
    ...optional(
      'body_fat_percentage',
      0,
      values.body_fat_percentage,
      parseBodyFat,
      MESSAGES.bodyFat
    ),
    ...MEASUREMENT_KEYS.flatMap((key) =>
      optional(
        `measurements.${key}`,
        1,
        values.measurements[key],
        parseMeasurement,
        MESSAGES.measurement
      )
    ),
    ...FEEDBACK_KEYS.flatMap((key) => tooLong(`feedback.${key}`, 2, values.feedback[key])),
    ...optional(
      'training_adherence_score',
      2,
      values.training_adherence_score,
      parseAdherence,
      MESSAGES.adherence
    ),
    ...optional(
      'nutrition_adherence_score',
      2,
      values.nutrition_adherence_score,
      parseAdherence,
      MESSAGES.adherence
    ),
    ...tooLong('notes', 2, values.notes),
  ];
}

const textOrNull = (text: string) => (isBlank(text) ? null : text.trim());

/**
 * Constrói o pedido a partir de valores já validados por {@link checkInIssues}.
 *
 * Um bloco inteiro vazio (medidas ou respostas) vai a `null`; um campo vazio dentro de um
 * bloco preenchido vai a `null` nesse campo. Vazio nunca é zero.
 */
export function toCheckInAnswer(values: CheckInFormValues): CheckInAnswer {
  const measurements = Object.fromEntries(
    MEASUREMENT_KEYS.map((key) => [key, parseMeasurement(values.measurements[key])])
  ) as Measurements;
  const feedback = Object.fromEntries(
    FEEDBACK_KEYS.map((key) => [key, textOrNull(values.feedback[key])])
  ) as Feedback;

  return {
    weight_kg: parseBodyWeight(values.weight_kg) ?? 0,
    body_fat_percentage: parseBodyFat(values.body_fat_percentage),
    notes: textOrNull(values.notes),
    body_measurements: Object.values(measurements).some((value) => value !== null)
      ? measurements
      : null,
    feedback: Object.values(feedback).some((value) => value !== null) ? feedback : null,
    training_adherence_score: parseAdherence(values.training_adherence_score),
    nutrition_adherence_score: parseAdherence(values.nutrition_adherence_score),
  };
}

/** Campos simples do pedido, pelo nome que o backend devolve em `errors[].field`. */
const SERVER_FIELDS: Readonly<Record<string, { field: CheckInField; step: number }>> = {
  WeightKg: { field: 'weight_kg', step: 0 },
  BodyFatPercentage: { field: 'body_fat_percentage', step: 0 },
  TrainingAdherenceScore: { field: 'training_adherence_score', step: 2 },
  NutritionAdherenceScore: { field: 'nutrition_adherence_score', step: 2 },
  Notes: { field: 'notes', step: 2 },
};

const SERVER_MESSAGES: Readonly<Record<string, string>> = {
  weight_invalid: MESSAGES.weight,
  body_fat_percentage_invalid: MESSAGES.bodyFat,
  body_measurement_invalid: MESSAGES.measurement,
  check_in_feedback_too_long: MESSAGES.text,
  notes_too_long: MESSAGES.text,
  training_adherence_invalid: MESSAGES.adherence,
  nutrition_adherence_invalid: MESSAGES.adherence,
};

/** Onde vive o campo de um erro do servidor; `null` quando o formulário não conhece. */
function serverTarget(field: string): { field: CheckInField; step: number } | null {
  if (Object.hasOwn(SERVER_FIELDS, field)) return SERVER_FIELDS[field] ?? null;

  const indexed = /^(BodyMeasurements|Feedback)\[(\d+)\]$/.exec(field);
  if (indexed === null) return null;

  const index = Number(indexed[2]);
  if (indexed[1] === 'BodyMeasurements') {
    const key = MEASUREMENT_KEYS[index];
    return key === undefined ? null : { field: `measurements.${key}`, step: 1 };
  }

  const key = FEEDBACK_KEYS[index];
  return key === undefined ? null : { field: `feedback.${key}`, step: 2 };
}

/**
 * Traduz os `errors[]` de um 400 para campos do formulário.
 *
 * Um erro cujo campo o formulário não reconhece fica de fora: o ecrã mostra então a mensagem
 * geral, em vez de o esconder num campo que não existe.
 */
export function serverCheckInIssues(fieldErrors: readonly ApiFieldError[]): CheckInIssue[] {
  return fieldErrors.flatMap((error) => {
    const target = serverTarget(error.field);
    const message = Object.hasOwn(SERVER_MESSAGES, error.code)
      ? SERVER_MESSAGES[error.code]
      : undefined;

    return target === null || message === undefined ? [] : [{ ...target, message }];
  });
}

/**
 * Recusas de estado da avaliação (`AssessmentErrors`): 404 e 409 que chegam no `title`.
 *
 * Depois de qualquer uma a avaliação deixa de estar por responder, o ecrã relê e o formulário
 * sai. Por isso a mensagem vai para um toast, não para o formulário.
 */
const STATE_MESSAGES: Readonly<Record<string, string>> = {
  check_in_wrong_day: 'Esta avaliação só podia ser respondida no próprio dia.',
  check_in_already_answered: 'Esta avaliação já foi respondida.',
  check_in_cancelled: 'Esta avaliação foi cancelada pelo teu personal trainer.',
  check_in_not_found: 'Esta avaliação já não existe.',
  assessment_client_inactive: 'A tua ficha não está ativa. Fala com o teu personal trainer.',
};

/** Mensagem de uma recusa de estado; `null` quando a falha é de outro tipo. */
export function checkInStateMessage(failure: unknown): string | null {
  if (!isApiProblem(failure) || !Object.hasOwn(STATE_MESSAGES, failure.code)) return null;

  return STATE_MESSAGES[failure.code] ?? null;
}
