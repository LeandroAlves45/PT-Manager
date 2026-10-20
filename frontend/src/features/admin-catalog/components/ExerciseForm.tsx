import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useId, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { catalogKeys } from '@/features/admin-catalog/api/catalog';
import { setPendingExerciseVideo } from '@/features/admin-catalog/lib/pending-video';
import { ExerciseVideoPanel } from '@/features/exercise-video';
import {
  ACCEPTED_VIDEO_TYPES,
  validateVideoFile,
} from '@/features/exercise-video/lib/exerciseVideo';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { MuscleGroupPicker } from '@/shared/components/MuscleGroupPicker';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';
import {
  parseMuscleGroups,
  serializeMuscleGroups,
  type MuscleGroupCode,
} from '@/shared/lib/muscleGroups';

const exerciseSchema = z.object({
  name: z.string().trim().min(1, 'Indica o nome do exercício.').max(255),
  description: z.string(),
  muscle_groups: z.array(z.custom<MuscleGroupCode>()),
  equipment: z.string().max(255),
  difficulty_level: z.string().max(50),
});
type ExerciseValues = z.infer<typeof exerciseSchema>;
type Exercise = components['schemas']['GlobalExerciseResponse'];

/**
 * O formulário não altera a ligação externa; o vídeo gerido tem painel próprio.
 *
 * Ao criar, o vídeo é opcional: o ficheiro fica à espera do id do exercício (`onSaved` recebe
 * `withVideo`) e o envio começa na página de edição.
 */
export function ExerciseForm({
  item,
  onSaved,
  initialFile = null,
}: {
  item: Exercise | null;
  onSaved: (saved: Exercise, withVideo: boolean) => void;
  initialFile?: File | null;
}) {
  const queryClient = useQueryClient();
  const videoInputId = useId();
  const [video, setVideo] = useState<File | null>(null);
  const [videoError, setVideoError] = useState<string | null>(null);
  const [saved, setSaved] = useState<{ exercise: Exercise; withVideo: boolean } | null>(null);
  const form = useForm<ExerciseValues>({
    defaultValues: {
      name: item?.name ?? '',
      description: item?.description ?? '',
      muscle_groups: parseMuscleGroups(item?.muscle_groups ?? null),
      equipment: item?.equipment ?? '',
      difficulty_level: item?.difficulty_level ?? '',
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: ExerciseValues) => {
      const body = {
        name: values.name,
        description: values.description || null,
        muscle_groups: serializeMuscleGroups(values.muscle_groups),
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
    onSuccess: async (result) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: catalogKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
      ]);
      toast.success(
        item === null ? 'Exercício criado com sucesso.' : 'Exercício atualizado com sucesso.'
      );
      if (item === null && video !== null) setPendingExerciseVideo(result.id, video);
      setSaved({ exercise: result, withVideo: item === null && video !== null });
    },
  });
  // Sair durante o envio do vídeo cancela-o (o painel desmonta): também conta para o guard.
  const [uploading, setUploading] = useState(false);
  const blocker = useUnsavedChangesGuard(
    (form.formState.isDirty || video !== null || uploading) && saved === null
  );
  // Só depois de o render sem «alterações por guardar» é que se sai: assim o guard não bloqueia
  // a navegação que o próprio guardar provoca.
  useEffect(() => {
    if (saved !== null) onSaved(saved.exercise, saved.withVideo);
  }, [saved, onSaved]);

  async function submit(values: ExerciseValues) {
    const fileError = item === null && video !== null ? validateVideoFile(video) : null;
    setVideoError(fileError);
    if (fileError !== null) return;

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
      className="flex max-w-xl flex-col gap-4 pb-4"
    >
      {(['name', 'description', 'equipment', 'difficulty_level'] as const).map((field) => (
        <label key={field} className="space-y-1 text-sm">
          {
            {
              name: 'Nome',
              description: 'Descrição',
              equipment: 'Equipamento',
              difficulty_level: 'Dificuldade',
            }[field]
          }
          <Input
            {...form.register(field)}
            placeholder={field === 'equipment' ? 'ex.: Barra' : undefined}
            aria-invalid={!!form.formState.errors[field]}
          />
          {form.formState.errors[field] && (
            <span role="alert" className="text-destructive">
              {form.formState.errors[field]?.message}
            </span>
          )}
        </label>
      ))}
      {/* [6E-3] Lista fechada do MuscleGroupCatalog: texto livre dava 400 com códigos desconhecidos. */}
      <div className="space-y-1 text-sm">
        <label htmlFor="global-exercise-muscle-groups">Grupos musculares</label>
        <Controller
          control={form.control}
          name="muscle_groups"
          render={({ field }) => (
            <MuscleGroupPicker
              id="global-exercise-muscle-groups"
              value={field.value}
              onChange={field.onChange}
              aria-invalid={!!form.formState.errors.muscle_groups}
            />
          )}
        />
        {form.formState.errors.muscle_groups && (
          <span role="alert" className="text-destructive">
            {form.formState.errors.muscle_groups.message}
          </span>
        )}
      </div>
      {item === null && (
        <div className="space-y-1 text-sm">
          <label htmlFor={videoInputId}>Vídeo (opcional)</label>
          <Input
            id={videoInputId}
            type="file"
            accept={ACCEPTED_VIDEO_TYPES.join(',')}
            className="min-h-11 md:min-h-8"
            aria-invalid={videoError !== null}
            onChange={(event) => {
              setVideo(event.target.files?.[0] ?? null);
              setVideoError(null);
            }}
          />
          <p className="text-muted-foreground text-xs">
            MP4 ou MOV, H.264, até 100 MB e 3 minutos.
          </p>
          {videoError !== null && (
            <span role="alert" className="text-destructive">
              {videoError}
            </span>
          )}
        </div>
      )}
      {item && (
        <ExerciseVideoPanel
          exercise={item}
          audience="superuser"
          listKey={catalogKeys.all}
          initialFile={initialFile}
          onUploadingChange={setUploading}
        />
      )}
      {form.formState.errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {form.formState.errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end border-t pt-4">
        <Button type="submit" className="min-h-11 md:min-h-8" disabled={mutation.isPending}>
          {item === null
            ? mutation.isPending
              ? 'A criar…'
              : 'Criar'
            : mutation.isPending
              ? 'A guardar…'
              : 'Guardar'}
        </Button>
      </div>
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title={uploading ? 'Sair durante o envio do vídeo?' : 'Sair sem guardar?'}
        description={
          uploading
            ? 'O vídeo ainda está a ser enviado. Se saíres, o envio é cancelado.'
            : 'As alterações a este item perdem-se.'
        }
        confirmLabel="Sair sem guardar"
        cancelLabel="Continuar a editar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </form>
  );
}
