import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { catalogKeys } from '@/features/admin-catalog/api/catalog';
import { ExerciseVideoPanel } from '@/features/admin-catalog/components/ExerciseVideoPanel';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

const exerciseSchema = z.object({
  name: z.string().trim().min(1, 'Indica o nome do exercício.').max(255),
  description: z.string(),
  muscle_groups: z.string().max(500),
  equipment: z.string().max(255),
  difficulty_level: z.string().max(50),
});
type ExerciseValues = z.infer<typeof exerciseSchema>;
type Exercise = components['schemas']['GlobalExerciseResponse'];

/** O formulário não altera a ligação externa; o vídeo gerido tem painel próprio. */
export function ExerciseForm({ item, onSaved }: { item: Exercise | null; onSaved: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm<ExerciseValues>({
    defaultValues: {
      name: item?.name ?? '',
      description: item?.description ?? '',
      muscle_groups: item?.muscle_groups ?? '',
      equipment: item?.equipment ?? '',
      difficulty_level: item?.difficulty_level ?? '',
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: ExerciseValues) => {
      const body = {
        name: values.name,
        description: values.description || null,
        muscle_groups: values.muscle_groups || null,
        equipment: values.equipment || null,
        difficulty_level: values.difficulty_level || null,
        video_url: item?.video_url ?? null,
      };
      return item === null
        ? unwrap(await apiClient.POST('/api/v1/global-exercises', { body }))
        : unwrap(
            await apiClient.PATCH('/api/v1/global-exercises/{exerciseId}', {
              params: { path: { exerciseId: item.id } },
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
        item === null ? 'Exercício criado com sucesso.' : 'Exercício atualizado com sucesso.'
      );
      onSaved();
    },
  });

  async function submit(values: ExerciseValues) {
    const parsed = exerciseSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof ExerciseValues, { message: issue.message });
      }
      return;
    }

    try {
      await mutation.mutateAsync(parsed.data);
    } catch (error) {
      if (isApiProblem(error) && error.hasFieldErrors) {
        const fields: Record<string, keyof ExerciseValues> = {
          Name: 'name',
          MuscleGroups: 'muscle_groups',
          Equipment: 'equipment',
          DifficultyLevel: 'difficulty_level',
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
      {(['name', 'description', 'muscle_groups', 'equipment', 'difficulty_level'] as const).map(
        (field) => (
          <label key={field} className="space-y-1 text-sm">
            {
              {
                name: 'Nome',
                description: 'Descrição',
                muscle_groups: 'Grupos musculares',
                equipment: 'Equipamento',
                difficulty_level: 'Dificuldade',
              }[field]
            }
            <Input {...form.register(field)} aria-invalid={!!form.formState.errors[field]} />
            {form.formState.errors[field] && (
              <span role="alert" className="text-destructive">
                {form.formState.errors[field]?.message}
              </span>
            )}
          </label>
        )
      )}
      {item && <ExerciseVideoPanel exercise={item} />}
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
