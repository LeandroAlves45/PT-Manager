import { useForm, type FieldPath } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { useSaveInitialAssessmentMutation } from '@/features/clients/api/mutations';
import { FormField } from '@/features/clients/components/FormField';
import { ACTIVITY_LEVEL_LABELS, fitnessLevelOptions } from '@/features/clients/lib/labels';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';
import { Textarea } from '@/shared/components/ui/textarea';

type Assessment = components['schemas']['InitialAssessmentResponse'];
type Measurements = components['schemas']['BodyMeasurementsPayload'];
type Intake = components['schemas']['NutritionIntakePayload'];

/** Número opcional vindo de um `<input type="number">`: vazio fica `null`. */
const toNumber = (value: string | number | null) =>
  value == null || value === '' ? null : Number(value);

const positiveOrNull = (message: string) => z.number(message).positive(message).nullable();
const scale = z
  .number('Indica um valor de 1 a 5.')
  .int('Indica um valor de 1 a 5.')
  .min(1, 'Indica um valor de 1 a 5.')
  .max(5, 'Indica um valor de 1 a 5.')
  .nullable();
const longText = z.string().max(2000, 'O texto não pode exceder 2000 caracteres.');
const MEASUREMENT = 'A medida tem de ser maior que 0 cm.';

/**
 * Regras de `CreateInitialAssessmentCommandValidator`, `AssessmentInputValidators` e
 * `InitialAssessment.ValidateParameters`: peso e altura > 0, gordura entre 0 e 100
 * (exclusivos), condição física 1–50 caracteres, profissão ≤ 255, medidas > 0, escalas
 * 1–5, água > 0 e textos de hábitos ≤ 2000.
 */
const assessmentSchema = z.object({
  weight_kg: z.number('Indica o peso.').positive('O peso tem de ser maior que 0 kg.'),
  height_cm: z
    .number('Indica a altura.')
    .int('A altura é em centímetros inteiros.')
    .positive('A altura tem de ser maior que 0 cm.'),
  body_fat_percentage: z
    .number('Indica uma percentagem entre 0 e 100.')
    .gt(0, 'Indica uma percentagem entre 0 e 100.')
    .lt(100, 'Indica uma percentagem entre 0 e 100.')
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
  goals: z.string().trim().min(1, 'Indica os objetivos.'),
  profession: z.string().trim().max(255, 'A profissão não pode exceder 255 caracteres.'),
  medical_conditions: z.string(),
  body_measurements: z.object({
    waist_cm: positiveOrNull(MEASUREMENT),
    hip_cm: positiveOrNull(MEASUREMENT),
    chest_cm: positiveOrNull(MEASUREMENT),
    right_arm_cm: positiveOrNull(MEASUREMENT),
    left_arm_cm: positiveOrNull(MEASUREMENT),
    right_thigh_cm: positiveOrNull(MEASUREMENT),
    left_thigh_cm: positiveOrNull(MEASUREMENT),
    right_calf_cm: positiveOrNull(MEASUREMENT),
    left_calf_cm: positiveOrNull(MEASUREMENT),
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
    avg_water_liters_per_day: positiveOrNull('A água tem de ser maior que 0L.'),
    hungriest_time_of_day: longText,
    uses_supplements: z.enum(['', 'yes', 'no']),
    current_supplements: longText,
    other_notes: longText,
  }),
});

/**
 * Valores do formulário: peso e altura começam vazios (`null`) numa avaliação nova; o
 * `safeParse` recusa esse `null` com a mensagem "Indica o peso/altura".
 */
type AssessmentValues = Omit<z.input<typeof assessmentSchema>, 'weight_kg' | 'height_cm'> & {
  weight_kg: number | null;
  height_cm: number | null;
};

const MEASUREMENT_LABELS: Record<keyof Measurements, string> = {
  waist_cm: 'Cintura (cm)',
  hip_cm: 'Anca (cm)',
  chest_cm: 'Peito (cm)',
  right_arm_cm: 'Braço direito (cm)',
  left_arm_cm: 'Braço esquerdo (cm)',
  right_thigh_cm: 'Coxa direita (cm)',
  left_thigh_cm: 'Coxa esquerda (cm)',
  right_calf_cm: 'Gémeo direito (cm)',
  left_calf_cm: 'Gémeo esquerdo (cm)',
};

const INTAKE_TEXT_LABELS = {
  food_preferences: 'Preferências alimentares',
  disliked_foods: 'Alimentos de que não gosta',
  food_intolerances: 'Intolerâncias',
  food_allergies: 'Alergias',
  dietary_restrictions: 'Restrições alimentares',
  daily_routine: 'Rotina diária',
  hungriest_time_of_day: 'Altura do dia com mais fome',
  current_supplements: 'Suplementos atuais',
  other_notes: 'Outras notas',
} as const;

const SCALE_LABELS = {
  sleep_quality: 'Qualidade do sono (1-5)',
  mood: 'Humor (1-5)',
  stress_level: 'Stress (1-5)',
} as const;

/** Campos de topo do backend (PascalCase) para os campos do formulário. */
const SERVER_FIELDS: Record<string, FieldPath<AssessmentValues>> = {
  WeightKg: 'weight_kg',
  HeightCm: 'height_cm',
  BodyFatPercentage: 'body_fat_percentage',
  FitnessLevel: 'fitness_level',
  ActivityLevel: 'activity_level',
  Goals: 'goals',
  Profession: 'profession',
};

function defaultsFrom(assessment: Assessment | null): AssessmentValues {
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
 * Formulário da avaliação inicial (criar e substituir).
 *
 * Medidas e hábitos ficam em secções recolhidas: são opcionais e longas, e o que o trainer
 * precisa de preencher sempre (peso, altura, níveis e objetivos) fica à vista.
 *
 * @param clientId Cliente avaliado.
 * @param assessment Avaliação existente, ou `null` para criar.
 * @param onSaved Chamado depois de guardar com sucesso.
 */
export function InitialAssessmentForm({
  clientId,
  assessment,
  onSaved,
}: {
  clientId: string;
  assessment: Assessment | null;
  onSaved: () => void;
}) {
  const mutation = useSaveInitialAssessmentMutation(clientId, assessment?.id ?? null);
  const defaults = defaultsFrom(assessment);
  const form = useForm<AssessmentValues>({ defaultValues: defaults });
  const errors = form.formState.errors;

  async function submit(values: AssessmentValues) {
    const parsed = assessmentSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues)
        form.setError(issue.path.join('.') as FieldPath<AssessmentValues>, {
          message: issue.message,
        });
      return;
    }

    const data = parsed.data;
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

    try {
      await mutation.mutateAsync({
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
      });
      toast.success('Avaliação inicial guardada.');
      onSaved();
    } catch (error) {
      if (isApiProblem(error) && error.code === 'initial_assessment_already_exists') {
        form.setError('root', {
          message: 'Este cliente já tem uma avaliação. Atualiza a página para a editar.',
        });
      } else if (isApiProblem(error) && error.hasFieldErrors) {
        let mapped = false;
        for (const fieldError of error.fieldErrors) {
          const field = SERVER_FIELDS[fieldError.field];
          if (field !== undefined) {
            form.setError(field, { message: fieldError.message });
            mapped = true;
          }
        }
        // Medidas e hábitos chegam com caminhos aninhados: aviso geral em vez de adivinhar.
        if (!mapped)
          form.setError('root', { message: 'Revê as medidas e os hábitos alimentares.' });
      } else {
        form.setError('root', { message: 'Não foi possível guardar. Tenta novamente.' });
      }
    }
  }

  const number = { setValueAs: toNumber };

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex h-full flex-col gap-4 pb-4"
    >
      <div className="grid gap-4 sm:grid-cols-3">
        <FormField label="Peso (kg)" error={errors.weight_kg?.message}>
          {(control) => (
            <Input {...control} type="number" step="0.1" {...form.register('weight_kg', number)} />
          )}
        </FormField>
        <FormField label="Altura (cm)" error={errors.height_cm?.message}>
          {(control) => (
            <Input {...control} type="number" step="1" {...form.register('height_cm', number)} />
          )}
        </FormField>
        <FormField label="Massa gorda (%)" error={errors.body_fat_percentage?.message}>
          {(control) => (
            <Input
              {...control}
              type="number"
              step="0.1"
              {...form.register('body_fat_percentage', number)}
            />
          )}
        </FormField>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Condição física" error={errors.fitness_level?.message}>
          {(control) => (
            <NativeSelect {...control} {...form.register('fitness_level')}>
              {fitnessLevelOptions(defaults.fitness_level).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </NativeSelect>
          )}
        </FormField>
        <FormField label="Nível de atividade" error={errors.activity_level?.message}>
          {(control) => (
            <NativeSelect {...control} {...form.register('activity_level')}>
              {Object.entries(ACTIVITY_LEVEL_LABELS).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </NativeSelect>
          )}
        </FormField>
      </div>
      <FormField label="Objetivos" error={errors.goals?.message}>
        {(control) => <Textarea {...control} rows={3} {...form.register('goals')} />}
      </FormField>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Profissão" error={errors.profession?.message}>
          {(control) => <Input {...control} {...form.register('profession')} />}
        </FormField>
        <FormField label="Condições médicas" error={errors.medical_conditions?.message}>
          {(control) => <Input {...control} {...form.register('medical_conditions')} />}
        </FormField>
      </div>

      <details className="border-border rounded-lg border p-3">
        <summary className="cursor-pointer text-sm font-medium">Medidas corporais</summary>
        <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3">
          {(Object.keys(MEASUREMENT_LABELS) as (keyof Measurements)[]).map((key) => (
            <FormField
              key={key}
              label={MEASUREMENT_LABELS[key]}
              error={errors.body_measurements?.[key]?.message}
            >
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  step="0.1"
                  {...form.register(`body_measurements.${key}`, number)}
                />
              )}
            </FormField>
          ))}
        </div>
      </details>

      <details className="border-border rounded-lg border p-3">
        <summary className="cursor-pointer text-sm font-medium">Hábitos e alimentação</summary>
        <div className="mt-3 grid gap-3">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {(Object.keys(SCALE_LABELS) as (keyof typeof SCALE_LABELS)[]).map((key) => (
              <FormField
                key={key}
                label={SCALE_LABELS[key]}
                error={errors.nutrition_intake?.[key]?.message}
              >
                {(control) => (
                  <Input
                    {...control}
                    type="number"
                    step="1"
                    {...form.register(`nutrition_intake.${key}`, number)}
                  />
                )}
              </FormField>
            ))}
            <FormField
              label="Água por dia (L)"
              error={errors.nutrition_intake?.avg_water_liters_per_day?.message}
            >
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  step="0.1"
                  {...form.register('nutrition_intake.avg_water_liters_per_day', number)}
                />
              )}
            </FormField>
          </div>
          <FormField label="Toma suplementos?">
            {(control) => (
              <NativeSelect {...control} {...form.register('nutrition_intake.uses_supplements')}>
                <option value="">Sem resposta</option>
                <option value="yes">Sim</option>
                <option value="no">Não</option>
              </NativeSelect>
            )}
          </FormField>
          {(Object.keys(INTAKE_TEXT_LABELS) as (keyof typeof INTAKE_TEXT_LABELS)[]).map((key) => (
            <FormField
              key={key}
              label={INTAKE_TEXT_LABELS[key]}
              error={errors.nutrition_intake?.[key]?.message}
            >
              {(control) => (
                <Textarea {...control} rows={2} {...form.register(`nutrition_intake.${key}`)} />
              )}
            </FormField>
          ))}
        </div>
      </details>

      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Guardar avaliação'}
        </Button>
      </div>
    </form>
  );
}
