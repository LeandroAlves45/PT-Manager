import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { catalogKeys } from '@/features/admin-catalog/api/catalog';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

/** Grama por 100 g: obrigatório, entre 0 e 100, com mensagens pt-PT em vez das do Zod. */
const gramsPer100 = z
  .number({ error: 'Indica um valor entre 0 e 100.' })
  .min(0, 'Indica um valor entre 0 e 100.')
  .max(100, 'Indica um valor entre 0 e 100.');

/** Validar formulário com Zod; verifica se o formulário é válido antes de enviar. */
const foodSchema = z
  .object({
    name: z.string().trim().min(1, 'Indica o nome do alimento.').max(255),
    description: z.string(),
    protein: gramsPer100,
    carbs: gramsPer100,
    fats: gramsPer100,
    fiber: gramsPer100.nullable(),
    // Limite do domínio (`Food.MaxDefaultServingGrams`): uma porção pode passar de 100 g.
    default_serving_grams: z
      .number()
      .positive('A porção tem de ser maior que 0 g.')
      .max(1000, 'A porção não pode exceder 1000 g.')
      .nullable(),
  })
  .refine((food) => food.protein + food.carbs + food.fats <= 100, {
    path: ['protein'],
    message: 'A soma das macros não pode exceder 100g.',
  });
type FoodValues = z.infer<typeof foodSchema>;
type Food = components['schemas']['GlobalFoodResponse'];

/** Formulário próprio dos alimentos; kcal nunca integra o pedido de escrita. */
export function FoodForm({ item, onSaved }: { item: Food | null; onSaved: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm<FoodValues>({
    defaultValues: {
      name: item?.name ?? '',
      description: item?.description ?? '',
      protein: item?.protein ?? 0,
      carbs: item?.carbs ?? 0,
      fats: item?.fats ?? 0,
      fiber: item?.fiber ?? null,
      default_serving_grams: item?.default_serving_grams ?? null,
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: FoodValues) => {
      const body = { ...values, description: values.description || null };
      return item === null
        ? unwrap(await apiClient.POST('/api/v1/global-foods', { body }))
        : unwrap(
            await apiClient.PATCH('/api/v1/global-foods/{foodId}', {
              params: { path: { foodId: item.id } },
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
        item === null ? 'Alimento criado com sucesso.' : 'Alimento atualizado com sucesso.'
      );
      onSaved();
    },
  });

  const macros = useWatch({ control: form.control, name: ['protein', 'carbs', 'fats'] });
  const kcal =
    (Number(macros[0]) || 0) * 4 + (Number(macros[1]) || 0) * 4 + (Number(macros[2]) || 0) * 9;

  async function submit(values: FoodValues) {
    const parsed = foodSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof FoodValues, { message: issue.message });
      }
      return;
    }

    try {
      await mutation.mutateAsync(parsed.data);
    } catch (error) {
      if (isApiProblem(error) && error.hasFieldErrors) {
        const fields: Record<string, keyof FoodValues> = {
          Name: 'name',
          Protein: 'protein',
          Carbs: 'carbs',
          Fats: 'fats',
          Fiber: 'fiber',
          DefaultServingGrams: 'default_serving_grams',
          Macros: 'protein',
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
      <label className="space-y-1 text-sm">
        Nome
        <Input {...form.register('name')} aria-invalid={!!form.formState.errors.name} />
        {form.formState.errors.name && (
          <span role="alert" className="text-destructive">
            {form.formState.errors.name.message}
          </span>
        )}
      </label>
      <label className="space-y-1 text-sm">
        Descrição
        <Input {...form.register('description')} />
      </label>
      <div className="grid grid-cols-2 gap-3">
        {(['protein', 'carbs', 'fats', 'fiber', 'default_serving_grams'] as const).map((field) => (
          <label key={field} className="space-y-1 text-sm">
            {
              {
                protein: 'Proteína (g)',
                carbs: 'Hidratos (g)',
                fats: 'Gordura (g)',
                fiber: 'Fibra (g)',
                default_serving_grams: 'Porção padrão (g)',
              }[field]
            }
            <Input
              type="number"
              min="0"
              max={field === 'default_serving_grams' ? 1000 : 100}
              step="0.01"
              {...form.register(field, {
                setValueAs: (value: string | null) =>
                  value == null || value === '' ? null : Number(value),
              })}
              aria-invalid={!!form.formState.errors[field]}
            />
            {form.formState.errors[field] && (
              <span role="alert" className="text-destructive">
                {form.formState.errors[field]?.message}
              </span>
            )}
          </label>
        ))}
      </div>
      <p className="text-sm">
        Energia calculada: <strong>{Math.round(kcal)} kcal/100 g</strong>
      </p>
      {form.formState.errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {form.formState.errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Guardar alterações'}
        </Button>
      </div>
    </form>
  );
}
