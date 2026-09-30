import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { useSaveSupplementMutation } from '@/features/library/api/mutations';
import { applyServerErrors, applyZodIssues } from '@/features/library/lib/errors';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';

type Supplement = components['schemas']['SupplementResponse'];

/**
 * Regras iguais a `CreateSupplementCommandValidator`: nome 1–255, unidade 1–50, dose 1–100 e
 * momento 1–255 obrigatórios; descrição e notas sem limite no backend.
 */
const supplementSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Indica o nome do suplemento.')
    .max(255, 'O nome não pode exceder 255 caracteres.'),
  description: z.string(),
  unit_of_measure: z
    .string()
    .trim()
    .min(1, 'Indica a unidade de medida.')
    .max(50, 'A unidade não pode exceder 50 caracteres.'),
  serving_size: z
    .string()
    .trim()
    .min(1, 'Indica a dose.')
    .max(100, 'A dose não pode exceder 100 caracteres.'),
  timing: z
    .string()
    .trim()
    .min(1, 'Indica quando tomar.')
    .max(255, 'O momento não pode exceder 255 caracteres.'),
  trainer_notes: z.string(),
});
type SupplementValues = z.input<typeof supplementSchema>;

const FIELDS = [
  'name',
  'description',
  'unit_of_measure',
  'serving_size',
  'timing',
  'trainer_notes',
] as const;

/** Campos do comando no backend → campos do formulário. */
const SERVER_FIELDS: Record<string, (typeof FIELDS)[number]> = {
  Name: 'name',
  UnitOfMeasure: 'unit_of_measure',
  ServingSize: 'serving_size',
  Timing: 'timing',
};

/**
 * Formulário do suplemento privado (criar e editar).
 *
 * @param supplement Suplemento a editar, ou `null` para criar.
 */
export function SupplementForm({
  supplement,
  onSaved,
}: {
  supplement: Supplement | null;
  onSaved: () => void;
}) {
  const mutation = useSaveSupplementMutation(supplement?.id ?? null);
  const form = useForm<SupplementValues>({
    defaultValues: {
      name: supplement?.name ?? '',
      description: supplement?.description ?? '',
      unit_of_measure: supplement?.unit_of_measure ?? '',
      serving_size: supplement?.serving_size ?? '',
      timing: supplement?.timing ?? '',
      trainer_notes: supplement?.trainer_notes ?? '',
    },
  });
  const errors = form.formState.errors;

  async function submit(values: SupplementValues) {
    const parsed = supplementSchema.safeParse(values);
    if (!parsed.success) {
      applyZodIssues(parsed.error.issues, FIELDS, form.setError);
      return;
    }

    const data = parsed.data;
    try {
      await mutation.mutateAsync({
        name: data.name,
        description: data.description.trim() || null,
        unit_of_measure: data.unit_of_measure,
        serving_size: data.serving_size,
        timing: data.timing,
        trainer_notes: data.trainer_notes.trim() || null,
      });
      toast.success(
        supplement === null
          ? 'Suplemento criado com sucesso.'
          : 'Suplemento atualizado com sucesso.'
      );
      onSaved();
    } catch (error) {
      applyServerErrors(error, SERVER_FIELDS, form.setError);
    }
  }

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex flex-col gap-4"
    >
      <FormField label="Nome" error={errors.name?.message}>
        {(control) => <Input {...control} autoComplete="off" {...form.register('name')} />}
      </FormField>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Dose" error={errors.serving_size?.message}>
          {(control) => (
            <Input {...control} placeholder="ex.: 5" {...form.register('serving_size')} />
          )}
        </FormField>
        <FormField label="Unidade de medida" error={errors.unit_of_measure?.message}>
          {(control) => (
            <Input {...control} placeholder="ex.: g" {...form.register('unit_of_measure')} />
          )}
        </FormField>
      </div>
      <FormField label="Quando tomar" error={errors.timing?.message}>
        {(control) => (
          <Input {...control} placeholder="ex.: Depois do treino" {...form.register('timing')} />
        )}
      </FormField>
      <FormField label="Descrição" error={errors.description?.message}>
        {(control) => <Textarea {...control} rows={3} {...form.register('description')} />}
      </FormField>
      <FormField label="Notas" error={errors.trainer_notes?.message}>
        {(control) => <Textarea {...control} rows={3} {...form.register('trainer_notes')} />}
      </FormField>
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="flex justify-end">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending
            ? 'A guardar…'
            : supplement === null
              ? 'Criar suplemento'
              : 'Guardar alterações'}
        </Button>
      </div>
    </form>
  );
}
