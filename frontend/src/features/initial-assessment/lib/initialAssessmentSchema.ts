import type { FieldPath } from 'react-hook-form';
import { z } from 'zod';

import { ACTIVITY_LEVEL_LABELS } from '@/features/clients/lib/labels';
import type { ApiFieldError } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';

/**
 * Regras e conversões da avaliação inicial, partilhadas pelo formulário do personal trainer e
 * pelo preenchimento do cliente no portal. Os limites são os de
 * `InitialAssessmentValuesValidator` e `AssessmentInputValidators`.
 */

type Assessment = components['schemas']['InitialAssessmentResponse'];
type Intake = components['schemas']['NutritionIntakePayload'];
export type AssessmentRequest = components['schemas']['SubmitMyInitialAssessmentRequest'];

/** Limites do servidor. */
export const MAX_WEIGHT_KG = 1000;
export const MAX_MEASUREMENT_CM = 1000;
export const MAX_WATER_LITERS = 50;
export const MAX_TEXT_LENGTH = 2000;

/** Número opcional vindo de um `<input type="number">`: vazio fica `null`. */
export const toNumber = (value: string | number | null) =>
  value == null || value === '' ? null : Number(value);

/** Até duas casas decimais, como as colunas `numeric(10,2)` do servidor. */
const hasTwoDecimalsAtMost = (value: number) =>
  Math.abs(value * 100 - Math.round(value * 100)) < 1e-6;

const DECIMALS = 'Usa no máximo duas casas decimais.';

/** Decimal positivo com teto e duas casas; devolve só a primeira regra falhada. */
function boundedNumber(required: string, positive: string, max: number, tooBig: string) {
  return z.number(required).superRefine((value, ctx) => {
    const message =
      value <= 0 ? positive : value > max ? tooBig : !hasTwoDecimalsAtMost(value) ? DECIMALS : null;
    if (message !== null) ctx.addIssue({ code: 'custom', message });
  });
}

const measurement = () =>
  boundedNumber(
    'A medida tem de ser maior que 0 cm.',
    'A medida tem de ser maior que 0 cm.',
    MAX_MEASUREMENT_CM,
    `A medida não pode exceder ${MAX_MEASUREMENT_CM} cm.`
  ).nullable();
const scale = z
  .number('Indica um valor de 1 a 5.')
  .int('Indica um valor de 1 a 5.')
  .min(1, 'Indica um valor de 1 a 5.')
  .max(5, 'Indica um valor de 1 a 5.')
  .nullable();
const TEXT_TOO_LONG = `O texto não pode exceder ${MAX_TEXT_LENGTH} caracteres.`;
const longText = z.string().max(MAX_TEXT_LENGTH, TEXT_TOO_LONG);
const BODY_FAT = 'Indica uma percentagem entre 0 e 100.';

/** Esquema da avaliação inicial (valores do formulário). */
export const assessmentSchema = z.object({
  weight_kg: boundedNumber(
    'Indica o peso.',
    'O peso tem de ser maior que 0 kg.',
    MAX_WEIGHT_KG,
    `O peso não pode exceder ${MAX_WEIGHT_KG} kg.`
  ),
  height_cm: z
    .number('Indica a altura.')
    .int('A altura é em centímetros inteiros.')
    .positive('A altura tem de ser maior que 0 cm.'),
  body_fat_percentage: z
    .number(BODY_FAT)
    .gt(0, BODY_FAT)
    .lt(100, BODY_FAT)
    .refine(hasTwoDecimalsAtMost, DECIMALS)
    .nullable(),
  fitness_level: z
    .string()
    .trim()
    .min(1, 'Escolhe a condição física.')
    .max(50, 'A condição física não pode exceder 50 caracteres.'),
  activity_level: z.enum(
    Object.keys(ACTIVITY_LEVEL_LABELS) as [keyof typeof ACTIVITY_LEVEL_LABELS],
    { error: 'Escolhe o nível de atividade.' }
  ),
  goals: z.string().trim().min(1, 'Indica os objetivos.').max(MAX_TEXT_LENGTH, TEXT_TOO_LONG),
  profession: z.string().trim().max(255, 'A profissão não pode exceder 255 caracteres.'),
  medical_conditions: longText,
  body_measurements: z.object({
    waist_cm: measurement(),
    hip_cm: measurement(),
    chest_cm: measurement(),
    right_arm_cm: measurement(),
    left_arm_cm: measurement(),
    right_thigh_cm: measurement(),
    left_thigh_cm: measurement(),
    right_calf_cm: measurement(),
    left_calf_cm: measurement(),
  }),
  nutrition_intake: z.object({
    food_preferences: longText,
    disliked_foods: longText,
    food_intolerances: longText,
    food_allergies: longText,
    dietary_restrictions: longText,
    daily_routine: longText,
    sleep_quality: scale,
    mood: scale,
    stress_level: scale,
    avg_water_liters_per_day: boundedNumber(
      'A água tem de ser maior que 0 L.',
      'A água tem de ser maior que 0 L.',
      MAX_WATER_LITERS,
      `A água não pode exceder ${MAX_WATER_LITERS} L.`
    ).nullable(),
    hungriest_time_of_day: longText,
    uses_supplements: z.enum(['', 'yes', 'no']),
    current_supplements: longText,
    other_notes: longText,
  }),
});

/**
 * Valores do formulário: peso e altura começam vazios (`null`) numa avaliação nova, e a
 * condição física e o nível de atividade podem estar por escolher (`''`). O `safeParse`
 * recusa esses valores com a mensagem do campo.
 */
export type AssessmentValues = Omit<
  z.input<typeof assessmentSchema>,
  'weight_kg' | 'height_cm' | 'activity_level'
> & {
  weight_kg: number | null;
  height_cm: number | null;
  activity_level: z.input<typeof assessmentSchema>['activity_level'] | '';
};

/** Caminho de um campo do formulário. */
export type AssessmentField = FieldPath<AssessmentValues>;

/** Textos dos hábitos pela ordem do contrato (o servidor indexa o erro: `NutritionIntake[2]`). */
export const INTAKE_TEXT_KEYS = [
  'food_preferences',
  'disliked_foods',
  'food_intolerances',
  'food_allergies',
  'dietary_restrictions',
  'daily_routine',
  'hungriest_time_of_day',
  'current_supplements',
  'other_notes',
] as const;

export const INTAKE_TEXT_LABELS = {
  food_preferences: 'Preferências alimentares',
  disliked_foods: 'Alimentos de que não gosta',
  food_intolerances: 'Intolerâncias',
  food_allergies: 'Alergias',
  dietary_restrictions: 'Restrições alimentares',
  daily_routine: 'Rotina diária',
  hungriest_time_of_day: 'Altura do dia com mais fome',
  current_supplements: 'Suplementos atuais',
  other_notes: 'Outras notas',
} as const satisfies Record<(typeof INTAKE_TEXT_KEYS)[number], string>;

export const SCALE_LABELS = {
  sleep_quality: 'Qualidade do sono (1-5)',
  mood: 'Humor (1-5)',
  stress_level: 'Stress (1-5)',
} as const;

/** Medidas pela ordem do contrato (`BodyMeasurements[2]` é o peito). */
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
] as const;

/** Os três passos do preenchimento do cliente. */
export const ASSESSMENT_STEPS = ['Corpo e objetivos', 'Medidas', 'Hábitos e saúde'] as const;

/** Passo onde vive um campo, pelo primeiro segmento do caminho. */
export function stepOfField(field: string): number {
  if (field.startsWith('body_measurements')) return 1;
  if (field.startsWith('nutrition_intake')) return 2;
  return 0;
}

/** Erro de um campo, com o passo onde ele vive. */
export interface AssessmentIssue {
  readonly field: AssessmentField;
  readonly step: number;
  readonly message: string;
}

/** Todos os erros dos valores, com o passo de cada um. Sem erros devolve lista vazia. */
export function assessmentIssues(values: AssessmentValues): AssessmentIssue[] {
  const parsed = assessmentSchema.safeParse(values);
  if (parsed.success) return [];

  return parsed.error.issues.map((issue) => {
    const field = issue.path.join('.') as AssessmentField;
    return { field, step: stepOfField(field), message: issue.message };
  });
}

/** Campos de topo do backend (PascalCase) para os campos do formulário. */
export const SERVER_FIELDS: Readonly<Record<string, AssessmentField>> = {
  WeightKg: 'weight_kg',
  HeightCm: 'height_cm',
  BodyFatPercentage: 'body_fat_percentage',
  FitnessLevel: 'fitness_level',
  ActivityLevel: 'activity_level',
  Goals: 'goals',
  Profession: 'profession',
  MedicalConditions: 'medical_conditions',
};

/** Mensagens por código de validação do servidor. */
const SERVER_MESSAGES: Readonly<Record<string, string>> = {
  weight_invalid: `Indica um peso maior que 0 e até ${MAX_WEIGHT_KG} kg, com duas casas decimais no máximo.`,
  height_invalid: 'A altura tem de ser maior que 0 cm.',
  body_fat_percentage_invalid: BODY_FAT,
  fitness_level_invalid: 'Escolhe a condição física.',
  activity_level_invalid: 'Escolhe o nível de atividade.',
  goals_required: 'Indica os objetivos.',
  goals_too_long: TEXT_TOO_LONG,
  medical_conditions_too_long: TEXT_TOO_LONG,
  profession_too_long: 'A profissão não pode exceder 255 caracteres.',
  body_measurement_invalid: `Indica uma medida maior que 0 e até ${MAX_MEASUREMENT_CM} cm.`,
  nutrition_text_too_long: TEXT_TOO_LONG,
  sleep_quality_invalid: 'Indica um valor de 1 a 5.',
  mood_invalid: 'Indica um valor de 1 a 5.',
  stress_level_invalid: 'Indica um valor de 1 a 5.',
  average_water_invalid: `Indica uma quantidade maior que 0 e até ${MAX_WATER_LITERS} L.`,
};

/** Onde vive um erro do servidor; `null` quando o formulário não o reconhece. */
function serverTarget(field: string): AssessmentField | null {
  if (Object.hasOwn(SERVER_FIELDS, field)) return SERVER_FIELDS[field] ?? null;

  const indexed = /^(BodyMeasurements|NutritionIntake)\[(\d+)\]$/.exec(field);
  if (indexed !== null) {
    const index = Number(indexed[2]);
    if (indexed[1] === 'BodyMeasurements') {
      const key = MEASUREMENT_KEYS[index];
      return key === undefined ? null : `body_measurements.${key}`;
    }
    const key = INTAKE_TEXT_KEYS[index];
    return key === undefined ? null : `nutrition_intake.${key}`;
  }

  const named: Record<string, AssessmentField> = {
    'NutritionIntake.SleepQuality': 'nutrition_intake.sleep_quality',
    'NutritionIntake.Mood': 'nutrition_intake.mood',
    'NutritionIntake.StressLevel': 'nutrition_intake.stress_level',
    'NutritionIntake.AvgWaterLitersPerDay': 'nutrition_intake.avg_water_liters_per_day',
  };
  return Object.hasOwn(named, field) ? (named[field] ?? null) : null;
}

/**
 * Traduz os `errors[]` de um 400 para campos do formulário, com a mensagem pelo código.
 * Um erro que o formulário não reconhece fica de fora: o ecrã mostra a mensagem geral.
 */
export function serverAssessmentIssues(fieldErrors: readonly ApiFieldError[]): AssessmentIssue[] {
  return fieldErrors.flatMap((error) => {
    const field = serverTarget(error.field);
    const message = Object.hasOwn(SERVER_MESSAGES, error.code)
      ? SERVER_MESSAGES[error.code]
      : undefined;

    return field === null || message === undefined
      ? []
      : [{ field, step: stepOfField(field), message }];
  });
}

/** Formulário de uma avaliação nova: nada pré-escolhido, nem a condição física nem a atividade. */
export function emptyAssessmentValues(): AssessmentValues {
  return { ...assessmentDefaults(null), fitness_level: '', activity_level: '' };
}

/** Valores iniciais do formulário a partir de uma avaliação guardada (ou valores neutros). */
export function assessmentDefaults(assessment: Assessment | null): AssessmentValues {
  const intake = assessment?.nutrition_intake;
  const text = (value: string | null | undefined) => value ?? '';

  return {
    weight_kg: assessment?.weight_kg ?? null,
    height_cm: assessment?.height_cm ?? null,
    body_fat_percentage: assessment?.body_fat_percentage ?? null,
    fitness_level: assessment?.fitness_level ?? 'beginner',
    activity_level:
      (assessment?.activity_level as AssessmentValues['activity_level'] | undefined) ?? 'sedentary',
    goals: assessment?.goals ?? '',
    profession: assessment?.profession ?? '',
    medical_conditions: assessment?.medical_conditions ?? '',
    body_measurements: {
      waist_cm: assessment?.body_measurements.waist_cm ?? null,
      hip_cm: assessment?.body_measurements.hip_cm ?? null,
      chest_cm: assessment?.body_measurements.chest_cm ?? null,
      right_arm_cm: assessment?.body_measurements.right_arm_cm ?? null,
      left_arm_cm: assessment?.body_measurements.left_arm_cm ?? null,
      right_thigh_cm: assessment?.body_measurements.right_thigh_cm ?? null,
      left_thigh_cm: assessment?.body_measurements.left_thigh_cm ?? null,
      right_calf_cm: assessment?.body_measurements.right_calf_cm ?? null,
      left_calf_cm: assessment?.body_measurements.left_calf_cm ?? null,
    },
    nutrition_intake: {
      food_preferences: text(intake?.food_preferences),
      disliked_foods: text(intake?.disliked_foods),
      food_intolerances: text(intake?.food_intolerances),
      food_allergies: text(intake?.food_allergies),
      dietary_restrictions: text(intake?.dietary_restrictions),
      daily_routine: text(intake?.daily_routine),
      sleep_quality: intake?.sleep_quality ?? null,
      mood: intake?.mood ?? null,
      stress_level: intake?.stress_level ?? null,
      avg_water_liters_per_day: intake?.avg_water_liters_per_day ?? null,
      hungriest_time_of_day: text(intake?.hungriest_time_of_day),
      uses_supplements:
        intake?.uses_supplements === true ? 'yes' : intake?.uses_supplements === false ? 'no' : '',
      current_supplements: text(intake?.current_supplements),
      other_notes: text(intake?.other_notes),
    },
  };
}

/**
 * Pedido a partir de valores já validados por {@link assessmentSchema}: texto vazio vai a
 * `null`, medidas e hábitos vão sempre como objeto.
 */
export function toAssessmentRequest(data: z.output<typeof assessmentSchema>): AssessmentRequest {
  const intake = data.nutrition_intake;
  const orNull = (value: string) => value.trim() || null;
  const nutrition: Intake = {
    food_preferences: orNull(intake.food_preferences),
    disliked_foods: orNull(intake.disliked_foods),
    food_intolerances: orNull(intake.food_intolerances),
    food_allergies: orNull(intake.food_allergies),
    dietary_restrictions: orNull(intake.dietary_restrictions),
    daily_routine: orNull(intake.daily_routine),
    sleep_quality: intake.sleep_quality,
    mood: intake.mood,
    stress_level: intake.stress_level,
    avg_water_liters_per_day: intake.avg_water_liters_per_day,
    hungriest_time_of_day: orNull(intake.hungriest_time_of_day),
    uses_supplements: intake.uses_supplements === '' ? null : intake.uses_supplements === 'yes',
    current_supplements: orNull(intake.current_supplements),
    other_notes: orNull(intake.other_notes),
  };

  return {
    weight_kg: data.weight_kg,
    height_cm: data.height_cm,
    body_fat_percentage: data.body_fat_percentage,
    medical_conditions: orNull(data.medical_conditions),
    fitness_level: data.fitness_level,
    activity_level: data.activity_level,
    goals: data.goals,
    profession: orNull(data.profession),
    body_measurements: data.body_measurements,
    nutrition_intake: nutrition,
  };
}
