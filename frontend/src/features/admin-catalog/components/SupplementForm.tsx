import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { catalogKeys } from '@/features/admin-catalog/api/catalog';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

const supplementSchema = z.object({
  name: z.string().trim().min(1, 'Indica o nome do suplemento.').max(255),
  description: z.string(),
  unit_of_measure: z.string().trim().min(1, 'Indica a unidade.').max(50),
  serving_size: z.string().trim().min(1, 'Indica a dose.').max(100),
  timing: z.string().trim().min(1, 'Indica quando tomar o suplemento.').max(255),
  trainer_notes: z.string(),
});
type SupplementValues = z.infer<typeof supplementSchema>;
type Supplement = components['schemas']['GlobalSupplementResponse'];

/** Formulário próprio dos suplementos globais. */
export function SupplementForm({
  item,
  onSaved,
}: {
  item: Supplement | null;
  onSaved: () => void;
}) {
  const queryClient = useQueryClient();
  const form = useForm<SupplementValues>({
    defaultValues: {
      name: item?.name ?? '',
      description: item?.description ?? '',
      unit_of_measure: item?.unit_of_measure ?? '',
      serving_size: item?.serving_size ?? '',
      timing: item?.timing ?? '',
      trainer_notes: item?.trainer_notes ?? '',
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: SupplementValues) => {
      const body = {
        ...values,
        description: values.description || null,
        trainer_notes: values.trainer_notes || null,
      };
      return item === null
        ? unwrap(await apiClient.POST('/api/v1/global-supplements', { body }))
        : unwrap(
            await apiClient.PATCH('/api/v1/global-supplements/{supplementId}', {
              params: { path: { supplementId: item.id } },
              body,
            })
          );
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: catalogKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
      ]);
      toast.success(
        item === null ? 'Suplemento criado com sucesso.' : 'Suplemento atualizado com sucesso.'
      );
      onSaved();
    },
  });

  async function submit(values: SupplementValues) {
    const parsed = supplementSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof SupplementValues, { message: issue.message });
      }
      return;
    }

    try {
      await mutation.mutateAsync(parsed.data);
    } catch (error) {
      if (isApiProblem(error) && error.hasFieldErrors) {
        const fields: Record<string, keyof SupplementValues> = {
          Name: 'name',
          UnitOfMeasure: 'unit_of_measure',
          ServingSize: 'serving_size',
          Timing: 'timing',
        };
        for (const fieldError of error.fieldErrors) {
          const field = fields[fieldError.field];
          if (field) form.setError(field, { message: fieldError.message });
        }
      } else form.setError('root', { message: 'Não foi possível guardar. Tenta novamente.' });
    }
  }

  return (
    <form
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex h-full flex-col gap-4 pb-4"
    >
      {(
        [
          'name',
          'description',
          'unit_of_measure',
          'serving_size',
          'timing',
          'trainer_notes',
        ] as const
      ).map((field) => (
        <label key={field} className="space-y-1 text-sm">
          {
            {
              name: 'Nome',
              description: 'Descrição',
              unit_of_measure: 'Unidade',
              serving_size: 'Dose',
              timing: 'Momento',
              trainer_notes: 'Notas para o personal trainer',
            }[field]
          }
          <Input {...form.register(field)} aria-invalid={!!form.formState.errors[field]} />
          {form.formState.errors[field] && (
            <span role="alert" className="text-destructive">
              {form.formState.errors[field]?.message}
            </span>
          )}
        </label>
      ))}
      {form.formState.errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {form.formState.errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Guardar alterações'}
        </Button>
      </div>
    </form>
  );
}
