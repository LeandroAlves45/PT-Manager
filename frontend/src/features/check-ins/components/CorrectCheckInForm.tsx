import { useForm, type FieldPath } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { useCorrectCheckInMutation } from '@/features/check-ins/api/mutations';
import { FEEDBACK_LABELS } from '@/features/check-ins/lib/answers';
import { checkInErrorMessage } from '@/features/check-ins/lib/checkInStatus';
import { MEASUREMENT_LABELS } from '@/features/clients';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';

type CheckIn = components['schemas']['CheckInResponse'];
type Measurements = components['schemas']['BodyMeasurementsPayload'];
type Feedback = components['schemas']['CheckInFeedbackPayload'];

/** Número opcional vindo de um `<input type="number">`: vazio fica `null`. */
const toNumber = (value: string | number | null) =>
  value == null || value === '' ? null : Number(value);

const MEASUREMENT = 'A medida tem de ser maior que 0 cm.';
const ADHERENCE = 'Indica uma valor inteiro de 0 a 100.';
const measurement = z.number(MEASUREMENT).positive(MEASUREMENT).nullable();
const adherence = z
  .number(ADHERENCE)
  .int(ADHERENCE)
  .min(0, ADHERENCE)
  .max(100, ADHERENCE)
  .nullable();
const longText = z.string().max(2000, 'O texto não pode exceder 2000 caracteres.');

/**
 * Regras de `CorrectCheckInCommandValidator` e `AssessmentInputValidators`: peso > 0,
 * gordura entre 0 e 100 (exclusivos), adesões 0–100, medidas > 0, notas e respostas
 * ≤ 2000 caracteres, data-alvo nunca antes do dia do check-in.
 */
function correctionSchema(checkInDate: string) {
  return z.object({
    target_date: z
      .string()
      .refine(
        (value) => value === '' || value >= checkInDate,
        'A data-alvo não pode ser anterior ao check-in.'
      ),
    weight_kg: z.number('Indica o peso.').positive('O peso tem de ser maior que 0 kg.'),
    body_fat_percentage: z
      .number('Indica uma percentagem entre 0 e 100.')
      .gt(0, 'Indica uma percentagem entre 0 e 100.')
      .lt(100, 'Indica uma percentagem entre 0 e 100.')
      .nullable(),
    notes: longText,
    training_adherence_score: adherence,
    nutrition_adherence_score: adherence,
    body_measurements: z.object({
      waist_cm: measurement,
      hip_cm: measurement,
      chest_cm: measurement,
      right_arm_cm: measurement,
      left_arm_cm: measurement,
      right_thigh_cm: measurement,
      left_thigh_cm: measurement,
      right_calf_cm: measurement,
      left_calf_cm: measurement,
    }),
    feedback: z.object({
      appetite: longText,
      digestion: longText,
      training_load: longText,
      recovery_sleep: longText,
      energy_levels: longText,
      body_response: longText,
    }),
  });
}

/** Valores do formulário: textos vazios em vez de 'null', para os campos controlados. */
interface CorrectionValues {
  target_date: string;
  weight_kg: number | null;
  body_fat_percentage: number | null;
  notes: string;
  training_adherence_score: number | null;
  nutrition_adherence_score: number | null;
  body_measurements: Measurements;
  feedback: Record<keyof Feedback, string>;
}

/** Campos de topo do servidor; medidas e respostas chegam com caminhos aninhados. */
const SERVER_FIELDS: Readonly<Record<string, FieldPath<CorrectionValues>>> = {
  TargetDate: 'target_date',
  WeightKg: 'weight_kg',
  BodyFatPercentage: 'body_fat_percentage',
  Notes: 'notes',
  TrainingAdherenceScore: 'training_adherence_score',
  NutritionAdherenceScore: 'nutrition_adherence_score',
};

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());

function toValues(checkIn: CheckIn): CorrectionValues {
  const feedback = checkIn.feedback;
  return {
    target_date: checkIn.target_date ?? '',
    weight_kg: checkIn.weight_kg,
    body_fat_percentage: checkIn.body_fat_percentage,
    notes: checkIn.notes ?? '',
    training_adherence_score: checkIn.training_adherence_score,
    nutrition_adherence_score: checkIn.nutrition_adherence_score,
    body_measurements: { ...checkIn.body_measurements },
    feedback: {
      appetite: feedback.appetite ?? '',
      digestion: feedback.digestion ?? '',
      training_load: feedback.training_load ?? '',
      recovery_sleep: feedback.recovery_sleep ?? '',
      energy_levels: feedback.energy_levels ?? '',
      body_response: feedback.body_response ?? '',
    },
  };
}

/**
 * Correção de um check-in respondido pelo personal trainer (`PUT /check-ins/{id}/answer`).
 *
 * Pré-preenchido com a resposta atual; o `PUT` substitui todos os valores, por isso um campo
 * esvaziado passa a `null`. Corrigir não marca como revisto: são ações separadas.
 *
 * @param checkIn Check-in respondido a corrigir.
 * @param onSaved Chamado depois de guardar.
 */
export function CorrectCheckInForm({
  checkIn,
  onSaved,
}: {
  checkIn: CheckIn;
  onSaved: () => void;
}) {
  const mutation = useCorrectCheckInMutation();
  const form = useForm<CorrectionValues>({ defaultValues: toValues(checkIn) });
  const errors = form.formState.errors;

  async function submit(values: CorrectionValues) {
    const parsed = correctionSchema(checkIn.check_in_date).safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues)
        form.setError(issue.path.join('.') as FieldPath<CorrectionValues>, {
          message: issue.message,
        });
      return;
    }

    const data = parsed.data;
    const fallback = 'Não foi possível guardar a correção. Tenta novamente.';
    try {
      await mutation.mutateAsync({
        checkInId: checkIn.id,
        body: {
          target_date: data.target_date === '' ? null : data.target_date,
          weight_kg: data.weight_kg,
          body_fat_percentage: data.body_fat_percentage,
          notes: orNull(data.notes),
          training_adherence_score: data.training_adherence_score,
          nutrition_adherence_score: data.nutrition_adherence_score,
          body_measurements: data.body_measurements,
          feedback: {
            appetite: orNull(data.feedback.appetite),
            digestion: orNull(data.feedback.digestion),
            training_load: orNull(data.feedback.training_load),
            recovery_sleep: orNull(data.feedback.recovery_sleep),
            energy_levels: orNull(data.feedback.energy_levels),
            body_response: orNull(data.feedback.body_response),
          },
        },
      });
      toast.success(`Check-in de ${checkIn.client_name} corrigido.`);
      onSaved();
    } catch (error) {
      if (!isApiProblem(error)) {
        form.setError('root', { message: fallback });
        return;
      }
      let mapped = false;
      for (const fieldError of error.fieldErrors) {
        const field = SERVER_FIELDS[fieldError.field];
        if (field !== undefined) {
          form.setError(field, { message: fieldError.message });
          mapped = true;
        }
      }
      if (!mapped)
        form.setError('root', {
          message: error.hasFieldErrors
            ? 'Revê as medidas e respostas.'
            : checkInErrorMessage(error.code, fallback),
        });
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
        <FormField label="Data-alvo" error={errors.target_date?.message}>
          {(control) => <Input {...control} type="date" {...form.register('target_date')} />}
        </FormField>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Adesão ao treino (%)" error={errors.training_adherence_score?.message}>
          {(control) => (
            <Input
              {...control}
              type="number"
              step="1"
              {...form.register('training_adherence_score', number)}
            />
          )}
        </FormField>
        <FormField
          label="Adesão à alimentação (%)"
          error={errors.nutrition_adherence_score?.message}
        >
          {(control) => (
            <Input
              {...control}
              type="number"
              step="1"
              {...form.register('nutrition_adherence_score', number)}
            />
          )}
        </FormField>
      </div>
      <fieldset className="space-y-3">
        <legend className="mb-2 font-medium">Medidas</legend>
        <div className="grid gap-4 sm:grid-cols-3">
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
      </fieldset>
      <fieldset className="space-y-3">
        <legend className="mb-2 font-medium">Respostas</legend>
        {(Object.keys(FEEDBACK_LABELS) as (keyof Feedback)[]).map((key) => (
          <FormField key={key} label={FEEDBACK_LABELS[key]} error={errors.feedback?.[key]?.message}>
            {(control) => <Textarea {...control} rows={2} {...form.register(`feedback.${key}`)} />}
          </FormField>
        ))}
        <FormField label="Notas" error={errors.notes?.message}>
          {(control) => <Textarea {...control} rows={3} {...form.register('notes')} />}
        </FormField>
      </fieldset>
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Guardar correção'}
        </Button>
      </div>
    </form>
  );
}
