import { useEffect, useId, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { ExerciseVideoPanel } from '@/features/exercise-video';
import {
  ACCEPTED_VIDEO_TYPES,
  validateVideoFile,
} from '@/features/exercise-video/lib/exerciseVideo';
import { libraryKeys } from '@/features/library/api/keys';
import { useSaveExerciseMutation } from '@/features/library/api/mutations';
import { UnsavedChangesDialog } from '@/features/library/components/UnsavedChangesDialog';
import { applyServerErrors, applyZodIssues } from '@/features/library/lib/errors';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { MuscleGroupPicker } from '@/shared/components/MuscleGroupPicker';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import { setPendingExerciseVideo } from '@/shared/lib/pending-video';
import {
  parseMuscleGroups,
  serializeMuscleGroups,
  type MuscleGroupCode,
} from '@/shared/lib/muscleGroups';

type Exercise = components['schemas']['ExerciseResponse'];

/**
 * Regras iguais a `CreateExerciseCommandValidator`: nome 1–255, equipamento ≤ 255, dificuldade
 * ≤ 50 (texto livre, F11), grupos musculares só da lista fechada. A descrição não tem limite no
 * backend.
 */
const exerciseSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Indica o nome do exercício.')
    .max(255, 'O nome não pode exceder 255 caracteres.'),
  description: z.string(),
  muscle_groups: z.array(z.custom<MuscleGroupCode>()),
  equipment: z.string().trim().max(255, 'O equipamento não pode exceder 255 caracteres.'),
  difficulty_level: z.string().trim().max(50, 'A dificuldade não pode exceder 50 caracteres.'),
});
type ExerciseValues = z.input<typeof exerciseSchema>;

const FIELDS = ['name', 'description', 'muscle_groups', 'equipment', 'difficulty_level'] as const;

/** Campos e comandos no backend -> campos do formulário. */
const SERVER_FIELDS: Record<string, (typeof FIELDS)[number]> = {
  Name: 'name',
  MuscleGroups: 'muscle_groups',
  Equipment: 'equipment',
  DifficultyLevel: 'difficulty_level',
};

/**
 * Formulário do exercício privado (criar e editar). Ao editar, mostra o painel do vídeo 5D.
 *
 * Ao criar, o vídeo é opcional: o ficheiro fica à espera do id do exercício
 * (`onSaved` recebe `withVideo`) e o envio começa na página de edição do exercício novo.
 *
 * Avisa ao sair com alterações por guardar, com um ficheiro escolhido ou com um envio em
 * curso (sair cancela-o, porque o painel do vídeo desmonta).
 *
 * `video_url` (ligação externa antiga) não aparece e é reenviado tal como está: o PATCH
 * substitui todos os campos e apagá-lo seria uma perda silenciosa.
 *
 * @param exercise Exercício a editar, ou `null` para criar.
 * @param onSaved Chamado depois de guardar, já sem o aviso de saída ativo.
 * @param initialFile Ficheiro escolhido ao criar; o painel do vídeo começa o envio uma vez.
 */
export function ExerciseForm({
  exercise,
  onSaved,
  initialFile = null,
}: {
  exercise: Exercise | null;
  onSaved: (saved: Exercise, withVideo: boolean) => void;
  initialFile?: File | null;
}) {
  const videoInputId = useId();
  const [video, setVideo] = useState<File | null>(null);
  const [videoError, setVideoError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [saved, setSaved] = useState<{ exercise: Exercise; withVideo: boolean } | null>(null);
  const mutation = useSaveExerciseMutation(exercise?.id ?? null);
  const form = useForm<ExerciseValues>({
    defaultValues: {
      name: exercise?.name ?? '',
      description: exercise?.description ?? '',
      muscle_groups: parseMuscleGroups(exercise?.muscle_groups ?? null),
      equipment: exercise?.equipment ?? '',
      difficulty_level: exercise?.difficulty_level ?? '',
    },
  });
  const errors = form.formState.errors;

  // Só depois de o render sem «alterações por guardar» é que se sai: o guard não bloqueia a
  // navegação que o próprio guardar provoca.
  useEffect(() => {
    if (saved !== null) onSaved(saved.exercise, saved.withVideo);
  }, [saved, onSaved]);

  async function submit(values: ExerciseValues) {
    const fileError = exercise === null && video !== null ? validateVideoFile(video) : null;
    setVideoError(fileError);
    if (fileError !== null) return;

    const parsed = exerciseSchema.safeParse(values);
    if (!parsed.success) {
      applyZodIssues(parsed.error.issues, FIELDS, form.setError);
      return;
    }

    const data = parsed.data;
    try {
      const result = await mutation.mutateAsync({
        name: data.name,
        description: data.description.trim() || null,
        muscle_groups: serializeMuscleGroups(data.muscle_groups),
        equipment: data.equipment || null,
        difficulty_level: data.difficulty_level || null,
        video_url: exercise?.video_url ?? null,
      });
      toast.success(
        exercise === null ? 'Exercício criado com sucesso.' : 'Exercício atualizado com sucesso.'
      );
      if (exercise === null && video !== null) setPendingExerciseVideo(result.id, video);
      setSaved({ exercise: result, withVideo: exercise === null && video !== null });
    } catch (error) {
      applyServerErrors(error, SERVER_FIELDS, form.setError);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => void submit(values))}
        className="flex flex-col gap-4"
      >
        <FormField label="Nome" error={errors.name?.message}>
          {(control) => <Input {...control} autoComplete="off" {...form.register('name')} />}
        </FormField>
        <FormField label="Grupos musculares" error={errors.muscle_groups?.message}>
          {(control) => (
            <Controller
              control={form.control}
              name="muscle_groups"
              render={({ field }) => (
                <MuscleGroupPicker {...control} value={field.value} onChange={field.onChange} />
              )}
            />
          )}
        </FormField>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label="Equipamento" error={errors.equipment?.message}>
            {(control) => (
              <Input
                {...control}
                placeholder="ex.: Barra"
                autoComplete="off"
                {...form.register('equipment')}
              />
            )}
          </FormField>
          <FormField label="Dificuldade" error={errors.difficulty_level?.message}>
            {(control) => (
              <Input
                {...control}
                placeholder="ex.: Intermédio"
                autoComplete="off"
                {...form.register('difficulty_level')}
              />
            )}
          </FormField>
        </div>
        <FormField label="Descrição" error={errors.description?.message}>
          {(control) => (
            <Textarea
              {...control}
              rows={4}
              placeholder="Execução, cuidados, variações…"
              {...form.register('description')}
            />
          )}
        </FormField>
        {exercise === null && (
          <div className="flex flex-col gap-1 text-sm">
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
              MP4 ou MOV, H.264, até 100 MB e 3 minutos. O envio começa depois de criares o
              exercício.
            </p>
            {videoError !== null && (
              <span role="alert" className="text-destructive">
                {videoError}
              </span>
            )}
          </div>
        )}
        {errors.root && (
          <p role="alert" className="text-destructive text-sm">
            {errors.root.message}
          </p>
        )}
        <div className="flex justify-end">
          <Button type="submit" className="min-h-11 md:min-h-8" disabled={mutation.isPending}>
            {mutation.isPending
              ? exercise === null
                ? 'A criar…'
                : 'A guardar…'
              : exercise === null
                ? 'Criar'
                : 'Guardar'}
          </Button>
        </div>
      </form>
      {exercise !== null && (
        <ExerciseVideoPanel
          exercise={exercise}
          audience="trainer"
          listKey={libraryKeys.kind('exercises')}
          initialFile={initialFile}
          onUploadingChange={setUploading}
        />
      )}
      <UnsavedChangesDialog
        when={(form.formState.isDirty || video !== null || uploading) && saved === null}
        uploading={uploading}
      />
    </div>
  );
}
