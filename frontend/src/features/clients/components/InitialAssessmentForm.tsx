import { useEffect, useState } from 'react';
import { useForm, type FieldPath } from 'react-hook-form';
import { toast } from 'sonner';

import { useSaveInitialAssessmentMutation } from '@/features/clients/api/mutations';
import {
  assessmentDefaults,
  assessmentSchema,
  BodyAndGoalsFields,
  HabitsFields,
  MeasurementsFields,
  SERVER_FIELDS,
  toAssessmentRequest,
  type AssessmentValues,
} from '@/features/initial-assessment';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { Button } from '@/shared/components/ui/button';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';

type Assessment = components['schemas']['InitialAssessmentResponse'];

/**
 * Formulário da avaliação inicial (criar e substituir).
 *
 * Medidas e hábitos ficam em secções recolhidas: são opcionais e longas, e o que o trainer
 * precisa de preencher sempre (peso, altura, níveis e objetivos) fica à vista.
 *
 * @param clientId Cliente avaliado.
 * @param assessment Avaliação existente, ou `null` para criar.
 * Sair com alterações por guardar pede confirmação; `onSaved` corre depois de o guard
 * deixar de valer.
 *
 * @param onSaved Chamado depois de guardar com sucesso. Tem de ter identidade estável.
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
  const defaults = assessmentDefaults(assessment);
  const form = useForm<AssessmentValues>({ defaultValues: defaults });
  const [saved, setSaved] = useState(false);
  const blocker = useUnsavedChangesGuard(form.formState.isDirty && !saved);
  useEffect(() => {
    if (saved) onSaved();
  }, [saved, onSaved]);
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

    try {
      await mutation.mutateAsync(toAssessmentRequest(parsed.data));
      toast.success('Avaliação inicial guardada.');
      setSaved(true);
    } catch (error) {
      if (isApiProblem(error) && error.code === 'initial_assessment_already_exists') {
        form.setError('root', {
          message: 'Este cliente já tem uma avaliação. Atualiza a página para a editar.',
        });
      } else if (isApiProblem(error) && error.hasFieldErrors) {
        let mapped = false;
        for (const fieldError of error.fieldErrors) {
          const field = Object.hasOwn(SERVER_FIELDS, fieldError.field)
            ? SERVER_FIELDS[fieldError.field]
            : undefined;
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

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex h-full flex-col gap-4 pb-4"
    >
      <BodyAndGoalsFields form={form} />

      <details className="border-border rounded-lg border p-3">
        <summary className="cursor-pointer text-sm font-medium">Medidas corporais</summary>
        <div className="mt-3">
          <MeasurementsFields form={form} />
        </div>
      </details>

      <details className="border-border rounded-lg border p-3">
        <summary className="cursor-pointer text-sm font-medium">Hábitos e alimentação</summary>
        <div className="mt-3">
          <HabitsFields form={form} />
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
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title="Sair sem guardar?"
        description="As alterações a esta avaliação perdem-se."
        confirmLabel="Sair sem guardar"
        cancelLabel="Continuar a editar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </form>
  );
}
