import type { FieldPath, UseFormReturn } from 'react-hook-form';

import {
  ACTIVITY_LEVEL_LABELS,
  fitnessLevelOptions,
  MEASUREMENT_LABELS,
} from '@/features/clients/lib/labels';
import {
  INTAKE_TEXT_LABELS,
  MEASUREMENT_KEYS,
  SCALE_LABELS,
  toNumber,
  type AssessmentValues,
} from '@/features/initial-assessment/lib/initialAssessmentSchema';
import { FormField } from '@/shared/components/FormField';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';
import { Textarea } from '@/shared/components/ui/textarea';

/** Ligação dos grupos de campos ao formulário. */
export interface AssessmentFieldsProps {
  form: UseFormReturn<AssessmentValues>;
}

const number = { setValueAs: toNumber };

/**
 * Corpo e objetivos: peso, altura, massa gorda, níveis, objetivos, profissão e condições
 * médicas. Com condição física e atividade por escolher (`''`) mostra uma opção "Escolhe…".
 */
export function BodyAndGoalsFields({ form }: AssessmentFieldsProps) {
  const errors = form.formState.errors;
  const initial = form.formState.defaultValues;
  const fitnessOptions = fitnessLevelOptions(initial?.fitness_level ?? '');

  return (
    <>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <FormField label="Peso (kg)" error={errors.weight_kg?.message}>
          {(control) => (
            <Input
              {...control}
              type="number"
              inputMode="decimal"
              step="0.01"
              {...form.register('weight_kg', number)}
            />
          )}
        </FormField>
        <FormField label="Altura (cm)" error={errors.height_cm?.message}>
          {(control) => (
            <Input
              {...control}
              type="number"
              inputMode="numeric"
              step="1"
              {...form.register('height_cm', number)}
            />
          )}
        </FormField>
        <FormField label="Massa gorda (%)" error={errors.body_fat_percentage?.message}>
          {(control) => (
            <Input
              {...control}
              type="number"
              inputMode="decimal"
              step="0.01"
              {...form.register('body_fat_percentage', number)}
            />
          )}
        </FormField>
      </div>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <FormField label="Condição física" error={errors.fitness_level?.message}>
          {(control) => (
            <NativeSelect {...control} {...form.register('fitness_level')}>
              {initial?.fitness_level === '' && (
                <option value="" disabled>
                  Escolhe…
                </option>
              )}
              {fitnessOptions.map(([value, label]) => (
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
              {initial?.activity_level === '' && (
                <option value="" disabled>
                  Escolhe…
                </option>
              )}
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
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <FormField label="Profissão" error={errors.profession?.message}>
          {(control) => <Input {...control} {...form.register('profession')} />}
        </FormField>
        <FormField label="Condições médicas" error={errors.medical_conditions?.message}>
          {(control) => <Input {...control} {...form.register('medical_conditions')} />}
        </FormField>
      </div>
    </>
  );
}

/** Perímetros corporais, todos opcionais. */
export function MeasurementsFields({ form }: AssessmentFieldsProps) {
  const errors = form.formState.errors;

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
      {MEASUREMENT_KEYS.map((key) => (
        <FormField
          key={key}
          label={MEASUREMENT_LABELS[key]}
          error={errors.body_measurements?.[key]?.message}
        >
          {(control) => (
            <Input
              {...control}
              type="number"
              inputMode="decimal"
              step="0.01"
              {...form.register(`body_measurements.${key}` as FieldPath<AssessmentValues>, number)}
            />
          )}
        </FormField>
      ))}
    </div>
  );
}

/** Hábitos e saúde: escalas, água, suplementos e textos livres, todos opcionais. */
export function HabitsFields({ form }: AssessmentFieldsProps) {
  const errors = form.formState.errors;

  return (
    <div className="grid grid-cols-1 gap-3">
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
                inputMode="numeric"
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
              inputMode="decimal"
              step="0.01"
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
  );
}
